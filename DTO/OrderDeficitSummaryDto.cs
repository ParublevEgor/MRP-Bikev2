using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class OrderDeficitSummaryDto
{
    [JsonPropertyName("asOf")]
    public DateTime AsOf { get; set; }

    [JsonPropertyName("lines")]
    public List<OrderDeficitLineDto> Lines { get; set; } = [];
}
