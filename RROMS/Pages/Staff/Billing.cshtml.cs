using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class BillingModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public BillingModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Reservation> SeatedReservations
        { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Retrieve all seated reservations
        SeatedReservations = await _db.Reservations
            .AsNoTracking()
            .Include(r => r.Customer)
            .Where(r => r.ReservationStatus == "Seated")
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.StartTime)
            .ToListAsync();
    }
}