using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MRP.Api.Data;
using MRP.Api.DTO;
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

    [HttpPost]
    public async Task<IActionResult> Create(BomDto dto)
    {
        var bom = new Models.Bom
        {
            ParentItemID = dto.ParentItemID,
            ChildItemID = dto.ChildItemID,
            Quantity = dto.Quantity
        };

        _context.Boms.Add(bom);
        await _context.SaveChangesAsync();

        return Ok(bom);
    }
}