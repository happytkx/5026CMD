// Staff daily schedule: loads reservations and customer details for the selected date, calculates dashboard data and filters by status/reference/name.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;
namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class IndexModel(ApplicationDbContext db, ReservationService service) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateTime Date { get; set; } = ReservationPolicy.Now.Date;
    [BindProperty(SupportsGet = true)]
    public string? Status
    {
        get; set;
    }
    [BindProperty(SupportsGet = true)]
    public string? Search
    {
        get; set;
    }
    public List<Reservation> DailyBookings { get; set; } = [];
    public RestaurantSetting CurrentPolicy { get; private set; } = new();
    public List<Reservation> Bookings { get; set; } = [];
    public async Task OnGetAsync()
    {
        await service.Expire();
        CurrentPolicy = await db.RestaurantSettings.AsNoTracking().OrderBy(s => s.SettingID).FirstOrDefaultAsync() ?? new RestaurantSetting();
        DailyBookings = await db.Reservations.AsNoTracking().Include(r => r.Customer)
            .Where(r => r.ReservationDate == Date.Date)
            .OrderBy(r => r.StartTime).ToListAsync();
        Bookings = DailyBookings.Where(r => (string.IsNullOrEmpty(Status) || r.ReservationStatus == Status) && 
        (string.IsNullOrWhiteSpace(Search) || r.BookingReference.Contains(Search, StringComparison.OrdinalIgnoreCase) || 
        r.Customer.CustomerName.Contains(Search, StringComparison.OrdinalIgnoreCase))).ToList();
    }
}
