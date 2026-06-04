using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MRP.Api.Data;
using MRP.Api.DTO;
using MRP.Api.Models;
using System.Collections.Generic;

namespace MRP.Api.Controllers;

// Производство

[ApiController]
[Route("api/[controller]")]
public class ProductionController : ControllerBase
{
    private readonly BikeContext _context;

    private const string BikeCode = "BIKE";

    public ProductionController(BikeContext context)
    {
        _context = context;
    }

    [HttpGet("capacity")]
    public async Task<ActionResult<ProductionCapacityDto>> GetCapacity([FromQuery] int productItemId)
    {
        var product = await _context.Items.FindAsync(productItemId);
        if (product == null) return NotFound();
        if (product.ItemType != ItemType.Product)
            return BadRequest("Выберите позицию типа «Готовая продукция» (Product).");

        var allBoms = await _context.Boms.Include(b => b.ChildItem).ToListAsync();
        if (!allBoms.Any(b => b.ParentItemID == productItemId))
            return BadRequest("У изделия нет строк спецификации.");

        var reqPerBike = new Dictionary<int, decimal>();
        AddLeafRequirements(productItemId, 1m, allBoms, reqPerBike, new HashSet<int>());
        if (reqPerBike.Count == 0)
            return BadRequest("Спецификация не раскрывается до материалов (проверьте BOM).");

        var stock = await GetCurrentStockByItemAsync();
        return Ok(BuildCapacityDtoFromExploded(productItemId, reqPerBike, stock, allBoms));
    }

    // Экономика производства
    [HttpGet("stats")]
    public async Task<ActionResult<ProductionStatsDto>> GetStats([FromQuery] int productItemId)
    {
        var product = await _context.Items.FindAsync(productItemId);
        if (product == null) return NotFound();
        if (product.ItemType != ItemType.Product)
            return BadRequest("Укажите готовую продукцию.");

        var allBoms = await _context.Boms.Include(b => b.ChildItem).ToListAsync();
        var reqPerBike = new Dictionary<int, decimal>();
        AddLeafRequirements(productItemId, 1m, allBoms, reqPerBike, new HashSet<int>());

        var itemIds = reqPerBike.Keys.ToList();
        var items = await _context.Items.Where(i => itemIds.Contains(i.ItemID)).ToDictionaryAsync(i => i.ItemID);

        decimal costPerBike = 0;
        foreach (var kv in reqPerBike)
        {
            var uc = items.GetValueOrDefault(kv.Key)?.UnitCost ?? 0;
            costPerBike += kv.Value * uc;
        }

        var producedQty = await _context.StockOperations
            .Where(s => s.ItemId == productItemId && s.OperationType == StockOperationType.Receipt)
            .SumAsync(s => s.Quantity);

        var totalMaterialCost = producedQty * costPerBike;
        decimal? totalRevenue = null;
        decimal? totalProfit = null;
        if (product.SellingPrice is { } sp && sp > 0)
        {
            totalRevenue = producedQty * sp;
            totalProfit = totalRevenue - totalMaterialCost;
        }

        return Ok(new ProductionStatsDto
        {
            ProductItemId = productItemId,
            ProducedQty = producedQty,
            CostPerBike = costPerBike,
            TotalMaterialCost = totalMaterialCost,
            SellingPrice = product.SellingPrice,
            TotalRevenue = totalRevenue,
            TotalProfit = totalProfit
        });
    }

