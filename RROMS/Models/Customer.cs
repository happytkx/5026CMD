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

        // Customer, Restaurant Staff or Administrator. Issued as the role claim at sign-in.
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

        public ICollection<Reservation> Reservations { get; set; } = [];
    }
}
