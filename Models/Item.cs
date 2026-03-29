using System.ComponentModel.DataAnnotations;

namespace MRP.Api.Models;

public class Item
{
    [Key]
    public int ItemID { get; set; }

    [MaxLength(20)]
    public string? ItemCode { get; set; }

    [Required]
    [MaxLength(100)]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    //public string ItemType { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }

    public decimal? UnitCost { get; set; }

    public string? Unit { get; set; }
}