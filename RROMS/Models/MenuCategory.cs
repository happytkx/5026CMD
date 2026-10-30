using System.ComponentModel.DataAnnotations;

namespace RROMS.Models;

// Categories exist independently so the administrator can add them before adding dishes.
public class MenuCategory
{
    [Key]
    public int MenuCategoryID { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;
}
