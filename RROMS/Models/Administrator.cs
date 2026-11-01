using System.ComponentModel.DataAnnotations;
namespace RROMS.Models;

// Independent administrator credentials. Passwords are stored only as salted hashes.
public class Administrator
{
    [Key] public int AdministratorID { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [Required, StringLength(100)] public string NormalizedEmail { get; set; } = "";
    [Required, StringLength(256)] public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsDemo { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public int SessionVersion { get; set; }
    [StringLength(64)] public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetExpiresUtc { get; set; }
    public DateTime? PasswordResetSentUtc { get; set; }
}

public interface IAdministratorManaged
{
    int? CreatedByAdministratorID { get; set; }
    int? UpdatedByAdministratorID { get; set; }
}
