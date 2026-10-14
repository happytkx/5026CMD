// Main booking record: connects customer and assigned table, tracks dining times, deposit, requests and lifecycle status, and preserves the original modification deadline.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RROMS.Models
{
    public class Reservation
    {
        [Key]
        public int ReservationID
        {
            get; set;
        }

        [ForeignKey("Customer")]
        public int CustomerID
        {
            get; set;
        }

        [ForeignKey("RestaurantTable")]
        public string TableID { get; set; } = string.Empty;

        [Required]
        public string BookingReference { get; set; } = string.Empty;

        [Required]
        public DateTime ReservationDate
        {
            get; set;
        }

        [Required]
        public TimeSpan StartTime
        {
            get; set;
        }

        [Required]
        public TimeSpan EndTime
        {
            get; set;
        }

        [Required]
        public int PartySize
        {
            get; set;
        }

        [Required]
        public string PreferredDiningOption { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositAmount
        {
            get; set;
        }

        public string DepositStatus { get; set; } = string.Empty;

        public string ReservationStatus { get; set; } = string.Empty;

        public int ModificationCount
        {
            get; set;
        }

        // Kept unchanged after modification so the seven-day deadline cannot be reset.
        public DateTime OriginalStart
        {
            get; set;
        }
        public DateTime CreatedDate
        {
            get; set;
        }
        public DateTime? CheckedInAt
        {
            get; set;
        }
        public DateTime? CancellationDate
        {
            get; set;
        }
        public bool LateArrivalNotified
        {
            get; set;
        }
        public DateTime? HoldExpiresAt
        {
            get; set;
        }
        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumSpend
        {
            get; set;
        }
        // SQL Server changes this token on each update to detect concurrent edits.
        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
        // Derived value for Sprint 3 integration; this is not a database column.
        [NotMapped]
        public bool OrderingEnabled => ReservationStatus == "Seated";

        // Guest Details combines preferences, allergies and special requests into this one stored field.
        public string? DietaryRequest
        {
            get; set;
        }

        public Customer Customer { get; set; } = null!;

        public RestaurantTable RestaurantTable { get; set; } = null!;

        public ICollection<Payment> Payments { get; set; } = [];

        public ICollection<ReservationHistory> ReservationHistories { get; set; } = [];
    }
}
