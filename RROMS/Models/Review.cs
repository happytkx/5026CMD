// Customer rating and comment for a completed order. Each customer can review an order once.
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

        public int OrderID
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

        public CustomerOrder Order { get; set; } = null!;
    }
}
