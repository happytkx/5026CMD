// Restaurant operating hours and editable reservation rules.
// Defaults preserve Céleste's established policy for existing databases.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RROMS.Models;

public class RestaurantSetting : IAdministratorManaged
{
    [Key] public int SettingID { get; set; }
    public TimeSpan OpeningTime { get; set; } = TimeSpan.FromHours(12);
    public TimeSpan ClosingTime { get; set; } = TimeSpan.FromHours(22);
    public int SlotIntervalMinutes { get; set; } = 30;

    [Column(TypeName = "decimal(18,2)")] public decimal DepositPerGuest { get; set; } = 50m;
    [Column(TypeName = "decimal(18,2)")] public decimal StandardMinimumSpendPerGuest { get; set; } = 50m;
    [Column(TypeName = "decimal(18,2)")] public decimal VipEightSeatMinimumSpend { get; set; } = 2400m;
    [Column(TypeName = "decimal(18,2)")] public decimal VipTwelveSeatMinimumSpend { get; set; } = 3600m;
    public int RescheduleCutoffHours { get; set; } = 168;
    public int MaximumReschedules { get; set; } = 1;
    public int ArrivalGraceMinutes { get; set; } = 10;
    public int ExtendedArrivalGraceMinutes { get; set; } = 30;
    public int StandardDiningMinutes { get; set; } = 120;
    public int StandardAdvanceHours { get; set; } = 2;
    public int VipAdvanceHours { get; set; } = 24;
    public int PaymentHoldMinutes { get; set; } = 10;
    [StringLength(1000)] public string? GeneralGuidelines { get; set; }
    public DateTime? PoliciesUpdatedAt { get; set; }

    public int? CreatedByAdministratorID { get; set; }
    public Administrator? CreatedByAdministrator { get; set; }
    public int? UpdatedByAdministratorID { get; set; }
    public Administrator? UpdatedByAdministrator { get; set; }
}