    // Приход материалов
    [HttpPost("receipt-materials")]
    public async Task<IActionResult> ReceiptMaterials([FromBody] ReceiptMaterialsRequestDto? dto)
    {
        dto ??= new ReceiptMaterialsRequestDto();
        if (dto.BikeCount <= 0)
            return BadRequest("Количество должно быть целым числом больше нуля.");
        var bikeCount = dto.BikeCount < 1 ? 10 : dto.BikeCount;

        int productId;
        if (dto.ProductItemId is > 0)
        {
            var p = await _context.Items.FindAsync(dto.ProductItemId.Value);
            if (p == null) return NotFound("Изделие не найдено.");
            if (p.ItemType != ItemType.Product)
                return BadRequest("Нужна позиция типа «Готовая продукция».");
            productId = p.ItemID;
        }
        else
            productId = await EnsureStandardBikeCatalogAsync();

        var allBoms = await _context.Boms.Include(b => b.ChildItem).ToListAsync();
        if (!allBoms.Any(b => b.ParentItemID == productId))
            return BadRequest("Нет спецификации: сначала создайте BOM или используйте кнопку без выбора изделия (каталог BIKE).");

        var req = new Dictionary<int, decimal>();
        AddLeafRequirements(productId, bikeCount, allBoms, req, new HashSet<int>());
        if (req.Count == 0)
            return BadRequest("Нет материалов для прихода (проверьте BOM).");

        var now = DateTime.UtcNow;
        foreach (var kv in req)
        {
            var line = FindLeafBomLine(kv.Key, allBoms);
            if (line == null) continue;

            _context.StockOperations.Add(new StockOperation
            {
                SpecificationId = line.BOMID,
                ItemId = kv.Key,
                Date = now,
                Quantity = kv.Value,
                OperationType = StockOperationType.Receipt
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { ok = true, bikeCount, productItemId = productId });
    }

    [HttpPost("produce")]
    public async Task<IActionResult> Produce([FromBody] ProductionProduceRequestDto dto)
    {
        if (dto.Quantity <= 0)
            return BadRequest("Укажите целое количество изделий больше нуля.");

        var product = await _context.Items.FindAsync(dto.ProductItemId);
        if (product == null) return NotFound();
        if (product.ItemType != ItemType.Product)
            return BadRequest("Выберите позицию типа «Готовая продукция» (Product).");

        var allBoms = await _context.Boms.Include(b => b.ChildItem).ToListAsync();
        if (!allBoms.Any(b => b.ParentItemID == dto.ProductItemId))
            return BadRequest("Нет спецификации для изделия.");

        var reqPerBike = new Dictionary<int, decimal>();
        AddLeafRequirements(dto.ProductItemId, 1m, allBoms, reqPerBike, new HashSet<int>());
        var stock = await GetCurrentStockByItemAsync();
        var cap = BuildCapacityDtoFromExploded(dto.ProductItemId, reqPerBike, stock, allBoms);

        if (dto.Quantity > cap.MaxQty)
            return BadRequest($"Недостаточно материалов. По остаткам максимум: {cap.MaxQty} шт.");

        var now = DateTime.UtcNow;
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            AddIssueLeaves(dto.ProductItemId, dto.Quantity, allBoms, now, _context, new HashSet<int>());

            var receipt = new StockOperation
            {
                Date = now,
                Quantity = dto.Quantity,
                OperationType = StockOperationType.Receipt
            };
            await StockAccounting.ConfigureManualOperationAsync(_context, receipt, dto.ProductItemId);
            _context.StockOperations.Add(receipt);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return Ok(new { ok = true, produced = dto.Quantity, productItemId = dto.ProductItemId });
    }

    private async Task<int> EnsureStandardBikeCatalogAsync()
    {
        var existing = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.ItemCode == BikeCode);
        if (existing != null)
            return existing.ItemID;

        var bike = new Item
        {
            ItemCode = BikeCode,
            ItemName = "Велосипед",
            ItemType = ItemType.Product,
            Unit = "шт.",
            UnitCost = null,
            SellingPrice = null
        };
        var frame = new Item
        {
            ItemCode = "FRAME",
            ItemName = "Рама",
            ItemType = ItemType.Assembly,
            Unit = "шт.",
            UnitCost = null
        };
        var al = new Item
        {
            ItemCode = "AL",
            ItemName = "Алюминий",
            ItemType = ItemType.Material,
            Unit = "кг",
            UnitCost = 200
        };
        var chain = new Item
        {
            ItemCode = "CHAIN",
            ItemName = "Цепь",
            ItemType = ItemType.Material,
            Unit = "шт.",
            UnitCost = 400
        };
        var pedal = new Item
        {
            ItemCode = "PEDAL",
            ItemName = "Педаль",
            ItemType = ItemType.Material,
            Unit = "шт.",
            UnitCost = 150
        };
        var handle = new Item
        {
            ItemCode = "HANDLE",
            ItemName = "Руль",
            ItemType = ItemType.Material,
            Unit = "шт.",
            UnitCost = 800
        };
        var wheel = new Item
        {
            ItemCode = "WHEEL",
            ItemName = "Колесо",
            ItemType = ItemType.Assembly,
            Unit = "шт.",
            UnitCost = null
        };
        var tire = new Item
        {
            ItemCode = "TIRE",
            ItemName = "Покрышка",
            ItemType = ItemType.Material,
            Unit = "шт.",
            UnitCost = 120
        };
        var rim = new Item
        {
            ItemCode = "RIM",
            ItemName = "Обод",
            ItemType = ItemType.Material,
            Unit = "шт.",
            UnitCost = 350
        };
        var spoke = new Item
        {
            ItemCode = "SPOKE",
            ItemName = "Спица",
            ItemType = ItemType.Material,
            Unit = "шт.",
            UnitCost = 15
        };

        _context.Items.AddRange(bike, frame, al, chain, pedal, handle, wheel, tire, rim, spoke);
        await _context.SaveChangesAsync();

        var bomBikeFrame = new Bom { ParentItemID = bike.ItemID, ChildItemID = frame.ItemID, Quantity = 1 };
        var bomBikeChain = new Bom { ParentItemID = bike.ItemID, ChildItemID = chain.ItemID, Quantity = 1 };
        var bomBikePedal = new Bom { ParentItemID = bike.ItemID, ChildItemID = pedal.ItemID, Quantity = 2 };
        var bomBikeHandle = new Bom { ParentItemID = bike.ItemID, ChildItemID = handle.ItemID, Quantity = 1 };
        var bomBikeWheel = new Bom { ParentItemID = bike.ItemID, ChildItemID = wheel.ItemID, Quantity = 2 };
        var bomFrameAl = new Bom { ParentItemID = frame.ItemID, ChildItemID = al.ItemID, Quantity = 10 };
        var bomWheelTire = new Bom { ParentItemID = wheel.ItemID, ChildItemID = tire.ItemID, Quantity = 1 };
        var bomWheelRim = new Bom { ParentItemID = wheel.ItemID, ChildItemID = rim.ItemID, Quantity = 1 };
        var bomWheelSpoke = new Bom { ParentItemID = wheel.ItemID, ChildItemID = spoke.ItemID, Quantity = 32 };

        _context.Boms.AddRange(
            bomBikeFrame,
            bomBikeChain,
            bomBikePedal,
            bomBikeHandle,
            bomBikeWheel,
            bomFrameAl,
            bomWheelTire,
            bomWheelRim,
            bomWheelSpoke);
        await _context.SaveChangesAsync();

        return bike.ItemID;
    }

    private static bool HasChildBom(int itemId, List<Bom> allBoms) =>
        allBoms.Any(b => b.ParentItemID == itemId);

    private static Bom? FindLeafBomLine(int childItemId, List<Bom> allBoms) =>
        allBoms.FirstOrDefault(b => b.ChildItemID == childItemId && !HasChildBom(b.ChildItemID, allBoms));

    // Производство: потребности в материалах для производства
    // parentItemId - ID позиции номенклатуры
    // unitsOfParent - количество единиц родительской позиции
    // allBoms - BOM для позиции номенклатуры
    // reqPerBike - потребности по позициям номенклатуры
    private static void AddLeafRequirements(
        int parentItemId,
        decimal unitsOfParent,
        List<Bom> allBoms,
        Dictionary<int, decimal> reqPerBike,
        HashSet<int> visiting)
    {
        if (!visiting.Add(parentItemId))
            return;

        try
        {
            foreach (var bom in allBoms.Where(b => b.ParentItemID == parentItemId))
            {
                // Количество единиц для подпозиции
                var flow = bom.Quantity * unitsOfParent;
                // Если подпозиция имеет свои подпозиции, то рекурсивно разбиваем потребности
                if (HasChildBom(bom.ChildItemID, allBoms))
                    AddLeafRequirements(bom.ChildItemID, flow, allBoms, reqPerBike, visiting);
                else
                    // Добавляем потребности в словарь
                    reqPerBike[bom.ChildItemID] = reqPerBike.GetValueOrDefault(bom.ChildItemID) + flow;
            }
        }
        finally
        {
            visiting.Remove(parentItemId);
        }
    }

    // Производство: расход материалов при выпуске
    // parentItemId - ID позиции номенклатуры
    // unitsOfParent - количество единиц родительской позиции
    // allBoms - BOM для позиции номенклатуры
    // reqPerBike - потребности по позициям номенклатуры
    private static void AddIssueLeaves(
        int parentItemId,
        decimal unitsOfParent,
        List<Bom> allBoms,
        DateTime now,
        BikeContext context,
        HashSet<int> visiting)
    {
        if (!visiting.Add(parentItemId))
            return;

        try
        {
            foreach (var bom in allBoms.Where(b => b.ParentItemID == parentItemId))
            {
                // На листьях создаётся операция
                var qty = bom.Quantity * unitsOfParent;
                if (HasChildBom(bom.ChildItemID, allBoms))
                    AddIssueLeaves(bom.ChildItemID, qty, allBoms, now, context, visiting);
                else
                {
                    context.StockOperations.Add(new StockOperation
                    {
                        SpecificationId = bom.BOMID,
                        ItemId = bom.ChildItemID,
                        Date = now,
                        Quantity = qty,
                        OperationType = StockOperationType.Issue
                    });
                }
            }
        }
        finally
        {
            visiting.Remove(parentItemId);
        }
    }

    // Максимальный выпуск изделий из остатков (capacity)
    // productItemId - ID позиции номенклатуры
    // reqPerBike - потребности по позициям номенклатуры
    // stock - остатки по позициям номенклатуры
    // allBoms - BOM для позиции номенклатуры
    // lines - линии для отображения
    // maxQty - максимальное количество изделий
    // limitingId - ID позиции, ограничивающей максимальное количество изделий
    private static ProductionCapacityDto BuildCapacityDtoFromExploded(
        int productItemId,
        Dictionary<int, decimal> reqPerBike,
        Dictionary<int, decimal> stock,
        List<Bom> allBoms)
    {
        var names = allBoms
            .Where(b => b.ChildItem != null)
            .GroupBy(b => b.ChildItemID)
            .ToDictionary(g => g.Key, g => g.First().ChildItem!.ItemName);

        var lines = new List<ProductionCapacityLineDto>();
        var maxQty = int.MaxValue;
        int? limitingId = null;

        foreach (var kv in reqPerBike.OrderBy(x => names.GetValueOrDefault(x.Key, "")))
        {
            var childId = kv.Key;
            var per = kv.Value;
            var s = stock.GetValueOrDefault(childId);
            var name = names.GetValueOrDefault(childId) ?? $"ID {childId}";

            // Максимальное количество изделий из остатков для данной позиции
            int maxFromLine;
            if (per <= 0)
                maxFromLine = int.MaxValue;
            else if (s < 0)
                maxFromLine = 0;
            else
                maxFromLine = (int)decimal.Floor(s / per);

            if (maxFromLine == int.MaxValue)
                maxFromLine = 0;

            lines.Add(new ProductionCapacityLineDto
            {
                BomId = 0,
                ChildItemId = childId,
                ChildName = name,
                QtyPerUnit = per,
                CurrentStock = s,
                MaxFromThisLine = maxFromLine
            });

            if (maxFromLine < maxQty)
            {
                maxQty = maxFromLine;
                limitingId = childId;
            }
        }

        if (maxQty == int.MaxValue)
            maxQty = 0;

        return new ProductionCapacityDto
        {
            ProductItemId = productItemId,
            MaxQty = maxQty,
            LimitingItemId = maxQty == 0 ? null : limitingId,
            Lines = lines
        };
    }

    private Task<Dictionary<int, decimal>> GetCurrentStockByItemAsync() =>
        StockAccounting.GetNetStockByItemAsync(_context, DateTime.UtcNow);
}
