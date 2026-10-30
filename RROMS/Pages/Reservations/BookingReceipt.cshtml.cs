using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Reservations;

[Authorize(Roles = "Customer,Restaurant Staff,Staff,Administrator")]
public class BookingReceiptModel(ApplicationDbContext db) : PageModel
{
    public Reservation? Booking { get; private set; }
    public Payment? DepositPayment { get; private set; }
    public bool IsPaid => DepositPayment is not null;

    [BindProperty(SupportsGet = true)]
    public bool Print { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (id <= 0) return NotFound();

        var isStaff = User.IsInRole("Restaurant Staff") ||
            User.IsInRole("Staff") || User.IsInRole("Administrator");
        var customerId = ReservationIdentity.CustomerId(User);

        Booking = await db.Reservations.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Payments)
            .SingleOrDefaultAsync(r => r.ReservationID == id &&
                (isStaff || r.CustomerID == customerId));
        if (Booking is null) return NotFound();

        DepositPayment = Booking.Payments
            .Where(p => p.PaymentType == "Deposit" && p.PaymentStatus == "Paid")
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefault();

        return Page();
    }
}
