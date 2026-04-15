using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MRP.Api.Data;
using MRP.Api.DTO;
using MRP.Api.Models;
using System;
using System.Threading.Tasks;

namespace MRP.Api.Controllers;

//Контроллер Склада

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
            .OrderBy(s => s.OperationType == StockOperationType.Receipt ? 0 : 1)
            .ThenByDescending(s => s.Date)
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
            return BadRequest("Неверный тип операции. Допустимо: Receipt, Issue.");

        if (dto.Quantity <= 0)
            return BadRequest("Количество должно быть больше нуля.");
        if (dto.Quantity != decimal.Truncate(dto.Quantity))
            return BadRequest("Количество должно быть целым числом.");

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
            Date = TrimSeconds(dto.Date),
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
            return BadRequest("Неверный тип операции. Допустимо: Receipt, Issue.");

        if (dto.Quantity <= 0)
            return BadRequest("Количество должно быть больше нуля.");
        if (dto.Quantity != decimal.Truncate(dto.Quantity))
            return BadRequest("Количество должно быть целым числом.");

        if (dto.SpecificationId <= 0)
            return BadRequest("Укажите корректный ID строки спецификации (BOM).");

        var err = ValidateDate(dto.Date);
        if (err != null) return BadRequest(err);

        var entity = await _context.StockOperations.FindAsync(id);
        if (entity == null) return NotFound();
        if (!await CanMutateOperationAsync(entity))
            return BadRequest("Операцию нельзя изменить: по этой позиции уже зафиксирован расход.");

        var bomExists = await _context.Boms.AnyAsync(b => b.BOMID == dto.SpecificationId);
        if (!bomExists)
            return BadRequest("Строка спецификации с таким ID не найдена.");

        entity.SpecificationId = dto.SpecificationId;
        entity.Date = TrimSeconds(dto.Date);
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
        if (!await CanMutateOperationAsync(entity))
            return BadRequest("Операцию нельзя удалить: по этой позиции уже зафиксирован расход.");

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

    private async Task<bool> CanMutateOperationAsync(StockOperation entity)
    {
        if (entity.OperationType != StockOperationType.Receipt)
            return true;

        var childItemId = await _context.Boms
            .Where(b => b.BOMID == entity.SpecificationId)
            .Select(b => (int?)b.ChildItemID)
            .FirstOrDefaultAsync();

        if (childItemId == null)
            return true;

        var issueExists = await _context.StockOperations
            .Join(
                _context.Boms,
                s => s.SpecificationId,
                b => b.BOMID,
                (s, b) => new { Operation = s, b.ChildItemID })
            .AnyAsync(x =>
                x.ChildItemID == childItemId.Value &&
                x.Operation.OperationType == StockOperationType.Issue &&
                x.Operation.Date >= entity.Date &&
                x.Operation.StockOperationID != entity.StockOperationID);

        return !issueExists;
    }

    private static DateTime TrimSeconds(DateTime date) =>
        new(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0, date.Kind);

    private static StockOperationDto ToDto(StockOperation s) => new()
    {
        StockOperationID = s.StockOperationID,
        SpecificationId = s.SpecificationId,
        Date = s.Date,
        Quantity = s.Quantity,
        OperationType = s.OperationType.ToString()
    };
}
