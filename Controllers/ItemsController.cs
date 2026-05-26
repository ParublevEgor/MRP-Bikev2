using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MRP.Api;
using MRP.Api.Data;
using MRP.Api.DTO;
using MRP.Api.Models;

namespace MRP.Api.Controllers;

// Номенклатура (Остатки)

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly BikeContext _context;

    public ItemsController(BikeContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ItemDto>>> Get()
    {
        var computedCosts = await BuildComputedItemCostsAsync();
        var items = await _context.Items
            .OrderBy(i => i.ItemID)
            .ToListAsync();

        return Ok(items.Select(i => ToDto(i, computedCosts.GetValueOrDefault(i.ItemID))));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ItemDto>> GetById(int id)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.ItemID == id);
        if (item == null) return NotFound();
        var computedCosts = await BuildComputedItemCostsAsync();
        return Ok(ToDto(item, computedCosts.GetValueOrDefault(item.ItemID)));
    }

    [HttpGet("stock-balance")]
    public async Task<ActionResult<IEnumerable<ItemStockBalanceDto>>> GetStockBalance([FromQuery] string? asOf)
    {
        var asOfUtc = PlanningAsOf.ResolveStockAsOfUtc(asOf);
        var stockCutoffUtc = PlanningAsOf.StockOperationsCutoffUtc(asOf);
        var byItemAgg = await StockAccounting.GetReceiptIssueByItemAsync(_context, stockCutoffUtc);
        var raw = byItemAgg.Select(kv => new
        {
            ItemId = kv.Key,
            kv.Value.ReceiptQty,
            kv.Value.IssueQty
        }).ToList();

        var byItem = raw.ToDictionary(
            x => x.ItemId,
            x => new { x.ReceiptQty, x.IssueQty });

        var result = await _context.Items
            .Where(i => i.ItemCode != StockAccounting.SysStockCode)
            .OrderBy(i => i.ItemID)
            .Select(i => new ItemStockBalanceDto
            {
                ItemID = i.ItemID,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                Unit = i.Unit
            })
            .ToListAsync();

        var boms = await _context.Boms.AsNoTracking().ToListAsync();
        var allOrders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .OrderBy(o => o.OrderDate)
            .ThenBy(o => o.OrderID)
            .ToListAsync();
        // Физический склад до планирования заказов
        var stockByItem = byItem.ToDictionary(x => x.Key, x => x.Value.ReceiptQty - x.Value.IssueQty);

        var ordersForPlan = PlanningAsOf.ShouldFilterOrdersByDate(asOf)
            ? allOrders.Where(o => o.OrderDate <= asOfUtc).ToList()
            : allOrders;

        var (orderQty, remainingAfterOrders) = BuildPlannedOrderQtyWithRemaining(ordersForPlan, boms, stockByItem);

        foreach (var item in result)
        {
            if (!byItem.TryGetValue(item.ItemID, out var agg))
            {
                item.ReceiptQty = 0;
                item.IssueQty = 0;
            }
            else
            {
                item.ReceiptQty = agg.ReceiptQty;
                item.IssueQty = agg.IssueQty;
            }

            item.OrderQty = orderQty.GetValueOrDefault(item.ItemID);
            item.AdjustmentQty = 0;
            // Остаток после симуляции заказов
            item.CurrentStock = Math.Max(0m, remainingAfterOrders.GetValueOrDefault(item.ItemID));
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ItemDto dto)
    {
        var err = ValidateItemDto(dto);
        if (err != null) return BadRequest(err);

        if (!Enum.TryParse<ItemType>(dto.ItemType, true, out var itemType))
            return BadRequest("Неверный тип. Допустимо: Product, Assembly, Component, Material.");

        dto.ItemName = dto.ItemName.Trim();
        dto.ItemCode = string.IsNullOrWhiteSpace(dto.ItemCode) ? null : dto.ItemCode.Trim();

        if (dto.ItemCode != null)
        {
            var dup = await _context.Items.AnyAsync(i => i.ItemCode == dto.ItemCode);
            if (dup) return BadRequest("Код позиции уже занят.");
        }

        var item = new Item
        {
            ItemCode = dto.ItemCode,
            ItemName = dto.ItemName,
            ItemType = itemType,
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? null : dto.Unit.Trim(),
            UnitCost = dto.UnitCost,
            SellingPrice = dto.SellingPrice
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        return Ok(ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ItemDto dto)
    {
        var err = ValidateItemDto(dto);
        if (err != null) return BadRequest(err);

        if (!Enum.TryParse<ItemType>(dto.ItemType, true, out var itemType))
            return BadRequest("Неверный тип. Допустимо: Product, Assembly, Component, Material.");

        var item = await _context.Items.FindAsync(id);
        if (item == null) return NotFound();

        dto.ItemName = dto.ItemName.Trim();
        dto.ItemCode = string.IsNullOrWhiteSpace(dto.ItemCode) ? null : dto.ItemCode.Trim();

        if (dto.ItemCode != null)
        {
            var dup = await _context.Items.AnyAsync(i => i.ItemCode == dto.ItemCode && i.ItemID != id);
            if (dup) return BadRequest("Код позиции уже занят.");
        }

        item.ItemCode = dto.ItemCode;
        item.ItemName = dto.ItemName;
        item.ItemType = itemType;
        item.Unit = string.IsNullOrWhiteSpace(dto.Unit) ? null : dto.Unit.Trim();
        item.UnitCost = dto.UnitCost;
        item.SellingPrice = dto.SellingPrice;

        await _context.SaveChangesAsync();

        return Ok(ToDto(item));
    }  
    
    // Удаление позиции номенклатуры
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var itemExists = await _context.Items.AnyAsync(i => i.ItemID == id);
        if (!itemExists) return NotFound();

        var stockByItem = await _context.StockOperations
            .Where(s => s.ItemId == id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ReceiptQty = g.Where(x => x.OperationType == StockOperationType.Receipt).Sum(x => (decimal?)x.Quantity) ?? 0m,
                IssueQty = g.Where(x => x.OperationType == StockOperationType.Issue).Sum(x => (decimal?)x.Quantity) ?? 0m
            })
            .FirstOrDefaultAsync();

        var currentStock = stockByItem == null ? 0m : stockByItem.ReceiptQty - stockByItem.IssueQty;
        if (currentStock > 0)
            return BadRequest("Нельзя удалить позицию: по ней есть положительные остатки.");

        var bomIds = await _context.Boms
            .AsNoTracking()
            .Where(b => b.ParentItemID == id || b.ChildItemID == id)
            .Select(b => b.BOMID)
            .ToListAsync();

        if (bomIds.Count > 0)
        {
            await _context.StockOperations
                .Where(s => bomIds.Contains(s.SpecificationId))
                .ExecuteDeleteAsync();
            await _context.Boms
                .Where(b => bomIds.Contains(b.BOMID))
                .ExecuteDeleteAsync();
        }

        var deleted = await _context.Items.Where(i => i.ItemID == id).ExecuteDeleteAsync();
        return deleted == 0 ? NotFound() : NoContent();
    }

    private static string? ValidateItemDto(ItemDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ItemName))
            return "Укажите наименование.";
        if (dto.ItemName.Trim().Length > 100)
            return "Наименование не длиннее 100 символов.";
        if (!string.IsNullOrEmpty(dto.ItemCode) && dto.ItemCode.Length > 20)
            return "Код не длиннее 20 символов.";
        if (!string.IsNullOrWhiteSpace(dto.Unit) && dto.Unit.Any(char.IsDigit))
            return "Единица измерения должна быть строкой, без цифр.";
        if (dto.UnitCost is { } unitCost && unitCost != decimal.Truncate(unitCost))
            return "Себестоимость должна быть целым числом.";
        if (dto.SellingPrice is { } sellingPrice && sellingPrice != decimal.Truncate(sellingPrice))
            return "Отпускная цена должна быть целым числом.";
        return null;
    }

    // Разузловывание заказов по BOM
    private static (Dictionary<int, decimal> PlannedQty, Dictionary<int, decimal> RemainingStock) BuildPlannedOrderQtyWithRemaining(
        List<Order> orders,
        List<Bom> boms,
        Dictionary<int, decimal> stockByItem)
    {
        var childrenByParent = boms
            .GroupBy(x => x.ParentItemID)
            .ToDictionary(g => g.Key, g => g.ToList());

        var remaining = new Dictionary<int, decimal>();
        foreach (var kv in stockByItem)
            remaining[kv.Key] = Math.Max(0m, kv.Value);

        var planned = new Dictionary<int, decimal>();

        foreach (var order in orders)
        {
            foreach (var line in order.Lines)
                ReserveOrExplodeForPlan(
                    line.ItemID,
                    line.Quantity,
                    childrenByParent,
                    remaining,
                    planned,
                    new HashSet<int>());
        }

        return (planned, remaining);
    }

    private static void ReserveOrExplodeForPlan(
        int itemId,
        decimal requiredQty,
        Dictionary<int, List<Bom>> childrenByParent,
        Dictionary<int, decimal> remaining,
        Dictionary<int, decimal> planned,
        HashSet<int> visiting)
    {
        if (requiredQty <= 0m)
            return;
        // Для каждой позиции в заказе: колонка "Заказ"
        planned[itemId] = planned.GetValueOrDefault(itemId) + requiredQty;
        // Остаток после выделения на заказ
        var available = Math.Max(0m, remaining.GetValueOrDefault(itemId));
        // Резерв со склада
        var reserve = Math.Min(available, requiredQty);
        remaining[itemId] = available - reserve;
        var shortage = requiredQty - reserve;
        if (shortage <= 0m)
            return;

        if (!childrenByParent.TryGetValue(itemId, out var lines) || lines.Count == 0)
            return;

        if (!visiting.Add(itemId))
            return;

        try
        {
            // Разузловывание на подпозиции
            foreach (var bom in lines)
            {
                if (bom.Quantity <= 0m)
                    continue;
                ReserveOrExplodeForPlan(
                    bom.ChildItemID,
                    shortage * bom.Quantity,
                    childrenByParent,
                    remaining,
                    planned,
                    visiting);
            }
        }
        finally
        {
            visiting.Remove(itemId);
        }
    }

    // Расчёт себестоимости по BOM
    private async Task<Dictionary<int, decimal?>> BuildComputedItemCostsAsync()
    {
        var items = await _context.Items.AsNoTracking().ToListAsync();
        var boms = await _context.Boms.AsNoTracking().ToListAsync();
        var itemsById = items.ToDictionary(x => x.ItemID);
        var childrenByParent = boms
            .GroupBy(x => x.ParentItemID)
            .ToDictionary(g => g.Key, g => g.ToList());

        var memo = new Dictionary<int, decimal?>();
        var visiting = new HashSet<int>();

        decimal? ComputeCost(int itemId)
        {
            if (memo.TryGetValue(itemId, out var cached))
                return cached;
            if (!itemsById.TryGetValue(itemId, out var item))
                return null;
            if (!visiting.Add(itemId))
                return item.UnitCost;

            try
            {
                if (!childrenByParent.TryGetValue(itemId, out var lines) || lines.Count == 0)
                {
                    memo[itemId] = item.UnitCost;
                    return item.UnitCost;
                }

                decimal total = 0;
                foreach (var line in lines)
                {
                    var childCost = ComputeCost(line.ChildItemID);
                    if (childCost == null)
                    {
                        memo[itemId] = item.UnitCost;
                        return item.UnitCost;
                    }
                    total += line.Quantity * childCost.Value;
                }

                var computed = decimal.Round(total, 2, MidpointRounding.AwayFromZero);
                memo[itemId] = computed;
                return computed;
            }
            finally
            {
                visiting.Remove(itemId);
            }
        }

        foreach (var item in items)
            ComputeCost(item.ItemID);

        return memo;
    }

    private static ItemDto ToDto(Item i, decimal? computedUnitCost = null) => new()
    {
        ItemID = i.ItemID,
        ItemCode = i.ItemCode,
        ItemName = i.ItemName,
        ItemType = i.ItemType.ToString(),
        Unit = i.Unit,
        UnitCost = computedUnitCost ?? i.UnitCost,
        SellingPrice = i.SellingPrice
    };
}
