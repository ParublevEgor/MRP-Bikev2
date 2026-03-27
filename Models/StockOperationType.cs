namespace MRP.Api.Models;

/// <summary>Тип складской операции по строке спецификации (BOM).</summary>
public enum StockOperationType
{
    /// <summary>Приход на склад (выпуск / оприходование).</summary>
    Receipt,
    /// <summary>Расход со склада (выдача в производство / отгрузка).</summary>
    Issue,
    /// <summary>Корректировка остатка.</summary>
    Adjustment
}
