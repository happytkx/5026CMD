// Customer rating and comment, optionally linked to a completed dining order.
using System.ComponentModel.DataAnnotations;

namespace RROMS.Models
{
    public class Review
    {
        [Key]
        public int ReviewID
        {
            get; set;
        }

        public int CustomerID
        {
            get; set;
        }

        public int? OrderID
        {
            get; set;
        }

        [Range(1, 5)]
        public int Rating
        {
            get; set;
        }

        [StringLength(800)]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt
        {
            get; set;
        }

        public Customer Customer { get; set; } = null!;

        public CustomerOrder? Order { get; set; }
    }
}
