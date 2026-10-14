// Audit record for a successful reschedule: preserves the old/new date and time without replacing the original deadline on the reservation.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RROMS.Models
{
    public class ReservationHistory
    {
        [Key]
        public int HistoryID
        {
            get; set;
        }

        [ForeignKey("Reservation")]
        public int ReservationID
        {
            get; set;
        }

        public DateTime OldDate
        {
            get; set;
        }

        public TimeSpan OldTime
        {
            get; set;
        }

        public DateTime NewDate
        {
            get; set;
        }

        public TimeSpan NewTime
        {
            get; set;
        }

        public DateTime ChangedDate
        {
            get; set;
        }

        public Reservation Reservation { get; set; } = null!;
    }
}
