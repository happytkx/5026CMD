
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

    // Stores seated reservations
    public List<Reservation> SeatedReservations
    { get; set; } = new();

    // Stores saved bills using ReservationID as the key
    public Dictionary<int, Bill> BillsByReservationId
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

        // Stop if there are no seated reservations
        if (SeatedReservations.Count == 0)
        {
            return;
        }

        // Collect the reservation IDs
        var reservationIds = SeatedReservations
            .Select(r => r.ReservationID)
            .ToList();

        // Retrieve any saved bills for these reservations
        BillsByReservationId = await _db.Bills
            .AsNoTracking()
            .Where(b => reservationIds.Contains(b.ReservationID))
            .ToDictionaryAsync(b => b.ReservationID);
    }
}
