// Customer inbox: order and payment updates for the signed-in customer, with mark-as-read actions.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages;

[Authorize]
public class NotificationsModel(ApplicationDbContext db) : PageModel
{
    public List<CustomerNotification> Items { get; set; } = [];
    public int UnreadCount
    {
        get; set;
    }

    public async Task OnGetAsync()
    {
        var id = ReservationIdentity.CustomerId(User);
        Items = await db.CustomerNotifications.AsNoTracking().Where(n => n.CustomerID == id).OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
        UnreadCount = await db.CustomerNotifications.AsNoTracking()
            .CountAsync(n => n.CustomerID == id && !n.IsRead);
    }

    public async Task<IActionResult> OnPostMarkReadAsync(int id)
    {
        var customerId = ReservationIdentity.CustomerId(User);
        var n = await db.CustomerNotifications.FirstOrDefaultAsync(x => x.CustomerNotificationID == id && x.CustomerID == customerId);
        if (n == null)
            return NotFound();
        n.IsRead = true;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkAllReadAsync()
    {
        var customerId = ReservationIdentity.CustomerId(User);
        await db.CustomerNotifications.Where(n => n.CustomerID == customerId && !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return RedirectToPage();
    }
}
