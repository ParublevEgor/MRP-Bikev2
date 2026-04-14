using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MRP.Api.Models;

public class StockOperation
{
    [Key]
    public int StockOperationID { get; set; }

    public int SpecificationId { get; set; }

    public DateTime Date { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    public StockOperationType OperationType { get; set; }

    public Bom Specification { get; set; } = null!;
}
