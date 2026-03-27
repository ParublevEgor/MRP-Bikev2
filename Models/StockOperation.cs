using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MRP.Api.Models;

/// <summary>Складская операция, привязанная к строке спецификации (BOM).</summary>
public class StockOperation
{
    [Key]
    public int StockOperationID { get; set; }

    /// <summary>Идентификатор строки спецификации (<see cref="Bom.BOMID"/>).</summary>
    public int SpecificationId { get; set; }

    public DateTime Date { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    public StockOperationType OperationType { get; set; }

    public Bom Specification { get; set; } = null!;
}
