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
                LeadTimeDays = i.LeadTimeDays
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
                LeadTimeDays = i.LeadTimeDays
            })
            .FirstOrDefaultAsync();

        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ItemDto dto)
    {
        if (!Enum.TryParse<ItemType>(dto.ItemType, true, out var itemType))
            return BadRequest("Invalid ItemType");

        var item = new Item
        {
            ItemCode = dto.ItemCode,
            ItemName = dto.ItemName,
            ItemType = itemType,

            Unit = dto.Unit,
            UnitCost = dto.UnitCost,
            LeadTimeDays = dto.LeadTimeDays
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        return Ok(ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ItemDto dto)
    {
        var item = await _context.Items.FindAsync(id);
        if (item == null) return NotFound();

        if (!Enum.TryParse<ItemType>(dto.ItemType, true, out var itemType))
            return BadRequest("Invalid ItemType");

        item.ItemCode = dto.ItemCode;
        item.ItemName = dto.ItemName;
        item.ItemType = itemType;

        item.Unit = dto.Unit;
        item.UnitCost = dto.UnitCost;
        item.LeadTimeDays = dto.LeadTimeDays;

        await _context.SaveChangesAsync();

        return Ok(ToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        // В БД у Boms два FK на Items с NO ACTION — SQL Server не делает каскад сам, а ClientCascade
        // при Remove(item) не всегда гарантирует порядок DELETE. Удаляем явно: склад → BOM → номенклатура.
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

    private static ItemDto ToDto(Item i) => new()
    {
        ItemID = i.ItemID,
        ItemCode = i.ItemCode,
        ItemName = i.ItemName,
        ItemType = i.ItemType.ToString(),
        Unit = i.Unit,
        UnitCost = i.UnitCost,
        LeadTimeDays = i.LeadTimeDays
    };
}
