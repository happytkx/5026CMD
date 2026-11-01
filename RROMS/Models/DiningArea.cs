// Dining-area table: identifies Indoor, Outdoor and VIP Room areas. RestaurantTable links each table or room to one area.
using System.ComponentModel.DataAnnotations;
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
    public ICollection<RestaurantTable> RestaurantTables { get; set; } = [];
}
