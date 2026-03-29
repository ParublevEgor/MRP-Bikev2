using System.Text.Json.Serialization;

namespace MRP.Api.DTO;

public class BomDto
{
    [JsonPropertyName("bomId")]
    public int BOMID { get; set; }

    [JsonPropertyName("parentItemId")]
    public int ParentItemID { get; set; }

    [JsonPropertyName("childItemId")]
    public int ChildItemID { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }
}
