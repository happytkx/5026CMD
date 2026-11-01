using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class NotificationsModel(ApplicationDbContext db) : PageModel
{
    public record AlertRow(int Id, string Title, string Message, string Type, DateTime CreatedAt, string TargetUrl, bool IsRead);
    public List<AlertRow> Items { get; private set; } = [];
    public int UnreadCount { get; private set; }
    private string RecipientKey => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    private IQueryable<StaffNotification> VisibleAlerts()
    {
        var isAdministrator = User.IsInRole(Customer.AdminRole);
        return db.StaffNotifications.Where(n =>
            n.Audience == StaffNotification.StaffAudience ||
            (isAdministrator && n.Audience == StaffNotification.AdministratorAudience));
    }

    public async Task OnGetAsync()
    {
        var key = RecipientKey;
        if (string.IsNullOrWhiteSpace(key)) return;
        UnreadCount = await VisibleAlerts().CountAsync(n =>
            !db.StaffNotificationReads.Any(r => r.StaffNotificationID == n.StaffNotificationID && r.RecipientKey == key));

        var rows = await VisibleAlerts().AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.StaffNotificationID)
            .Take(150)
            .Select(n => new
            {
                n.StaffNotificationID, n.Title, n.Message, n.Type, n.CreatedAt, n.TargetUrl,
                IsRead = db.StaffNotificationReads.Any(r =>
                    r.StaffNotificationID == n.StaffNotificationID && r.RecipientKey == key)
            })
            .ToListAsync();

        Items = rows.Select(n => new AlertRow(n.StaffNotificationID, n.Title, n.Message, n.Type,
            n.CreatedAt, n.TargetUrl, n.IsRead)).ToList();
    }

    public async Task<IActionResult> OnPostMarkReadAsync(int id)
    {
        var key = RecipientKey;
        if (string.IsNullOrWhiteSpace(key)) return Forbid();
        if (!await VisibleAlerts().AnyAsync(n => n.StaffNotificationID == id))
            return NotFound();
        if (!await db.StaffNotificationReads.AnyAsync(r => r.StaffNotificationID == id && r.RecipientKey == key))
        {
            db.StaffNotificationReads.Add(new StaffNotificationRead
            {
                StaffNotificationID = id, RecipientKey = key, ReadAt = ReservationPolicy.Now
            });
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkAllReadAsync()
    {
        var key = RecipientKey;
        if (string.IsNullOrWhiteSpace(key)) return Forbid();
        var ids = await VisibleAlerts()
            .Where(n => !db.StaffNotificationReads.Any(r =>
                r.StaffNotificationID == n.StaffNotificationID && r.RecipientKey == key))
            .Select(n => n.StaffNotificationID)
            .ToListAsync();
        foreach (var id in ids)
            db.StaffNotificationReads.Add(new StaffNotificationRead
            {
                StaffNotificationID = id, RecipientKey = key, ReadAt = ReservationPolicy.Now
            });
        if (ids.Count > 0) await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
