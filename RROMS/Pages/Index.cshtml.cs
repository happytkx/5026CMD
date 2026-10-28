// Public landing page data: active announcements and the guest-favourite dishes.
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<MenuItem> Popular { get; set; } = [];
    public List<Announcement> Announcements { get; set; } = [];

    public async Task OnGetAsync()
    {
        Popular = await db.MenuItems.AsNoTracking().Where(i => i.IsAvailable && i.IsPopular).OrderBy(i => i.Name).Take(4).ToListAsync();
        Announcements = await db.Announcements.AsNoTracking().Where(a => a.IsActive).OrderByDescending(a => a.CreatedAt).Take(3).ToListAsync();
    }
}
