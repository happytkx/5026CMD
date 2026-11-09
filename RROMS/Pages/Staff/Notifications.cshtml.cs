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

    private IQueryable<StaffNotification> VisibleAlerts(string recipientKey)
    {
        var isAdministrator = User.IsInRole(Customer.AdminRole);
        return db.StaffNotifications.Where(n =>
            (n.Audience == StaffNotification.StaffAudience ||
             (isAdministrator && n.Audience == StaffNotification.AdministratorAudience)) &&
            !db.StaffNotificationReads.Any(r =>
                r.StaffNotificationID == n.StaffNotificationID &&
                r.RecipientKey == recipientKey && r.DismissedAt != null));
    }

    public async Task OnGetAsync()
    {
        var key = RecipientKey;
        if (string.IsNullOrWhiteSpace(key)) return;
        UnreadCount = await VisibleAlerts(key).CountAsync(n =>
            !db.StaffNotificationReads.Any(r => r.StaffNotificationID == n.StaffNotificationID && r.RecipientKey == key));

        var rows = await VisibleAlerts(key).AsNoTracking()
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

        // Older alerts stored only a day-list URL. Recover their booking reference so
        // View details opens the exact reservation without modifying existing alerts.
        var legacyAlerts = rows.Where(n =>
            n.TargetUrl.StartsWith("/Staff/Index?Date=", StringComparison.OrdinalIgnoreCase) &&
            (n.Type == "Reservation" || n.Type == "Payment")).ToList();

        static string? BookingReferenceFromMessage(string message)
        {
            const string bookingPrefix = "Booking ";
            const string paymentPrefix = "The deposit attempt for ";
            if (message.StartsWith(bookingPrefix, StringComparison.OrdinalIgnoreCase))
                message = message[bookingPrefix.Length..];
            else if (message.StartsWith(paymentPrefix, StringComparison.OrdinalIgnoreCase))
                message = message[paymentPrefix.Length..];

            var end = message.IndexOfAny([' ', ':', ',', ';', '.']);
            var reference = (end < 0 ? message : message[..end]).Trim();
            return string.IsNullOrWhiteSpace(reference) ? null : reference;
        }

        var references = legacyAlerts.Select(n => BookingReferenceFromMessage(n.Message))
            .Where(reference => reference != null)
            .Select(reference => reference!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var reservationIds = references.Count == 0
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : (await db.Reservations.AsNoTracking()
                .Where(r => references.Contains(r.BookingReference))
                .Select(r => new { r.BookingReference, r.ReservationID })
                .ToListAsync())
                .ToDictionary(r => r.BookingReference, r => r.ReservationID, StringComparer.OrdinalIgnoreCase);

        Items = rows.Select(n =>
        {
            var targetUrl = n.TargetUrl;
            if (n.TargetUrl.StartsWith("/Staff/Index?Date=", StringComparison.OrdinalIgnoreCase) &&
                (n.Type == "Reservation" || n.Type == "Payment") &&
                BookingReferenceFromMessage(n.Message) is string reference &&
                reservationIds.TryGetValue(reference, out var reservationId))
                targetUrl = StaffAlerts.ReservationUrl(reservationId);

            return new AlertRow(n.StaffNotificationID, n.Title, n.Message, n.Type,
                n.CreatedAt, targetUrl, n.IsRead);
        }).ToList();
    }

    public async Task<IActionResult> OnPostMarkReadAsync(int id)
    {
        var key = RecipientKey;
        if (string.IsNullOrWhiteSpace(key)) return Forbid();
        if (!await VisibleAlerts(key).AnyAsync(n => n.StaffNotificationID == id))
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
        var ids = await VisibleAlerts(key)
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


    public async Task<IActionResult> OnPostDismissAsync(int id)
    {
        var key = RecipientKey;
        if (string.IsNullOrWhiteSpace(key)) return Forbid();
        if (!await VisibleAlerts(key).AnyAsync(n => n.StaffNotificationID == id))
            return NotFound();

        var read = await db.StaffNotificationReads.SingleOrDefaultAsync(r =>
            r.StaffNotificationID == id && r.RecipientKey == key);
        var now = ReservationPolicy.Now;
        if (read is null)
            db.StaffNotificationReads.Add(new StaffNotificationRead
            {
                StaffNotificationID = id, RecipientKey = key, ReadAt = now, DismissedAt = now
            });
        else
            read.DismissedAt = now;

        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearAllAsync()
    {
        var key = RecipientKey;
        if (string.IsNullOrWhiteSpace(key)) return Forbid();

        // Clear every visible alert, not only the 150 shown on the page.
        var ids = await VisibleAlerts(key).Select(n => n.StaffNotificationID).ToListAsync();
        if (ids.Count == 0) return RedirectToPage();

        var existing = await db.StaffNotificationReads
            .Where(r => r.RecipientKey == key && ids.Contains(r.StaffNotificationID))
            .ToDictionaryAsync(r => r.StaffNotificationID);
        var now = ReservationPolicy.Now;
        foreach (var id in ids)
        {
            if (existing.TryGetValue(id, out var read))
                read.DismissedAt = now;
            else
                db.StaffNotificationReads.Add(new StaffNotificationRead
                {
                    StaffNotificationID = id, RecipientKey = key, ReadAt = now, DismissedAt = now
                });
        }
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
