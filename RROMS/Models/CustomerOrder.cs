// Dine-in food order placed against a Seated reservation. Its lines are the source for the reservation's final bill.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RROMS.Models
{
    public class CustomerOrder
    {
        public static readonly string[] Statuses = ["Received", "Preparing", "Ready to Serve", "Served", "Completed", "Cancelled"];

        [Key]
        public int OrderID
        {
            get; set;
        }

        public int CustomerID
        {
            get; set;
        }

        public int ReservationID
        {
            get; set;
        }

        public DateTime CreatedAt
        {
            get; set;
        }

        [StringLength(30)]
        public string Status { get; set; } = "Received";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal
        {
            get; set;
        }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Tax
        {
            get; set;
        }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total
        {
            get; set;
        }

        [StringLength(500)]
        public string? Notes
        {
            get; set;
        }

        [StringLength(20)]
        public string Reference { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        public Customer Customer { get; set; } = null!;

        public Reservation Reservation { get; set; } = null!;

        public List<OrderLine> Lines { get; set; } = [];
    }

    // One dish on an order. Name and price are copied so later menu edits never change past orders or bills.
    public class OrderLine
    {
        [Key]
        public int OrderLineID
        {
            get; set;
        }

        public int OrderID
        {
            get; set;
        }

        public int MenuItemID
        {
            get; set;
        }

        [Required, StringLength(100)]
        public string ItemName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice
        {
            get; set;
        }

        public int Quantity
        {
            get; set;
        }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal
        {
            get; set;
        }

        // Keep cancelled dishes for history while excluding them from the bill.
        public bool IsCancelled { get; set; }

        public DateTime? CancelledAt { get; set; }

        [StringLength(100)]
        public string? CancelledBy { get; set; }

        public CustomerOrder Order { get; set; } = null!;

        public MenuItem MenuItem { get; set; } = null!;
    }
}
