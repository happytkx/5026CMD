using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages;

[Authorize(Policy = "Customer")]
public class MyBillModel(ApplicationDbContext db) : PageModel
{
    public Reservation? Booking { get; private set; }
    public Bill? FinalBill { get; private set; }
    public List<CustomerOrder> Orders { get; private set; } = [];
    public List<Payment> Payments { get; private set; } = [];
    public List<Reservation> MyReservations { get; private set; } = [];

    public string ActiveTab { get; private set; } = "summary";

    public bool IsPaid => FinalBill?.BillStatus == "Paid";
    // The saved unpaid bill is final only while it matches all served dish totals.
    public bool IsFinalBillReady => !IsPaid && FinalBill?.BillStatus == "Draft" &&
        Orders.Count > 0 &&
        Orders.All(o => o.Status is "Served" or "Completed") &&
        FinalBill.FoodSubtotal == Orders.Sum(o => o.Subtotal) &&
        FinalBill.TaxAmount == Orders.Sum(o => o.Tax);
    public decimal FoodSubtotal => IsPaid ? FinalBill!.FoodSubtotal : Orders.Sum(o => o.Subtotal);
    public decimal TaxAmount => IsPaid ? FinalBill!.TaxAmount : Orders.Sum(o => o.Tax);
    public decimal GrandTotal => IsPaid ? FinalBill!.TotalBill : FoodSubtotal + TaxAmount;
    public decimal DepositApplied => IsPaid
        ? FinalBill!.DepositApplied
        : Booking?.DepositStatus == "Paid" && Payments.Any(p => p.PaymentType == "Deposit" && p.PaymentStatus == "Paid")
            ? Math.Min(Booking.DepositAmount, GrandTotal)
            : 0m;
    public decimal Outstanding => IsPaid ? 0m : Math.Max(0m, GrandTotal - DepositApplied);
    public decimal Shortfall => Math.Max(0m, (Booking?.MinimumSpend ?? 0m) - FoodSubtotal);

    public async Task<IActionResult> OnGetAsync(int? id, string? tab)
    {
        ActiveTab = string.Equals(tab, "payments", StringComparison.OrdinalIgnoreCase) ? "payments" : "summary";
        var customerId = ReservationIdentity.CustomerId(User);
        MyReservations = await db.Reservations.AsNoTracking()
            .Where(r => r.CustomerID == customerId)
            .OrderByDescending(r => r.ReservationDate)
            .ThenByDescending(r => r.StartTime)
            .ToListAsync();

        if (id.HasValue && !MyReservations.Any(r => r.ReservationID == id.Value))
            return NotFound();

        var selectedId = id ?? MyReservations
            .FirstOrDefault(r => r.ReservationStatus == "Seated")?.ReservationID
            ?? MyReservations.FirstOrDefault()?.ReservationID;

        if (selectedId == null)
            return Page();

        Booking = MyReservations.Single(r => r.ReservationID == selectedId.Value);
        FinalBill = await db.Bills.AsNoTracking()
            .SingleOrDefaultAsync(b => b.ReservationID == selectedId.Value);
        Orders = await db.Orders.AsNoTracking().Include(o => o.Lines)
            .Where(o => o.ReservationID == selectedId.Value && o.CustomerID == customerId && o.Status != "Cancelled")
            .OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderID)
            .ToListAsync();
        Payments = await db.Payments.AsNoTracking()
            .Where(p => p.ReservationID == selectedId.Value &&
                (p.PaymentType == "Deposit" || p.PaymentType == "DiningBill"))
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.PaymentID)
            .ToListAsync();
        return Page();
    }
}
