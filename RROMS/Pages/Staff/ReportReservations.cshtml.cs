
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class ReportReservationsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    private static readonly string[] AllowedStatuses =
    {
        "Pending",
        "Confirmed",
        "Seated",
        "Completed",
        "Cancelled",
        "No-show",
        "Expired"
    };

    public ReportReservationsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; } =
        ReservationPolicy.Now.Date.AddDays(-30);

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; } =
        ReservationPolicy.Now.Date;

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<Reservation> Reservations { get; set; } = new();

    public string StatusLabel =>
        string.IsNullOrEmpty(Status)
            ? "All Reservations"
            : Status == "Seated"
                ? "Checked In (Seated)"
                : Status;

    public async Task<IActionResult> OnGetAsync()
    {
        DateTime from = StartDate.Date;
        DateTime to = EndDate.Date;

        if (from > to || to == DateTime.MaxValue.Date)
        {
            ModelState.AddModelError(
                "",
                "Please select a valid reporting date range.");

            return Page();
        }

        // Only supported reservation statuses may be filtered.
        if (!string.IsNullOrEmpty(Status) &&
            !AllowedStatuses.Contains(Status))
        {
            return BadRequest();
        }

        DateTime endExclusive = to.AddDays(1);

        // Use the same scheduled dining date rule
        // as the existing reservation reports.
        var query = _db.Reservations
            .AsNoTracking()
            .Include(r => r.Customer)
            .Where(r => r.ReservationDate >= from &&
                        r.ReservationDate < endExclusive);

        if (!string.IsNullOrEmpty(Status))
        {
            query = query.Where(
                r => r.ReservationStatus == Status);
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            string keyword = Search.Trim();

            query = query.Where(r =>
                r.BookingReference.Contains(keyword) ||
                r.Customer.CustomerName.Contains(keyword));
        }

        Reservations = await query
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.StartTime)
            .ThenBy(r => r.ReservationID)
            .ToListAsync();

        return Page();
    }
}
