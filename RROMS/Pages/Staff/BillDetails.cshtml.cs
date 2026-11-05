
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

    // Sum recorded final payments separately from the reservation deposit.
    public decimal PaidDiningPayments => Payments
        .Where(p => p.PaymentType == "DiningBill" && p.PaymentStatus == "Paid")
        .Sum(p => p.Amount);

    // Actual non-cancelled order lines, shown on the itemised final bill.
    public List<CustomerOrder> DiningOrders { get; set; } = new();

    // A paid bill retains its original checkout amount for history,
    // but the amount still owed is zero after settlement.
    public decimal OutstandingBalance =>
        Bill?.BillStatus == "Paid" ? 0m : Bill?.BalanceToPay ?? 0m;

    // Financial settlement checks.
    // This does not verify the source of food orders.
    public BillSettlementCheck? SettlementCheck { get; set; }

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
                "The final bill was prepared from the served dishes. It is awaiting payment.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["BillError"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    // Staff confirms payment only after collecting the full amount onsite.
    public async Task<IActionResult> OnPostRecordFinalPaymentAsync(
        int id, string paymentMethod, string? paymentReference)
    {
        try
        {
            await _billingService.RecordFinalPaymentAsync(
                id, paymentMethod, paymentReference);
            TempData["BillMessage"] =
                "Final payment confirmed. The bill is now paid.";
        }
        catch (ArgumentException ex)
        {
            TempData["BillError"] = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["BillError"] = ex.Message;
        }
        catch (DbUpdateException)
        {
            TempData["BillError"] =
                "The payment could not be saved. Please refresh and check the payment history before retrying.";
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

        if (Bill != null)
        {
            DiningOrders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Lines)
                .Where(o => o.ReservationID == id && o.Status != "Cancelled")
                .OrderBy(o => o.CreatedAt)
                .ThenBy(o => o.OrderID)
                .ToListAsync();
        }

        Payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.ReservationID == id)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();

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
