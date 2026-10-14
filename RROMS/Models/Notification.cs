// In-website reservation updates shown on the booking details page. Outgoing email scheduling and delivery are handled separately by ReservationEmail.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RROMS.Models
{
    public class Notification
    {
        [Key]
        public int NotificationID
        {
            get; set;
        }

        [ForeignKey("Reservation")]
        public int ReservationID
        {
            get; set;
        }

        public string NotificationType { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime SentDate
        {
            get; set;
        }

        public Reservation Reservation { get; set; } = null!;
    }
}
