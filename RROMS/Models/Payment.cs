// Deposit payment attempts and receipts, including failed simulations. Actual dining-bill payment belongs to Sprint 4.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RROMS.Models
{
    public class Payment
    {
        [Key]
        public int PaymentID
        {
            get; set;
        }

        [ForeignKey("Reservation")]
        public int ReservationID
        {
            get; set;
        }

        public string PaymentType { get; set; } = string.Empty;
        [StringLength(40)]
        public string PaymentReference { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "Simulation";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount
        {
            get; set;
        }

        public string PaymentStatus { get; set; } = string.Empty;

        public DateTime PaymentDate
        {
            get; set;
        }

        public Reservation Reservation { get; set; } = null!;
    }
}
