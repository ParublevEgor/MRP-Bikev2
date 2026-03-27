using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MRP.Api.Models;

public class Bom
{
    [Key]
    public int BOMID { get; set; }

    public int ParentItemID { get; set; }
    public int ChildItemID { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Quantity { get; set; }

    public Item ParentItem { get; set; } = null!;
    public Item ChildItem { get; set; } = null!;
}