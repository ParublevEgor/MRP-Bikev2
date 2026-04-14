using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class ReceiptMaterialsRequestDto
{
    [JsonPropertyName("productItemId")]
    public int? ProductItemId { get; set; }

    [JsonPropertyName("bikeCount")]
    public int BikeCount { get; set; } = 10;
}
