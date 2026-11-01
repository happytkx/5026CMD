// Dish on the digital menu: price, category, dietary/allergen information and whether customers can currently order it.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RROMS.Models
{
    public class MenuItem : IAdministratorManaged
    {
        // Null for legacy/seeded records whose author is unknown.
        public int? CreatedByAdministratorID { get; set; }
        public Administrator? CreatedByAdministrator { get; set; }
        public int? UpdatedByAdministratorID { get; set; }
        public Administrator? UpdatedByAdministrator { get; set; }

        public int MenuCategoryID { get; set; }
        public MenuCategory MenuCategory { get; set; } = null!;

        [Key]
        public int MenuItemID
        {
            get; set;
        }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(600)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price
        {
            get; set;
        }

        [StringLength(120)]
        public string Category { get; set; } = "Main Course";

        [StringLength(400)]
        public string? ImageUrl
        {
            get; set;
        }

        public bool IsAvailable { get; set; } = true;

        public bool IsPopular
        {
            get; set;
        }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? DiscountPercent
        {
            get; set;
        }

        [StringLength(250)]
        public string DietaryTags { get; set; } = string.Empty;

        [StringLength(40)]
        public string ProteinType { get; set; } = "Vegetarian";

        [Range(0, 5)]
        public int SpiceLevel
        {
            get; set;
        }

        [StringLength(300)]
        public string Allergens { get; set; } = string.Empty;
    }
}
