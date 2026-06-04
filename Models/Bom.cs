using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MRP.Api.Models;

// Таблица спецификации - это список рёбер.
// Для построения строки BOM группируются в словарь:
// var childrenByParent = boms
//     .GroupBy(x => x.ParentItemID)
//     .ToDictionary(g => g.Key, g => g.ToList());

public class Bom
{
    [Key]
    public int BOMID { get; set; }

    public int ParentItemID { get; set; } // ID позиции номенклатуры - родитель
    public int ChildItemID { get; set; } // ID позиции номенклатуры - компонент

    [Column(TypeName = "decimal(10,2)")]
    public decimal Quantity { get; set; } // Количество компонента на единицу родительской позиции

    public Item ParentItem { get; set; } = null!;
    public Item ChildItem { get; set; } = null!;
}