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
public class BomsController : ControllerBase
{
    private readonly BikeContext _context;

    public BomsController(BikeContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BomDto>>> Get()
    {
        var boms = await _context.Boms
            .Select(b => new BomDto
            {
                BOMID = b.BOMID,
                ParentItemID = b.ParentItemID,
                ChildItemID = b.ChildItemID,
                Quantity = b.Quantity
            })
            .ToListAsync();

        return Ok(boms);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BomDto>> GetById(int id)
    {
        var dto = await _context.Boms
            .Where(b => b.BOMID == id)
            .Select(b => new BomDto
            {
                BOMID = b.BOMID,
                ParentItemID = b.ParentItemID,
                ChildItemID = b.ChildItemID,
                Quantity = b.Quantity
            })
            .FirstOrDefaultAsync();

        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create(BomDto dto)
    {
        var err = await ValidateBomAsync(dto, excludeBomId: null);
        if (err != null) return BadRequest(err);

        var bom = new Bom
        {
            ParentItemID = dto.ParentItemID,
            ChildItemID = dto.ChildItemID,
            Quantity = dto.Quantity
        };

        _context.Boms.Add(bom);
        await _context.SaveChangesAsync();

        return Ok(ToDto(bom));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, BomDto dto)
    {
        var bom = await _context.Boms.FindAsync(id);
        if (bom == null) return NotFound();

        var err = await ValidateBomAsync(dto, excludeBomId: id);
        if (err != null) return BadRequest(err);

        bom.ParentItemID = dto.ParentItemID;
        bom.ChildItemID = dto.ChildItemID;
        bom.Quantity = dto.Quantity;

        await _context.SaveChangesAsync();

        return Ok(ToDto(bom));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _context.StockOperations
            .Where(s => s.SpecificationId == id)
            .ExecuteDeleteAsync();

        var deleted = await _context.Boms.Where(b => b.BOMID == id).ExecuteDeleteAsync();
        return deleted == 0 ? NotFound() : NoContent();
    }

    private async Task<string?> ValidateBomAsync(BomDto dto, int? excludeBomId)
    {
        if (dto.ParentItemID == dto.ChildItemID)
            return "ParentItemID and ChildItemID must differ.";

        if (dto.Quantity <= 0)
            return "Quantity must be greater than zero.";

        var parentOk = await _context.Items.AnyAsync(i => i.ItemID == dto.ParentItemID);
        var childOk = await _context.Items.AnyAsync(i => i.ItemID == dto.ChildItemID);
        if (!parentOk || !childOk)
            return "ParentItemID or ChildItemID does not exist.";

        var duplicate = await _context.Boms
            .AnyAsync(b =>
                b.ParentItemID == dto.ParentItemID &&
                b.ChildItemID == dto.ChildItemID &&
                (!excludeBomId.HasValue || b.BOMID != excludeBomId.Value));

        if (duplicate)
            return "A BOM row for this parent/child pair already exists.";

        return null;
    }

    private static BomDto ToDto(Bom b) => new()
    {
        BOMID = b.BOMID,
        ParentItemID = b.ParentItemID,
        ChildItemID = b.ChildItemID,
        Quantity = b.Quantity
    };
}
