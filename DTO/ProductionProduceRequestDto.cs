using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class ProductionProduceRequestDto
{
    [JsonPropertyName("productItemId")]
    public int ProductItemId { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}
