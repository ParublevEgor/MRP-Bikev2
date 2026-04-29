using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MRP.Api.Data;
using MRP.Api.DTO;
using MRP.Api.Models;
using System.Globalization;

namespace MRP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly BikeContext _context;

    public OrdersController(BikeContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> Get([FromQuery] string? dateTime)
    {
        await EnsureOrderTablesAsync();
        var dateFilter = TryParseDateFilter(dateTime);
        var allOrders = await _context.Orders
            .Include(o => o.Lines)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
        var orders = allOrders;

        if (dateFilter != null)
        {
            orders = orders
                .Where(o =>
                    o.OrderDate >= dateFilter.Value.FromUtc &&
                    o.OrderDate < dateFilter.Value.ToUtc)
                .ToList();
        }

        var itemsById = await _context.Items.AsNoTracking().ToDictionaryAsync(i => i.ItemID);
        var boms = await _context.Boms.AsNoTracking().ToListAsync();
        var stockByItem = await GetCurrentStockByItemAsync();
        var deficitByOrderId = ComputeDeficitPerOpenOrderFifo(allOrders, boms, stockByItem, itemsById);
        var computedCosts = BuildComputedItemCosts(itemsById, boms);

        return Ok(orders.Select(o => ToDto(o, itemsById, computedCosts, deficitByOrderId)));
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] OrderDto dto)
    {
        await EnsureOrderTablesAsync();
        var status = ParseStatus(dto.OrderType);
        if (status == null) return BadRequest("Неверный тип заказа. Допустимо: open, closed.");

        var validation = await ValidateOrderDtoAsync(dto, null);
        if (validation != null) return BadRequest(validation);

        var order = new Order
        {
            OrderDate = dto.OrderDate,
            DueDate = dto.DueDate,
            Status = status.Value,
            Lines = dto.Items.Select(x => new OrderLine
            {
                ItemID = x.ItemId,
                Quantity = x.Quantity
            }).ToList()
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        var allOrders = await _context.Orders.Include(x => x.Lines).ToListAsync();
        var itemsById = await _context.Items.AsNoTracking().ToDictionaryAsync(i => i.ItemID);
        var boms = await _context.Boms.AsNoTracking().ToListAsync();
        var stockByItem = await GetCurrentStockByItemAsync();
        var deficitByOrderId = ComputeDeficitPerOpenOrderFifo(allOrders, boms, stockByItem, itemsById);
        var computedCosts = BuildComputedItemCosts(itemsById, boms);
        return Ok(ToDto(order, itemsById, computedCosts, deficitByOrderId));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OrderDto>> Update(int id, [FromBody] OrderDto dto)
    {
        await EnsureOrderTablesAsync();
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.OrderID == id);
        if (order == null) return NotFound();
        if (order.Status == OrderStatus.Closed) return BadRequest("Закрытый заказ изменять нельзя.");

        var status = ParseStatus(dto.OrderType);
        if (status == null) return BadRequest("Неверный тип заказа. Допустимо: open, closed.");

        var validation = await ValidateOrderDtoAsync(dto, id);
        if (validation != null) return BadRequest(validation);

        order.OrderDate = dto.OrderDate;
        order.DueDate = dto.DueDate;
        order.Status = status.Value;

        _context.OrderLines.RemoveRange(order.Lines);
        order.Lines = dto.Items.Select(x => new OrderLine
        {
            ItemID = x.ItemId,
            Quantity = x.Quantity
        }).ToList();

        await _context.SaveChangesAsync();
        var allOrders = await _context.Orders.Include(x => x.Lines).ToListAsync();
        var itemsById = await _context.Items.AsNoTracking().ToDictionaryAsync(i => i.ItemID);
        var boms = await _context.Boms.AsNoTracking().ToListAsync();
        var stockByItem = await GetCurrentStockByItemAsync();
        var deficitByOrderId = ComputeDeficitPerOpenOrderFifo(allOrders, boms, stockByItem, itemsById);
        var computedCosts = BuildComputedItemCosts(itemsById, boms);
        return Ok(ToDto(order, itemsById, computedCosts, deficitByOrderId));
    }

    [HttpPost("{id:int}/close")]
    public async Task<ActionResult<OrderDto>> Close(int id)
    {
        await EnsureOrderTablesAsync();
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.OrderID == id);
        if (order == null) return NotFound();
        if (order.Status == OrderStatus.Closed) return BadRequest("Заказ уже закрыт.");

        order.Status = OrderStatus.Closed;
        await _context.SaveChangesAsync();
        var allOrders = await _context.Orders.Include(x => x.Lines).ToListAsync();
        var itemsById = await _context.Items.AsNoTracking().ToDictionaryAsync(i => i.ItemID);
        var boms = await _context.Boms.AsNoTracking().ToListAsync();
        var stockByItem = await GetCurrentStockByItemAsync();
        var deficitByOrderId = ComputeDeficitPerOpenOrderFifo(allOrders, boms, stockByItem, itemsById);
        var computedCosts = BuildComputedItemCosts(itemsById, boms);
        return Ok(ToDto(order, itemsById, computedCosts, deficitByOrderId));
    }

    [HttpPost("{id:int}/open")]
    public async Task<ActionResult<OrderDto>> Open(int id)
    {
        await EnsureOrderTablesAsync();
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.OrderID == id);
        if (order == null) return NotFound();
        if (order.Status == OrderStatus.Open) return BadRequest("Заказ уже открыт.");

        order.Status = OrderStatus.Open;
        await _context.SaveChangesAsync();
        var allOrders = await _context.Orders.Include(x => x.Lines).ToListAsync();
        var itemsById = await _context.Items.AsNoTracking().ToDictionaryAsync(i => i.ItemID);
        var boms = await _context.Boms.AsNoTracking().ToListAsync();
        var stockByItem = await GetCurrentStockByItemAsync();
        var deficitByOrderId = ComputeDeficitPerOpenOrderFifo(allOrders, boms, stockByItem, itemsById);
        var computedCosts = BuildComputedItemCosts(itemsById, boms);
        return Ok(ToDto(order, itemsById, computedCosts, deficitByOrderId));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await EnsureOrderTablesAsync();
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> ValidateOrderDtoAsync(OrderDto dto, int? editingOrderId)
    {
        if (dto.OrderDate == default || dto.DueDate == default)
            return "Укажите даты заказа.";
        if ((dto.DueDate - dto.OrderDate) < TimeSpan.FromHours(1))
            return "Дата готовности должна быть минимум на 1 час позже даты оформления.";
        if (dto.Items == null || dto.Items.Count == 0)
            return "Добавьте минимум одно изделие в заказ.";
        if (dto.Items.Any(x => x.Quantity <= 0))
            return "Количество изделий должно быть целым числом больше нуля.";
        if (dto.Items.GroupBy(x => x.ItemId).Any(g => g.Count() > 1))
            return "В одном заказе каждое изделие должно быть уникальным.";

        var itemIds = dto.Items.Select(x => x.ItemId).Distinct().ToList();
        var items = await _context.Items
            .Where(i => itemIds.Contains(i.ItemID))
            .ToDictionaryAsync(i => i.ItemID);

        if (items.Count != itemIds.Count)
            return "Одно или несколько изделий не найдены.";
        if (items.Values.Any(i => i.ItemType == ItemType.Material))
            return "Материалы нельзя добавлять в заказ.";

        return null;
    }

    private async Task<Dictionary<int, decimal>> GetCurrentStockByItemAsync()
    {
        var now = DateTime.UtcNow;
        var stockRaw = await _context.StockOperations
            .Join(
                _context.Boms,
                s => s.SpecificationId,
                b => b.BOMID,
                (s, b) => new { b.ChildItemID, s.OperationType, s.Quantity, s.Date })
            .Where(x => x.Date <= now)
            .GroupBy(x => x.ChildItemID)
            .Select(g => new
            {
                ItemId = g.Key,
                ReceiptQty = g.Where(x => x.OperationType == StockOperationType.Receipt).Sum(x => (decimal?)x.Quantity) ?? 0m,
                IssueQty = g.Where(x => x.OperationType == StockOperationType.Issue).Sum(x => (decimal?)x.Quantity) ?? 0m
            })
            .ToDictionaryAsync(x => x.ItemId, x => x.ReceiptQty - x.IssueQty);

        return stockRaw;
    }

    private static Dictionary<int, List<OrderDeficitLineDto>> ComputeDeficitPerOpenOrderFifo(
        List<Order> allOrders,
        List<Bom> boms,
        Dictionary<int, decimal> stockByItem,
        Dictionary<int, Item> itemsById)
    {
        var childrenByParent = boms
            .GroupBy(x => x.ParentItemID)
            .ToDictionary(g => g.Key, g => g.ToList());

        var remaining = new Dictionary<int, decimal>();
        foreach (var kv in stockByItem)
            remaining[kv.Key] = Math.Max(0m, kv.Value);

        var ordered = allOrders
            .OrderBy(o => o.OrderDate)
            .ThenBy(o => o.OrderID)
            .ToList();

        var result = new Dictionary<int, List<OrderDeficitLineDto>>();

        foreach (var order in ordered)
        {
            var demandForOrder = new Dictionary<int, decimal>();
            foreach (var line in order.Lines)
                ReserveOrExplodeShortage(
                    line.ItemID,
                    line.Quantity,
                    childrenByParent,
                    remaining,
                    demandForOrder,
                    new HashSet<int>());

            var lines = new List<OrderDeficitLineDto>();
            foreach (var kv in demandForOrder.OrderBy(x => itemsById.GetValueOrDefault(x.Key)?.ItemName ?? $"ID {x.Key}"))
            {
                var itemId = kv.Key;
                var deficitQty = kv.Value;
                if (deficitQty <= 0m)
                    continue;

                lines.Add(new OrderDeficitLineDto
                {
                    ItemId = itemId,
                    ItemName = itemsById.GetValueOrDefault(itemId)?.ItemName ?? $"ID {itemId}",
                    RequiredQty = decimal.Round(deficitQty, 4, MidpointRounding.AwayFromZero),
                    InStockQty = 0m,
                    DeficitQty = decimal.Round(deficitQty, 4, MidpointRounding.AwayFromZero)
                });
            }

            result[order.OrderID] = lines
                .OrderByDescending(x => x.DeficitQty)
                .ThenBy(x => x.ItemName)
                .ToList();
        }

        return result;
    }

    private static void ReserveOrExplodeShortage(
        int itemId,
        decimal requiredQty,
        Dictionary<int, List<Bom>> childrenByParent,
        Dictionary<int, decimal> remaining,
        Dictionary<int, decimal> deficitLeaves,
        HashSet<int> visiting)
    {
        if (requiredQty <= 0m)
            return;

        var available = Math.Max(0m, remaining.GetValueOrDefault(itemId));
        var reserve = Math.Min(available, requiredQty);
        remaining[itemId] = available - reserve;
        var shortage = requiredQty - reserve;
        if (shortage <= 0m)
            return;

        if (!childrenByParent.TryGetValue(itemId, out var lines) || lines.Count == 0)
        {
            deficitLeaves[itemId] = deficitLeaves.GetValueOrDefault(itemId) + shortage;
            return;
        }

        if (!visiting.Add(itemId))
        {
            deficitLeaves[itemId] = deficitLeaves.GetValueOrDefault(itemId) + shortage;
            return;
        }

        try
        {
            foreach (var bom in lines)
            {
                if (bom.Quantity <= 0m)
                    continue;
                ReserveOrExplodeShortage(
                    bom.ChildItemID,
                    shortage * bom.Quantity,
                    childrenByParent,
                    remaining,
                    deficitLeaves,
                    visiting);
            }
        }
        finally
        {
            visiting.Remove(itemId);
        }
    }

    private static Dictionary<int, decimal?> BuildComputedItemCosts(
        Dictionary<int, Item> itemsById,
        List<Bom> boms)
    {
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

        foreach (var itemId in itemsById.Keys)
            ComputeCost(itemId);

        return memo;
    }

    private async Task EnsureOrderTablesAsync()
    {
        await _context.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[Orders]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Orders](
                    [OrderID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [OrderDate] datetime2 NOT NULL,
                    [DueDate] datetime2 NOT NULL,
                    [Status] nvarchar(20) NOT NULL
                );
            END
            IF OBJECT_ID(N'[Orders]', N'U') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM sys.columns c
                   WHERE c.object_id = OBJECT_ID(N'[Orders]')
                     AND c.name = 'Status'
                     AND c.max_length > 40 -- nvarchar length in bytes
               )
            BEGIN
                ALTER TABLE [Orders] ALTER COLUMN [Status] nvarchar(20) NOT NULL;
            END
            IF OBJECT_ID(N'[OrderLines]', N'U') IS NULL
            BEGIN
                CREATE TABLE [OrderLines](
                    [OrderLineID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [OrderID] INT NOT NULL,
                    [ItemID] INT NOT NULL,
                    [Quantity] INT NOT NULL,
                    CONSTRAINT [FK_OrderLines_Orders_OrderID] FOREIGN KEY ([OrderID]) REFERENCES [Orders]([OrderID]) ON DELETE CASCADE,
                    CONSTRAINT [FK_OrderLines_Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [Items]([ItemID]) ON DELETE NO ACTION
                );
                CREATE INDEX [IX_OrderLines_OrderID] ON [OrderLines]([OrderID]);
                CREATE INDEX [IX_OrderLines_ItemID] ON [OrderLines]([ItemID]);
            END
            """);
    }

    private static OrderStatus? ParseStatus(string? raw)
    {
        if (string.Equals(raw, "open", StringComparison.OrdinalIgnoreCase)) return OrderStatus.Open;
        if (string.Equals(raw, "closed", StringComparison.OrdinalIgnoreCase)) return OrderStatus.Closed;
        return null;
    }

    private static (DateTime FromUtc, DateTime ToUtc)? TryParseDateFilter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            return null;

        var utc = parsed.ToUniversalTime();
        var granularity = raw.Contains(':') ? TimeSpan.FromMinutes(1) : TimeSpan.FromDays(1);
        return (utc, utc + granularity);
    }

    private static OrderDto ToDto(
        Order order,
        Dictionary<int, Item> itemsById,
        Dictionary<int, decimal?> computedCosts,
        Dictionary<int, List<OrderDeficitLineDto>> deficitByOrderId)
    {
        var deficit = deficitByOrderId.TryGetValue(order.OrderID, out var d)
            ? d
            : new List<OrderDeficitLineDto>();

        var totalCost = order.Lines.Sum(l =>
        {
            var item = itemsById.GetValueOrDefault(l.ItemID);
            var price = item?.SellingPrice ?? computedCosts.GetValueOrDefault(l.ItemID) ?? item?.UnitCost ?? 0m;
            return price * l.Quantity;
        });

        return new OrderDto
        {
            OrderId = order.OrderID,
            OrderDate = order.OrderDate,
            DueDate = order.DueDate,
            OrderType = order.Status == OrderStatus.Closed ? "closed" : "open",
            TotalCostRub = decimal.Round(totalCost, 2, MidpointRounding.AwayFromZero),
            Deficit = deficit,
            Items = order.Lines.Select(l => new OrderLineDto
            {
                ItemId = l.ItemID,
                Quantity = l.Quantity
            }).ToList()
        };
    }
}
