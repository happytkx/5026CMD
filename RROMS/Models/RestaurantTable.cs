// Table/room inventory: stores area, capacity and availability status. Time conflicts are checked against Reservations, not just this status.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RROMS.Models
{
    public class RestaurantTable
    {
        [Key]
        [StringLength(10)]
        public string TableID { get; set; } = string.Empty;

        [ForeignKey("DiningArea")]
        public int DiningAreaID
        {
            get; set;
        }

        [Required]
        public int Capacity
        {
            get; set;
        }

        [Required]
        public string Status { get; set; } = string.Empty;

        public DiningArea DiningArea { get; set; } = null!;

        public ICollection<Reservation> Reservations { get; set; } = [];
    }
}
