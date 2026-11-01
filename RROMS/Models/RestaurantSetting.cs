// Restaurant opening and closing hours used when generating and validating reservation slots. Fixed coursework policies live in ReservationPolicy.
using System.ComponentModel.DataAnnotations;
namespace RROMS.Models;

public class RestaurantSetting : IAdministratorManaged
{
        // Null for legacy/seeded records whose author is unknown.
        public int? CreatedByAdministratorID { get; set; }
        public Administrator? CreatedByAdministrator { get; set; }
        public int? UpdatedByAdministratorID { get; set; }
        public Administrator? UpdatedByAdministrator { get; set; }

    [Key]
    public int SettingID
    {
        get; set;
    }
    public TimeSpan OpeningTime { get; set; } = TimeSpan.FromHours(12);
    public TimeSpan ClosingTime { get; set; } = TimeSpan.FromHours(22);
}
