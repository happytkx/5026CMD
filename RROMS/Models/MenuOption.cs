using System.ComponentModel.DataAnnotations;

namespace RROMS.Models;

// Administrator-managed choices for protein types, dietary tags and allergens.
public class MenuOption
{
    [Key]
    public int MenuOptionID { get; set; }

    [Required, StringLength(20)]
    public string Kind { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;
}
