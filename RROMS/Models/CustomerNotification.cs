// Account-level inbox message for a customer, such as order progress. Reservation updates remain in Notification.
using System.ComponentModel.DataAnnotations;

namespace RROMS.Models
{
    public class CustomerNotification
    {
        [Key]
        public int CustomerNotificationID
        {
            get; set;
        }

        public int CustomerID
        {
            get; set;
        }

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(700)]
        public string Message { get; set; } = string.Empty;

        [StringLength(30)]
        public string Type { get; set; } = "Order";

        public int? RelatedOrderID
        {
            get; set;
        }

        public bool IsRead
        {
            get; set;
        }

        public DateTime CreatedAt
        {
            get; set;
        }

        public Customer Customer { get; set; } = null!;

        public CustomerOrder? RelatedOrder
        {
            get; set;
        }
    }
}
