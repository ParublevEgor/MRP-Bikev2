using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class ProductionStatsDto
{
    [JsonPropertyName("productItemId")]
    public int ProductItemId { get; set; }

    [JsonPropertyName("producedQty")]
    public decimal ProducedQty { get; set; }

    [JsonPropertyName("costPerBike")]
    public decimal CostPerBike { get; set; }

    [JsonPropertyName("totalMaterialCost")]
    public decimal TotalMaterialCost { get; set; }

    [JsonPropertyName("sellingPrice")]
    public decimal? SellingPrice { get; set; }

    [JsonPropertyName("totalRevenue")]
    public decimal? TotalRevenue { get; set; }

    [JsonPropertyName("totalProfit")]
    public decimal? TotalProfit { get; set; }
}
