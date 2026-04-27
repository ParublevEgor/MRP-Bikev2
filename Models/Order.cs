using System.ComponentModel.DataAnnotations;

namespace MRP.Api.Models;

public class Order
{
    [Key]
    public int OrderID { get; set; }

    public DateTime OrderDate { get; set; }

    public DateTime DueDate { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Open;

    public List<OrderLine> Lines { get; set; } = [];
}
