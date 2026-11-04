
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize]
public class PaymentReceiptModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public PaymentReceiptModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public Payment? Payment { get; private set; }

    // Itemised final receipts read the same stored orders and bill as staff checkout.
    // Keep the original order-line prices, even when the live menu has since changed.
    public Bill? DiningBill { get; private set; }

    public record ReceiptDish(string Name, int Quantity, decimal UnitPrice, decimal TotalPrice);

    public List<ReceiptDish> Dishes { get; private set; } = [];

    public decimal OutstandingBalance => DiningBill is null || Payment is null
        ? 0m
        : Math.Max(0m, DiningBill.TotalBill - DiningBill.DepositApplied - Payment.Amount);

    public bool IsStaff => User.IsInRole("Restaurant Staff") || User.IsInRole("Staff") || User.IsInRole("Administrator");

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var customerId = ReservationIdentity.CustomerId(User);

        // Only successful, supported payment records
        // are eligible for a receipt.
        Payment = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Reservation)
                .ThenInclude(r => r.Customer)
            .SingleOrDefaultAsync(p =>
                p.PaymentID == id &&
                (IsStaff || p.Reservation.CustomerID == customerId) &&
                p.PaymentStatus == "Paid" &&
                (p.PaymentType == "Deposit" ||
                 p.PaymentType == "DiningBill"));

        if (Payment == null)
        {
            return NotFound();
        }

        // Deposit receipts remain deposit-only. The final dining receipt also
        // includes the saved bill totals and all non-cancelled ordered dishes.
        if (Payment.PaymentType == "DiningBill")
        {
            DiningBill = await _db.Bills
                .AsNoTracking()
                .SingleOrDefaultAsync(b => b.ReservationID == Payment.ReservationID);

            var diningOrders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Lines)
                .Where(o => o.ReservationID == Payment.ReservationID &&
                            o.Status != "Cancelled")
                .ToListAsync();

            Dishes = diningOrders
                .SelectMany(order => order.Lines.Where(line => !line.IsCancelled))
                .GroupBy(line => new
                {
                    Name = MenuDisplayNames.InEnglish(line.ItemName),
                    line.UnitPrice
                })
                .OrderBy(group => group.Key.Name)
                .Select(group => new ReceiptDish(
                    group.Key.Name,
                    group.Sum(line => line.Quantity),
                    group.Key.UnitPrice,
                    group.Sum(line => line.LineTotal)))
                .ToList();
        }

        return Page();
    }
}
