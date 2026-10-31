// Restaurant announcement published by an administrator and shown on the customer home page.
using System.ComponentModel.DataAnnotations;

namespace RROMS.Models
{
    public class Announcement
    {
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

        // Null means the announcement remains visible until the administrator removes it.
        // The end is exclusive so an end date can include its entire calendar day.
        public DateTime? ExpiresAt { get; set; }

        public DateTime CreatedAt
        {
            get; set;
        }
    }
}
