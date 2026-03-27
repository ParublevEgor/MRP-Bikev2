namespace MRP.Api.DTO;

public class ItemDto
{
    public int ItemID { get; set; }
    public string? ItemCode { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;

    public decimal? UnitCost { get; set; }
    public string? Unit { get; set; }
    public int? LeadTimeDays { get; set; }
}