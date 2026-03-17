using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MRP.Api.Data;
using MRP.Api.DTO;
using MRP.Api.Models;
using System;
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

        return Ok(item);
    }

    [HttpPut("{id}")]
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

        return Ok(item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.Items.FindAsync(id);
        if (item == null) return NotFound();

        var used = await _context.Boms
            .AnyAsync(b => b.ParentItemID == id || b.ChildItemID == id);

        if (used)
            return BadRequest("Нельзя удалить: Item используется в BOM");

        _context.Items.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}