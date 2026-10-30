using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;
namespace RROMS.Pages.Reservations;

[Authorize(Policy = "Customer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReceiptModel(ApplicationDbContext db) : PageModel
{
    public Payment? Payment { get; private set; }
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var customerId = ReservationIdentity.CustomerId(User);
        Payment = await db.Payments.AsNoTracking().Include(p => p.Reservation).ThenInclude(r => r.Customer)
            .SingleOrDefaultAsync(p => p.PaymentID == id && p.Reservation.CustomerID == customerId &&
                p.PaymentStatus == "Paid" && (p.PaymentType == "Deposit" || p.PaymentType == "DiningBill"));
        return Payment == null ? NotFound() : Page();
    }
}
