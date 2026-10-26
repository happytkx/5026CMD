
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class PaymentReceiptModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public PaymentReceiptModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public Payment? Payment { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        // Only successful, supported payment records
        // are eligible for a receipt.
        Payment = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Reservation)
                .ThenInclude(r => r.Customer)
            .SingleOrDefaultAsync(p =>
                p.PaymentID == id &&
                p.PaymentStatus == "Paid" &&
                (p.PaymentType == "Deposit" ||
                 p.PaymentType == "DiningBill"));

        if (Payment == null)
        {
            return NotFound();
        }

        return Page();
    }
}
