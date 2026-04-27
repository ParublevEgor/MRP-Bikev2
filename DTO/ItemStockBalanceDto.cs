using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class ItemStockBalanceDto
{
    [JsonPropertyName("itemId")]
    public int ItemID { get; set; }

    [JsonPropertyName("itemCode")]
    public string? ItemCode { get; set; }

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("receiptQty")]
    public decimal ReceiptQty { get; set; }

    [JsonPropertyName("issueQty")]
    public decimal IssueQty { get; set; }

    [JsonPropertyName("orderQty")]
    public decimal OrderQty { get; set; }

    [JsonPropertyName("adjustmentQty")]
    public decimal AdjustmentQty { get; set; }

    [JsonPropertyName("currentStock")]
    public decimal CurrentStock { get; set; }
}
