
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class BillDetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly BillingService _billingService;
    private readonly OrderBillingIntegrationService _orderBilling;

    public BillDetailsModel(
        ApplicationDbContext db,
        BillingService billingService,
        OrderBillingIntegrationService orderBilling)
    {
        _db = db;
        _billingService = billingService;
        _orderBilling = orderBilling;
    }

    public Reservation? Booking { get; set; }

    public Bill? Bill { get; set; }

    public List<Payment> Payments { get; set; } = new();

    // Financial settlement checks.
    // This does not verify the source of food orders.
    public BillSettlementCheck? SettlementCheck { get; set; }

    public decimal PaidDeposits { get; set; }

    public decimal PaidDiningPayments { get; set; }

    public decimal TotalCollected =>
        PaidDeposits + PaidDiningPayments;

    public decimal MinimumSpendShortfall =>
        Bill == null
            ? 0m
            : Math.Max(0m, Bill.MinSpend - Bill.FoodSubtotal);

    // Build or refresh the draft bill from the reservation's
    // served dining orders. Blocked until every order is served.
    public async Task<IActionResult> OnPostGenerateDraftAsync(int id)
    {
        try
        {
            await _orderBilling.SaveDraftFromOrdersAsync(id);
            TempData["BillMessage"] =
                "The draft bill was updated from the dining orders.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["BillError"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        Booking = await _db.Reservations
            .AsNoTracking()
            .Include(r => r.Customer)
            .SingleOrDefaultAsync(
                r => r.ReservationID == id);

        if (Booking == null)
        {
            return NotFound();
        }

        Bill = await _billingService.GetBillAsync(id);

        Payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.ReservationID == id)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();

        PaidDeposits = Payments
            .Where(p =>
                p.PaymentType == "Deposit" &&
                p.PaymentStatus == "Paid")
            .Sum(p => p.Amount);

        PaidDiningPayments = Payments
            .Where(p =>
                p.PaymentType == "DiningBill" &&
                p.PaymentStatus == "Paid")
            .Sum(p => p.Amount);

        // Retrieve the existing financial readiness check.
        // This only reads data and never records a payment.
        if (Bill != null && Bill.BillStatus == "Draft")
        {
            SettlementCheck =
                await _billingService.CheckSettlementAsync(id);
        }

        return Page();
    }
}
