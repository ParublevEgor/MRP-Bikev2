using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using MRP.Api.Data;

using MRP.Api.DTO;

using MRP.Api.Models;

using System;

using System.Threading.Tasks;



namespace MRP.Api.Controllers;



// Склад. Движение по позиции номенклатуры (ItemId).



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

        var systemIds = await StockAccounting.GetSystemItemIdsAsync(_context);
        var list = await StockAccounting
            .ExcludeSystemItems(_context.StockOperations.AsNoTracking(), systemIds)
            .OrderBy(s => s.OperationType == StockOperationType.Receipt ? 0 : 1)
            .ThenByDescending(s => s.Date)
            .Select(s => new StockOperationDto

            {

                StockOperationID = s.StockOperationID,

                SpecificationId = s.SpecificationId,

                ItemId = s.ItemId,

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

                ItemId = s.ItemId,

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



        var itemId = await ResolveItemIdAsync(dto);

        if (itemId <= 0)

            return BadRequest("Укажите позицию номенклатуры.");



        var err = ValidateDate(dto.Date);

        if (err != null) return BadRequest(err);



        var entity = new StockOperation

        {

            Date = TrimSeconds(dto.Date),

            Quantity = dto.Quantity,

            OperationType = opType

        };



        try

        {

            await StockAccounting.ConfigureManualOperationAsync(_context, entity, itemId);

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(ex.Message);

        }



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



        var itemId = await ResolveItemIdAsync(dto);

        if (itemId <= 0)

            return BadRequest("Укажите позицию номенклатуры.");



        var err = ValidateDate(dto.Date);

        if (err != null) return BadRequest(err);



        var entity = await _context.StockOperations.FindAsync(id);

        if (entity == null) return NotFound();

        if (!await CanMutateOperationAsync(entity))

            return BadRequest("Операцию нельзя изменить: по этой позиции уже зафиксирован расход.");



        entity.Date = TrimSeconds(dto.Date);

        entity.Quantity = dto.Quantity;

        entity.OperationType = opType;



        try

        {

            await StockAccounting.ConfigureManualOperationAsync(_context, entity, itemId);

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(ex.Message);

        }



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



    private async Task<int> ResolveItemIdAsync(StockOperationDto dto)

    {

        if (dto.ItemId > 0)

            return dto.ItemId;



        if (dto.SpecificationId <= 0)

            return 0;



        var fromBom = await _context.Boms

            .Where(b => b.BOMID == dto.SpecificationId)

            .Select(b => (int?)b.ChildItemID)

            .FirstOrDefaultAsync();



        return fromBom ?? 0;

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



        if (entity.ItemId <= 0)

            return true;



        var issueExists = await _context.StockOperations.AnyAsync(s =>

            s.ItemId == entity.ItemId &&

            s.OperationType == StockOperationType.Issue &&

            s.Date >= entity.Date &&

            s.StockOperationID != entity.StockOperationID);



        return !issueExists;

    }



    private static DateTime TrimSeconds(DateTime date) =>

        new(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0, date.Kind);



    private static StockOperationDto ToDto(StockOperation s) => new()

    {

        StockOperationID = s.StockOperationID,

        SpecificationId = s.SpecificationId,

        ItemId = s.ItemId,

        Date = s.Date,

        Quantity = s.Quantity,

        OperationType = s.OperationType.ToString()

    };

}

