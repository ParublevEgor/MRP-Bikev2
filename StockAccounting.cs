using Microsoft.EntityFrameworkCore;
using MRP.Api.Data;
using MRP.Api.Models;

namespace MRP.Api;


// Складской учёт по позиции номенклатуры

public static class StockAccounting
{
    // Служебный код для позиции номенклатуры
    public const string SysStockCode = "SYS-STOCK";

    // Создание служебной позиции номенклатуры
    public static async Task<Item> EnsureSysStockItemAsync(BikeContext context)
    {
        // Проверка на существование служебной позиции номенклатуры
        var sys = await context.Items.FirstOrDefaultAsync(i => i.ItemCode == SysStockCode);
        if (sys != null)
            return sys;

        sys = new Item
        {
            ItemCode = SysStockCode,
            ItemName = "Учёт прихода на склад",
            ItemType = ItemType.Assembly,
            Unit = "шт."
        };
        context.Items.Add(sys);
        await context.SaveChangesAsync();
        return sys;
    }
    // Создание строки BOM для прихода материалов
    // context - контекст базы данных
    // itemId - ID позиции номенклатуры
    // Возвращает ID строки BOM
    // Создание строки BOM для прихода материалов
    public static async Task<int> EnsureStockReceiptBomAsync(BikeContext context, int itemId)
    {
        // Создание служебной позиции номенклатуры
        var sys = await EnsureSysStockItemAsync(context);
        // Проверка на существование строки BOM для прихода материалов
        var existing = await context.Boms.FirstOrDefaultAsync(b =>
            b.ParentItemID == sys.ItemID && b.ChildItemID == itemId);

        if (existing != null)
            return existing.BOMID;

        var bom = new Bom
        {
            ParentItemID = sys.ItemID,
            ChildItemID = itemId,
            Quantity = 1
        };
        context.Boms.Add(bom);
        await context.SaveChangesAsync();
        return bom.BOMID;
    }

    // Конфигурирование операции прихода материалов
    // context - контекст базы данных
    // operation - операция прихода материалов
    // itemId - ID позиции номенклатуры
    // Возвращает ID строки BOM
    // Конфигурирование операции прихода материалов
    public static async Task ConfigureManualOperationAsync(BikeContext context, StockOperation operation, int itemId)
    {
        // Проверка на существование позиции номенклатуры
        if (itemId <= 0)
            throw new ArgumentOutOfRangeException(nameof(itemId), "Укажите позицию номенклатуры.");

        var item = await context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.ItemID == itemId);
        if (item == null)
            throw new InvalidOperationException("Позиция номенклатуры не найдена.");
        if (item.ItemCode == SysStockCode)
            throw new InvalidOperationException("Служебные позиции нельзя оприходовать вручную.");

