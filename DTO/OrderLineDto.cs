using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class OrderLineDto
{
    [JsonPropertyName("itemId")]
    public int ItemId { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}
