// Customer table: stores the account identity, contact details and sign-in credentials used for reservations, ordering and confirmation emails.
using System.ComponentModel.DataAnnotations;

namespace RROMS.Models
{
    public class Customer
    {
        public const string CustomerRole = "Customer";
        public const string StaffRole = "Restaurant Staff";
        public const string AdminRole = "Administrator";

        [Key]
        public int CustomerID
        {
            get; set;
        }

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        public string PhoneNo { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Address
        {
            get; set;
        }

        // Salted ASP.NET password hash; null for accounts that cannot sign in with a password (e.g. the seeded sample customer).
        [StringLength(256)]
        public string? PasswordHash
        {
            get; set;
        }

        // Customer-only discriminator, enforced by a database constraint. Internal accounts use separate tables.
        [StringLength(30)]
        public string Role { get; set; } = CustomerRole;

        public DateTime CreatedAt
        {
            get; set;
        }

        public int FailedLoginCount
        {
            get; set;
        }

        public DateTime? LockoutEnd
        {
            get; set;
        }

        // Nullable credentials preserve existing reservation-only customer records during upgrade.
        [StringLength(100)] public string? NormalizedEmail { get; set; }
        public bool EmailConfirmed { get; set; }
        [StringLength(64)] public string? VerificationTokenHash { get; set; }
        public DateTime? VerificationExpiresUtc { get; set; }
        public DateTime? VerificationSentUtc { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        [StringLength(64)] public string? PasswordResetTokenHash { get; set; }
        public DateTime? PasswordResetExpiresUtc { get; set; }
        public DateTime? PasswordResetSentUtc { get; set; }
        public int SessionVersion { get; set; }
        public bool IsDemo { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Reservation> Reservations { get; set; } = [];
    }
}
