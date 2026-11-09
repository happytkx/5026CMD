using System.ComponentModel.DataAnnotations;

namespace RROMS.Models;

// Operational alerts visible to staff; administrator-only alerts use the Administrator audience.
public class StaffNotification
{
    public const string StaffAudience = "Staff";
    public const string AdministratorAudience = "Administrator";

    [Key] public int StaffNotificationID { get; set; }
    [Required, StringLength(100)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(700)] public string Message { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Type { get; set; } = "Operations";
    [Required, StringLength(30)] public string Audience { get; set; } = StaffAudience;
    [Required, StringLength(240)] public string TargetUrl { get; set; } = "/Staff/Index";
    public DateTime CreatedAt { get; set; }
    public ICollection<StaffNotificationRead> Reads { get; set; } = [];
}

public class StaffNotificationRead
{
    [Key] public int StaffNotificationReadID { get; set; }
    public int StaffNotificationID { get; set; }
    // Identifies either a legacy staff/customer account (numeric) or a new staff account (staff:ID).
    [Required, StringLength(100)] public string RecipientKey { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public StaffNotification Notification { get; set; } = null!;
}
