// Loads only the current customer's reservations and applies the Upcoming/All filter.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;
namespace RROMS.Pages.Reservations;

[Authorize(Policy = "Customer")]
public class IndexModel(ApplicationDbContext db, ReservationService service) : PageModel
{
    public List<Reservation> Bookings { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "Upcoming";
    public async Task OnGetAsync()
    {
        await service.Expire();
        var id = ReservationIdentity.CustomerId(User);
        Bookings = await db.Reservations.AsNoTracking().Where(r => r.CustomerID == id)
            .OrderByDescending(r => r.ReservationDate).ThenByDescending(r => r.StartTime).ToListAsync();
        if (Tab != "All")
            Bookings = Bookings.Where(r => r.ReservationDate.Add(r.EndTime) > ReservationPolicy.Now && r.ReservationStatus is "Pending" or "Confirmed" or "Seated").
                OrderBy(r => r.ReservationDate.Add(r.StartTime)).ToList();
    }
}
