using System.ComponentModel.DataAnnotations;

namespace RROMS.Models;

// Categories exist independently so the administrator can add them before adding dishes.
public class MenuCategory : IAdministratorManaged
{
        // Null for legacy/seeded records whose author is unknown.
        public int? CreatedByAdministratorID { get; set; }
        public Administrator? CreatedByAdministrator { get; set; }
        public int? UpdatedByAdministratorID { get; set; }
        public Administrator? UpdatedByAdministrator { get; set; }

    [Key]
    public int MenuCategoryID { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;
}
