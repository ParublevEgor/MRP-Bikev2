using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class ItemDto
{
    [JsonPropertyName("itemId")]
    public int ItemID { get; set; }

    [JsonPropertyName("itemCode")]
    public string? ItemCode { get; set; }

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("itemType")]
    public string ItemType { get; set; } = string.Empty;

    [JsonPropertyName("unitCost")]
    public decimal? UnitCost { get; set; }

    [JsonPropertyName("sellingPrice")]
    public decimal? SellingPrice { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}
