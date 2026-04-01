using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class ProductionProduceRequestDto
{
    [JsonPropertyName("productItemId")]
    public int ProductItemId { get; set; }

    /// <summary>Целое число готовых изделий.</summary>
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}
