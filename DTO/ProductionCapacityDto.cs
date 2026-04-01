using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class ProductionCapacityDto
{
    [JsonPropertyName("productItemId")]
    public int ProductItemId { get; set; }

    [JsonPropertyName("maxQty")]
    public int MaxQty { get; set; }

    [JsonPropertyName("limitingItemId")]
    public int? LimitingItemId { get; set; }

    [JsonPropertyName("lines")]
    public List<ProductionCapacityLineDto> Lines { get; set; } = [];
}

public class ProductionCapacityLineDto
{
    [JsonPropertyName("bomId")]
    public int BomId { get; set; }

    [JsonPropertyName("childItemId")]
    public int ChildItemId { get; set; }

    [JsonPropertyName("childName")]
    public string ChildName { get; set; } = string.Empty;

    [JsonPropertyName("qtyPerUnit")]
    public decimal QtyPerUnit { get; set; }

    [JsonPropertyName("currentStock")]
    public decimal CurrentStock { get; set; }

    [JsonPropertyName("maxFromThisLine")]
    public int MaxFromThisLine { get; set; }
}
