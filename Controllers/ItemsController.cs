using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MRP.Api.Data;
using MRP.Api.DTO;
using MRP.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MRP.Api.Controllers;

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
        var items = await _context.Items
            .Select(i => new ItemDto
            {
                ItemID = i.ItemID,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                ItemType = i.ItemType.ToString(),
                Unit = i.Unit,
                UnitCost = i.UnitCost,
                SellingPrice = i.SellingPrice
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ItemDto>> GetById(int id)
    {
        var dto = await _context.Items
            .Where(i => i.ItemID == id)
            .Select(i => new ItemDto
            {
                ItemID = i.ItemID,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                ItemType = i.ItemType.ToString(),
                Unit = i.Unit,
                UnitCost = i.UnitCost,
                SellingPrice = i.SellingPrice
            })
            .FirstOrDefaultAsync();

        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpGet("stock-balance")]
    public async Task<ActionResult<IEnumerable<ItemStockBalanceDto>>> GetStockBalance()
    {
        var raw = await _context.StockOperations
            .Join(
                _context.Boms,
                s => s.SpecificationId,
                b => b.BOMID,
                (s, b) => new { b.ChildItemID, s.OperationType, s.Quantity })
            .GroupBy(x => x.ChildItemID)
            .Select(g => new
            {
                ItemId = g.Key,
                ReceiptQty = g.Where(x => x.OperationType == StockOperationType.Receipt).Sum(x => (decimal?)x.Quantity) ?? 0m,
                IssueQty = g.Where(x => x.OperationType == StockOperationType.Issue).Sum(x => (decimal?)x.Quantity) ?? 0m,
                AdjustmentQty = g.Where(x => x.OperationType == StockOperationType.Adjustment).Sum(x => (decimal?)x.Quantity) ?? 0m
            })
            .ToListAsync();

        var byItem = raw.ToDictionary(
            x => x.ItemId,
            x => new { x.ReceiptQty, x.IssueQty, x.AdjustmentQty });

        var result = await _context.Items
            .OrderBy(i => i.ItemID)
            .Select(i => new ItemStockBalanceDto
            {
                ItemID = i.ItemID,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                Unit = i.Unit
            })
            .ToListAsync();

        foreach (var item in result)
        {
            if (!byItem.TryGetValue(item.ItemID, out var agg))
                continue;

            item.ReceiptQty = agg.ReceiptQty;
            item.IssueQty = agg.IssueQty;
            item.AdjustmentQty = agg.AdjustmentQty;
            item.CurrentStock = agg.ReceiptQty - agg.IssueQty + agg.AdjustmentQty;
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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
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
        return null;
    }

    private static ItemDto ToDto(Item i) => new()
    {
        ItemID = i.ItemID,
        ItemCode = i.ItemCode,
        ItemName = i.ItemName,
        ItemType = i.ItemType.ToString(),
        Unit = i.Unit,
        UnitCost = i.UnitCost,
        SellingPrice = i.SellingPrice
    };
}
