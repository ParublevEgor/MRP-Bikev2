using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MRP.Api.Data;
using MRP.Api.DTO;
using MRP.Api.Models;
using System;
using System.Threading.Tasks;

namespace MRP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockOperationsController : ControllerBase
{
    private readonly BikeContext _context;

    public StockOperationsController(BikeContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StockOperationDto>>> Get()
    {
        var list = await _context.StockOperations
            .Select(s => new StockOperationDto
            {
                StockOperationID = s.StockOperationID,
                SpecificationId = s.SpecificationId,
                Date = s.Date,
                Quantity = s.Quantity,
                OperationType = s.OperationType.ToString()
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StockOperationDto>> GetById(int id)
    {
        var dto = await _context.StockOperations
            .Where(s => s.StockOperationID == id)
            .Select(s => new StockOperationDto
            {
                StockOperationID = s.StockOperationID,
                SpecificationId = s.SpecificationId,
                Date = s.Date,
                Quantity = s.Quantity,
                OperationType = s.OperationType.ToString()
            })
            .FirstOrDefaultAsync();

        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create(StockOperationDto dto)
    {
        if (!Enum.TryParse<StockOperationType>(dto.OperationType, true, out var opType))
            return BadRequest("Неверный тип операции. Допустимо: Receipt, Issue, Adjustment.");

        if (dto.Quantity <= 0)
            return BadRequest("Количество должно быть больше нуля.");

        if (dto.SpecificationId <= 0)
            return BadRequest("Укажите корректный ID строки спецификации (BOM).");

        var err = ValidateDate(dto.Date);
        if (err != null) return BadRequest(err);

        var bomExists = await _context.Boms.AnyAsync(b => b.BOMID == dto.SpecificationId);
        if (!bomExists)
            return BadRequest("Строка спецификации с таким ID не найдена.");

        var entity = new StockOperation
        {
            SpecificationId = dto.SpecificationId,
            Date = dto.Date,
            Quantity = dto.Quantity,
            OperationType = opType
        };

        _context.StockOperations.Add(entity);
        await _context.SaveChangesAsync();

        return Ok(ToDto(entity));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, StockOperationDto dto)
    {
        if (!Enum.TryParse<StockOperationType>(dto.OperationType, true, out var opType))
            return BadRequest("Неверный тип операции. Допустимо: Receipt, Issue, Adjustment.");

        if (dto.Quantity <= 0)
            return BadRequest("Количество должно быть больше нуля.");

        if (dto.SpecificationId <= 0)
            return BadRequest("Укажите корректный ID строки спецификации (BOM).");

        var err = ValidateDate(dto.Date);
        if (err != null) return BadRequest(err);

        var entity = await _context.StockOperations.FindAsync(id);
        if (entity == null) return NotFound();

        var bomExists = await _context.Boms.AnyAsync(b => b.BOMID == dto.SpecificationId);
        if (!bomExists)
            return BadRequest("Строка спецификации с таким ID не найдена.");

        entity.SpecificationId = dto.SpecificationId;
        entity.Date = dto.Date;
        entity.Quantity = dto.Quantity;
        entity.OperationType = opType;

        await _context.SaveChangesAsync();

        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.StockOperations.FindAsync(id);
        if (entity == null) return NotFound();

        _context.StockOperations.Remove(entity);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static string? ValidateDate(DateTime date)
    {
        if (date == default)
            return "Укажите дату и время операции.";
        return null;
    }

    private static StockOperationDto ToDto(StockOperation s) => new()
    {
        StockOperationID = s.StockOperationID,
        SpecificationId = s.SpecificationId,
        Date = s.Date,
        Quantity = s.Quantity,
        OperationType = s.OperationType.ToString()
    };
}
