namespace MRP.Api.DTO;

public class StockOperationDto
{
    public int StockOperationID { get; set; }
    public int SpecificationId { get; set; }
    public DateTime Date { get; set; }
    public decimal Quantity { get; set; }
    public string OperationType { get; set; } = string.Empty;
}
