// Dining-area table: identifies Indoor, Outdoor and VIP Room areas. RestaurantTable links each table or room to one area.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace RROMS.Models;

public class DiningArea : IAdministratorManaged
{
        // Null for legacy/seeded records whose author is unknown.
        public int? CreatedByAdministratorID { get; set; }
        public Administrator? CreatedByAdministrator { get; set; }
        public int? UpdatedByAdministratorID { get; set; }
        public Administrator? UpdatedByAdministrator { get; set; }

    [Key]
    public int DiningAreaID
    {
        get; set;
    }
    [Required] public string AreaType { get; set; } = string.Empty;
    // An optional named zone within the existing Indoor / Outdoor / VIP booking policies.
    [StringLength(80)] public string? DisplayName { get; set; }
    [NotMapped] public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName) ? AreaType : DisplayName;
    public ICollection<RestaurantTable> RestaurantTables { get; set; } = [];
}
