namespace MRP.Api.DTO;

public class ItemDto
{
    public int ItemID { get; set; }
    public string? ItemCode { get; set; }
    public string ItemName { get; set; }
    public string ItemType { get; set; }

    public decimal? UnitCost { get; set; }
    public string? Unit { get; set; }
    public int? LeadTimeDays { get; set; }
}