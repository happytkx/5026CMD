// Restaurant announcement published by an administrator and shown on the customer home page.
using System.ComponentModel.DataAnnotations;

namespace RROMS.Models
{
    public class Announcement : IAdministratorManaged
    {
        // Null for legacy/seeded records whose author is unknown.
        public int? CreatedByAdministratorID { get; set; }
        public Administrator? CreatedByAdministrator { get; set; }
        public int? UpdatedByAdministratorID { get; set; }
        public Administrator? UpdatedByAdministrator { get; set; }

        [Key]
        public int AnnouncementID
        {
            get; set;
        }

        [Required, StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt
        {
            get; set;
        }
    }
}
