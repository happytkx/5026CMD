// Public landing page data: guest-favourite dishes.
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<MenuItem> Popular { get; set; } = [];
    public List<Announcement> Announcements { get; set; } = [];
    public List<Announcement> PopupAnnouncements { get; set; } = [];
    public Announcement? Featured { get; set; }
    public DateTime Today => RROMS.Services.ReservationPolicy.Now.Date;

    public async Task OnGetAsync()
    {
        Popular = await db.MenuItems.AsNoTracking().Where(i => i.IsAvailable && i.IsPopular).OrderBy(i => i.Name).Take(4).ToListAsync();
        var now = RROMS.Services.ReservationPolicy.Now;
        var visible = await db.Announcements.AsNoTracking()
            .Where(a => a.IsActive && (a.StartsAt == null || a.StartsAt <= now) && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.CreatedAt).ThenByDescending(a => a.AnnouncementID).ToListAsync();
        // Promotions can use the featured banner and welcome pop-up, but not the regular news carousel.
        Featured = visible.FirstOrDefault(a => a.IsFeatured);
        PopupAnnouncements = visible.Where(a => a.ShowInPopup).ToList();
        Announcements = visible.Where(a => !a.IsPromotional).ToList();
    }
}
