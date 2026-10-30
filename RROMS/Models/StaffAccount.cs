// Staff credentials are provisioned separately; public customer signup cannot create staff.
using System.ComponentModel.DataAnnotations;
namespace RROMS.Models;
public class StaffAccount
{
    [Key] public int StaffAccountID { get; set; }
    [Required, StringLength(100)] public string StaffName { get; set; } = "";
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [Required, StringLength(100)] public string NormalizedEmail { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsDemo { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public int SessionVersion { get; set; }
}
