using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class StockOperationDto
{
    [JsonPropertyName("stockOperationId")]
    public int StockOperationID { get; set; }

    [JsonPropertyName("specificationId")]
    public int SpecificationId { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("operationType")]
    public string OperationType { get; set; } = string.Empty;
}
