// Staff seating board: loads table inventory and active reservations for a selected area, date and time. The view derives Available/Reserved/Occupied states.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;
namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class SeatingModel(ApplicationDbContext db, ReservationService service) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Area { get; set; } = "Indoor";
    [BindProperty(SupportsGet = true)] public DateTime Date { get; set; } = ReservationPolicy.Now.Date;
    [BindProperty(SupportsGet = true)] public string Time { get; set; } = ReservationPolicy.Now.ToString("HH:mm");
    public List<RestaurantTable> Tables { get; set; } = [];
    public List<Reservation> Reservations { get; set; } = [];
    public DateTime At
    {
        get; set;
    }
    public async Task OnGetAsync()
    {
        if (!new[] { "Indoor", "Outdoor", "VIP Room" }.Contains(Area))
            Area = "Indoor";
        At = Date.Date.Add(TimeSpan.TryParse(Time, out var time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1) ? time : TimeSpan.Zero);
        await service.Expire();
        Tables = await db.RestaurantTables.AsNoTracking().Include(t => t.DiningArea).Where(t => t.DiningArea.AreaType == Area).OrderBy(t => t.TableID).ToListAsync();
        Reservations = await db.Reservations.AsNoTracking().Where(r => r.ReservationDate == Date.Date && r.PreferredDiningOption == Area && (r.ReservationStatus == "Confirmed" || r.ReservationStatus == "Seated" || (r.ReservationStatus == "Pending" && r.HoldExpiresAt > ReservationPolicy.Now))).ToListAsync();
    }
}
