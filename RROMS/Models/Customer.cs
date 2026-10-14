// Customer table: stores the account identity and contact details used for reservations and confirmation emails. Real login is supplied by Sprint 1.
using System.ComponentModel.DataAnnotations;

namespace RROMS.Models
{
    public class Customer
    {
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

        public ICollection<Reservation> Reservations { get; set; } = [];
    }
}
