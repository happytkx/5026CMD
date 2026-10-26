
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class BillDetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public BillDetailsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public Reservation? Booking { get; set; }

    public Bill? Bill { get; set; }

    public List<Payment> Payments { get; set; } = new();

    public decimal PaidDeposits { get; set; }

    public decimal PaidDiningPayments { get; set; }

    public decimal TotalCollected =>
        PaidDeposits + PaidDiningPayments;

    public decimal MinimumSpendShortfall =>
        Bill == null
            ? 0m
            : Math.Max(0m, Bill.MinSpend - Bill.TotalBill);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        // Reject invalid reservation IDs
        if (id <= 0)
        {
            return NotFound();
        }

        // Retrieve the reservation and customer details
        Booking = await _db.Reservations
            .AsNoTracking()
            .Include(r => r.Customer)
            .SingleOrDefaultAsync(r => r.ReservationID == id);

        if (Booking == null)
        {
            return NotFound();
        }

        // Retrieve the saved bill, if available
        Bill = await _db.Bills
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.ReservationID == id);

        // Retrieve all payment attempts and receipts
        Payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.ReservationID == id)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();

        // Calculate only successfully collected deposits
        PaidDeposits = Payments
            .Where(p => p.PaymentType == "Deposit"
                     && p.PaymentStatus == "Paid")
            .Sum(p => p.Amount);

        // Calculate successfully collected dining payments
        PaidDiningPayments = Payments
            .Where(p => p.PaymentType == "DiningBill"
                     && p.PaymentStatus == "Paid")
            .Sum(p => p.Amount);

        return Page();
    }
}
