using System.ComponentModel.DataAnnotations;

namespace MRP.Api.Models;

public class OrderLine
{
    [Key]
    public int OrderLineID { get; set; }

    public int OrderID { get; set; }

    public int ItemID { get; set; }

    public int Quantity { get; set; }

    public Order Order { get; set; } = null!;

    public Item Item { get; set; } = null!;
}
