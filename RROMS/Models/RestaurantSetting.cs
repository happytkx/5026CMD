
using System.ComponentModel.DataAnnotations;

namespace RROMS.Models
{
    public class RestaurantSetting : IAdministratorManaged
    {
        public int? CreatedByAdministratorID { get; set; }
        public Administrator? CreatedByAdministrator { get; set; }

        public int? UpdatedByAdministratorID { get; set; }
        public Administrator? UpdatedByAdministrator { get; set; }

        [Key]
        public int SettingID { get; set; }

        public TimeSpan OpeningTime { get; set; } = TimeSpan.FromHours(12);
        public TimeSpan ClosingTime { get; set; } = TimeSpan.FromHours(22);

        // US09: Administrator-configurable reservation settings.
        [Range(0, 10000)]
        public decimal DepositPerGuest { get; set; } = 50m;

        [Range(1, 12)]
        public int MinimumPartySize { get; set; } = 1;

        [Range(1, 12)]
        public int MaximumPartySize { get; set; } = 12;

        // Stored as a decimal: 0.06 means 6%.
        [Range(0, 1)]
        public decimal TaxRate { get; set; } = 0.06m;
    }
}
