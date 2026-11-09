
using System;
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

        // Restaurant operating hours
        public TimeSpan OpeningTime { get; set; } =
            TimeSpan.FromHours(12);

        public TimeSpan ClosingTime { get; set; } =
            TimeSpan.FromHours(22);

        [Range(1, 120)]
        public int SlotIntervalMinutes { get; set; } = 30;

        // US09: Reservation deposit configuration
        [Range(0, 10000)]
        public decimal DepositPerGuest { get; set; } = 50m;

        // US09: Reservation party size limits
        [Range(1, 12)]
        public int MinimumPartySize { get; set; } = 1;

        [Range(1, 12)]
        public int MaximumPartySize { get; set; } = 12;

        // US09: Minimum spending configuration

        // Indoor and outdoor dining: amount per guest
        [Range(0, 100000)]
        public decimal StandardMinimumSpendPerGuest { get; set; } = 50m;

        // VIP room: 6 to 8 guests
        [Range(0, 100000)]
        public decimal VipMinimumSpendSmallGroup { get; set; } = 2400m;

        // VIP room: 9 to 12 guests
        [Range(0, 100000)]
        public decimal VipMinimumSpendLargeGroup { get; set; } = 3600m;

        // US09: Tax rate stored as a decimal fraction
        // Example: 0.06 represents 6%
        [Range(0, 1)]
        public decimal TaxRate { get; set; } = 0.06m;
    }
}
