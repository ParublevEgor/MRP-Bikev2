using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class OrderDto
{
    [JsonPropertyName("orderId")]
    public int OrderId { get; set; }

    [JsonPropertyName("orderDate")]
    public DateTime OrderDate { get; set; }

    [JsonPropertyName("dueDate")]
    public DateTime DueDate { get; set; }

    [JsonPropertyName("orderType")]
    public string OrderType { get; set; } = "open";

    [JsonPropertyName("totalCostRub")]
    public decimal TotalCostRub { get; set; }

    [JsonPropertyName("deficit")]
    public List<OrderDeficitLineDto> Deficit { get; set; } = [];

    [JsonPropertyName("items")]
    public List<OrderLineDto> Items { get; set; } = [];
}
