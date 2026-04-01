using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class ReceiptMaterialsRequestDto
{
    /// <summary>Если 0 или не указан — создаётся/используется изделие с кодом BIKE.</summary>
    [JsonPropertyName("productItemId")]
    public int? ProductItemId { get; set; }

    [JsonPropertyName("bikeCount")]
    public int BikeCount { get; set; } = 10;
}