        operation.ItemId = itemId;
        operation.SpecificationId = await EnsureStockReceiptBomAsync(context, itemId);
    }

    public static async Task<HashSet<int>> GetSystemItemIdsAsync(BikeContext context)
    {
        var ids = await context.Items
            .AsNoTracking()
            .Where(i => i.ItemCode == SysStockCode)
            .Select(i => i.ItemID)
            .ToListAsync();
        return ids.ToHashSet();
    }

    public static IQueryable<StockOperation> ExcludeSystemItems(
        IQueryable<StockOperation> query,
        HashSet<int> systemItemIds) =>
        query.Where(s => s.ItemId > 0 && !systemItemIds.Contains(s.ItemId));

    public static async Task RepairWarehouseOperationsAsync(BikeContext context)
    {
        var systemIds = await GetSystemItemIdsAsync(context);
        var sysStockId = await context.Items
            .AsNoTracking()
            .Where(i => i.ItemCode == SysStockCode)
            .Select(i => (int?)i.ItemID)
            .FirstOrDefaultAsync();

        var wrongSysReceipts = await context.StockOperations
            .Where(s => systemIds.Contains(s.ItemId))
            .ToListAsync();
        if (wrongSysReceipts.Count > 0)
            context.StockOperations.RemoveRange(wrongSysReceipts);

        var boms = await context.Boms.AsNoTracking().ToDictionaryAsync(b => b.BOMID);
        var ops = await context.StockOperations
            .Where(s => s.ItemId <= 0 || systemIds.Contains(s.ItemId))
            .ToListAsync();

        foreach (var op in ops)
        {
            if (!boms.TryGetValue(op.SpecificationId, out var bom))
                continue;

            if (sysStockId is > 0 && bom.ParentItemID == sysStockId.Value && !systemIds.Contains(bom.ChildItemID))
                op.ItemId = bom.ChildItemID;
            else if (!systemIds.Contains(bom.ChildItemID))
                op.ItemId = bom.ChildItemID;
        }

        await context.SaveChangesAsync();
        await RemoveLegacySysGpAsync(context);
    }

    // Удаление устаревшей служебной позиции SYS-GP
    // context - контекст базы данных
    public static async Task RemoveLegacySysGpAsync(BikeContext context)
    {
        const string legacyCode = "SYS-GP";
        var sysGp = await context.Items.FirstOrDefaultAsync(i => i.ItemCode == legacyCode);
        if (sysGp == null)
            return;

        var sysGpId = sysGp.ItemID;
        var bomIds = await context.Boms
            .Where(b => b.ParentItemID == sysGpId || b.ChildItemID == sysGpId)
            .Select(b => b.BOMID)
            .ToListAsync();

        if (bomIds.Count > 0)
        {
            await context.StockOperations
                .Where(s => bomIds.Contains(s.SpecificationId))
                .ExecuteDeleteAsync();
            await context.Boms
                .Where(b => bomIds.Contains(b.BOMID))
                .ExecuteDeleteAsync();
        }

        context.Items.Remove(sysGp);
        await context.SaveChangesAsync();
    }

    public static async Task<Dictionary<int, decimal>> GetNetStockByItemAsync(
        BikeContext context,
        DateTime? cutoffUtc = null)
    {
        var systemIds = await GetSystemItemIdsAsync(context);
        var query = ExcludeSystemItems(context.StockOperations.AsNoTracking(), systemIds);
        if (cutoffUtc.HasValue)
            query = query.Where(s => s.Date <= cutoffUtc.Value);

        var raw = await query
            .GroupBy(s => s.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                ReceiptQty = g.Where(x => x.OperationType == StockOperationType.Receipt)
                    .Sum(x => (decimal?)x.Quantity) ?? 0m,
                IssueQty = g.Where(x => x.OperationType == StockOperationType.Issue)
                    .Sum(x => (decimal?)x.Quantity) ?? 0m
            })
            .ToListAsync();

        return raw.ToDictionary(x => x.ItemId, x => x.ReceiptQty - x.IssueQty);
    }

    // Получает остатки по позициям номенклатуры
    public static async Task<Dictionary<int, (decimal ReceiptQty, decimal IssueQty)>> GetReceiptIssueByItemAsync(
        BikeContext context,
        DateTime? cutoffUtc = null)
    {
        var systemIds = await GetSystemItemIdsAsync(context);
        var query = ExcludeSystemItems(
            context.StockOperations.AsNoTracking().Where(s => s.Quantity != 0),
            systemIds);
        if (cutoffUtc.HasValue)
            query = query.Where(s => s.Date <= cutoffUtc.Value);

        var raw = await query
            .GroupBy(s => s.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                ReceiptQty = g.Where(x => x.OperationType == StockOperationType.Receipt)
                    .Sum(x => (decimal?)x.Quantity) ?? 0m,
                IssueQty = g.Where(x => x.OperationType == StockOperationType.Issue)
                    .Sum(x => (decimal?)x.Quantity) ?? 0m
            })
            .ToListAsync();

        return raw.ToDictionary(x => x.ItemId, x => (x.ReceiptQty, x.IssueQty));
    }
}
