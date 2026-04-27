using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class OrderDeficitLineDto
{
    [JsonPropertyName("itemId")]
    public int ItemId { get; set; }

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("requiredQty")]
    public decimal RequiredQty { get; set; }

    [JsonPropertyName("inStockQty")]
    public decimal InStockQty { get; set; }

    [JsonPropertyName("deficitQty")]
    public decimal DeficitQty { get; set; }
}
