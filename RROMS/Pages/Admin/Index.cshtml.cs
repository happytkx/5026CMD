// Administrator dashboard: order and reservation figures for a period, popular dishes, CSV export, menu availability switches and announcements.
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class IndexModel(ApplicationDbContext db, BusinessAnalyticsService analytics, OccupancyReportingService occupancy) : PageModel
{
    public BusinessAnalyticsReport? Insights { get; private set; }
    public OccupancyReport? Occupancy { get; private set; }
    public DateTime From { get; private set; }
    public DateTime To { get; private set; }
    public decimal OrderSales
    {
        get; private set;
    }
    public int OrderCount
    {
        get; private set;
    }
    public int ReservationCount
    {
        get; private set;
    }
    public int CustomerCount
    {
        get; private set;
    }
    public List<PopularRow> Popular { get; private set; } = [];
    public List<MenuItem> Menu { get; private set; } = [];
    public List<Announcement> Announcements { get; private set; } = [];
    public DateTime CurrentTime => ReservationPolicy.Now;

    public record PopularRow(string Name, int Quantity, decimal Sales);

    public async Task OnGetAsync(DateTime? from, DateTime? to)
    {
        (From, To) = Period(from, to);
        var endExclusive = To.AddDays(1);
        // Cancelled orders are excluded from sales and popularity figures.
        var orders = await db.Orders.AsNoTracking().Include(o => o.Lines)
            .Where(o => o.CreatedAt >= From && o.CreatedAt < endExclusive && o.Status != "Cancelled").ToListAsync();
        OrderSales = orders.Sum(o => o.Total);
        OrderCount = orders.Count;
        ReservationCount = await db.Reservations.CountAsync(r => r.ReservationDate >= From && r.ReservationDate < endExclusive);
        CustomerCount = await db.Customers.CountAsync(c => c.Role == Customer.CustomerRole);
        Insights = await analytics.GetReportAsync(From, To);
        if ((To - From).Days <= 89)
            Occupancy = await occupancy.GetReportAsync(From, To);
        Popular = orders.SelectMany(o => o.Lines.Where(l => !l.IsCancelled)).GroupBy(l => l.ItemName)
            .Select(g => new PopularRow(g.Key, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
            .OrderByDescending(p => p.Quantity).Take(8).ToList();
        Menu = await db.MenuItems.AsNoTracking().OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync();
        Announcements = await db.Announcements.AsNoTracking().Where(a => a.IsActive).OrderByDescending(a => a.CreatedAt).ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleMenuAsync(int itemId)
    {
        var item = await db.MenuItems.FindAsync(itemId);
        if (item is null)
            return NotFound();
        item.IsAvailable = !item.IsAvailable;
        await db.SaveChangesAsync();
        TempData["Success"] = $"{item.Name} is now {(item.IsAvailable ? "available" : "unavailable")}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAnnouncementAsync(string? title, string? message, string? duration, DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message) || title.Trim().Length > 120 || message.Trim().Length > 1000)
        {
            TempData["Error"] = "Enter a valid title and message.";
            return RedirectToPage();
        }

        var now = ReservationPolicy.Now;
        if (!TryGetExpiry(duration, endDate, now, out var expiry))
        {
            TempData["Error"] = "Choose a valid announcement duration or an end date that is today or later.";
            return RedirectToPage();
        }

        db.Announcements.Add(new Announcement
        {
            Title = title.Trim(),
            Message = message.Trim(),
            CreatedAt = now,
            ExpiresAt = expiry
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Announcement published. It will be hidden automatically when its duration ends.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetAnnouncementExpiryAsync(int id, string? duration, DateTime? endDate)
    {
        var announcement = await db.Announcements.FindAsync(id);
        if (announcement is null) return NotFound();

        if (!TryGetExpiry(duration, endDate, ReservationPolicy.Now, out var expiry))
        {
            TempData["Error"] = "Choose a valid duration or an end date that is today or later.";
            return RedirectToPage();
        }

        announcement.ExpiresAt = expiry;
        await db.SaveChangesAsync();
        TempData["Success"] = $"The display duration for {announcement.Title} was updated.";
        return RedirectToPage();
    }

    private static bool TryGetExpiry(string? duration, DateTime? endDate, DateTime now, out DateTime? expiresAt)
    {
        expiresAt = null;
        switch (duration)
        {
            case "1":
                expiresAt = now.AddDays(1);
                return true;
            case "7":
                expiresAt = now.AddDays(7);
                return true;
            case "30":
                expiresAt = now.AddDays(30);
                return true;
            case "never":
                return true;
            case "custom" when endDate.HasValue && endDate.Value.Date >= now.Date
                               && endDate.Value.Date <= now.Date.AddYears(5):
                expiresAt = endDate.Value.Date.AddDays(1);
                return true;
            default:
                return false;
        }
    }

    public async Task<IActionResult> OnPostRetireAnnouncementAsync(int id)
    {
        var announcement = await db.Announcements.FindAsync(id);
        if (announcement is null)
            return NotFound();
        announcement.IsActive = false;
        await db.SaveChangesAsync();
        TempData["Success"] = "Announcement removed from the home page.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetExportAsync(DateTime? from, DateTime? to)
    {
        var (start, end) = Period(from, to);
        var endExclusive = end.AddDays(1);
        var orders = await db.Orders.AsNoTracking().Include(o => o.Customer)
            .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive).OrderBy(o => o.CreatedAt).ToListAsync();

        // Quote every value and neutralise spreadsheet formulas.
        static string EscapeCsv(string value)
        {
            if (value.TrimStart() is { Length: > 0 } trimmed && "=+-@".Contains(trimmed[0]))
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", new[] { "Reference", "Date", "Customer", "Status", "Subtotal", "Tax", "Total" }.Select(EscapeCsv)));
        foreach (var o in orders)
            csv.AppendLine(string.Join(",", new[] { o.Reference, o.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), o.Customer.Email, o.Status, Money(o.Subtotal), Money(o.Tax), Money(o.Total) }.Select(EscapeCsv)));

        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(content, "text/csv; charset=utf-8", $"celeste-orders-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    // Defaults to the last 30 days; an end date before the start date collapses to a single day.
    private static (DateTime From, DateTime To) Period(DateTime? from, DateTime? to)
    {
        var today = ReservationPolicy.Now.Date;
        var start = (from ?? today.AddDays(-29)).Date;
        var end = (to ?? today).Date;
        return (start, end < start ? start : end);
    }
}
