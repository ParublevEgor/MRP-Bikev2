namespace MRP.Api.DTO;

public class BomDto
{
    public int BOMID { get; set; }
    public int ParentItemID { get; set; }
    public int ChildItemID { get; set; }
    public decimal Quantity { get; set; }
}