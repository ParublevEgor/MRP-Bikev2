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
        var bom = await _context.Boms
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BOMID == id);
        if (bom == null) return NotFound();

        var stock = await _context.StockOperations
            .Where(s => s.SpecificationId == id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ReceiptQty = g.Where(x => x.OperationType == StockOperationType.Receipt).Sum(x => (decimal?)x.Quantity) ?? 0m,
                IssueQty = g.Where(x => x.OperationType == StockOperationType.Issue).Sum(x => (decimal?)x.Quantity) ?? 0m
            })
            .FirstOrDefaultAsync();

        var currentStock = stock == null ? 0m : stock.ReceiptQty - stock.IssueQty;
        if (currentStock > 0)
            return BadRequest("Нельзя удалить ветку BOM: по компоненту есть остатки.");

        await _context.StockOperations
            .Where(s => s.SpecificationId == id)
            .ExecuteDeleteAsync();

        var deleted = await _context.Boms.Where(b => b.BOMID == id).ExecuteDeleteAsync();
        return deleted == 0 ? NotFound() : NoContent();
    }

    private async Task<string?> ValidateBomAsync(BomDto dto, int? excludeBomId)
    {
        if (dto.ParentItemID == dto.ChildItemID)
            return "Родитель и компонент должны быть разными позициями.";

        if (dto.Quantity <= 0)
            return "Количество должно быть больше нуля.";
        if (dto.Quantity != decimal.Truncate(dto.Quantity))
            return "Количество должно быть целым числом.";

        var parentOk = await _context.Items.AnyAsync(i => i.ItemID == dto.ParentItemID);
        var childOk = await _context.Items.AnyAsync(i => i.ItemID == dto.ChildItemID);
        if (!parentOk || !childOk)
            return "Указан несуществующий родитель или компонент (проверьте ID).";

        var duplicate = await _context.Boms
            .AnyAsync(b =>
                b.ParentItemID == dto.ParentItemID &&
                b.ChildItemID == dto.ChildItemID &&
                (!excludeBomId.HasValue || b.BOMID != excludeBomId.Value));

        if (duplicate)
            return "Такая пара «родитель → компонент» уже есть в спецификации.";

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
