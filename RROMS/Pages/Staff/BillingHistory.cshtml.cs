
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Services;

namespace RROMS.Pages.Staff;

public sealed class BillingHistoryRow
{
    public int BillID { get; set; }
    public int ReservationID { get; set; }
    public string BookingReference { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public DateTime ReservationDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public string ReservationStatus { get; set; } = "";
    public decimal TotalBill { get; set; }
    public decimal DepositApplied { get; set; }
    public decimal BalanceToPay { get; set; }
    public string BillStatus { get; set; } = "";
}

[Authorize(Policy = "Staff")]
public class BillingHistoryModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public BillingHistoryModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; } =
        ReservationPolicy.Now.Date.AddDays(-30);

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; } =
        ReservationPolicy.Now.Date;

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<BillingHistoryRow> Bills { get; set; } = new();

    public int DraftCount =>
        Bills.Count(b => b.BillStatus == "Draft");

    public int PaidCount =>
        Bills.Count(b => b.BillStatus == "Paid");

    public async Task<IActionResult> OnGetAsync()
    {
        DateTime startDate = StartDate.Date;
        DateTime endDate = EndDate.Date;

        if (startDate > endDate || endDate == DateTime.MaxValue.Date)
        {
            ModelState.AddModelError(
                "",
                "Please select a valid reporting date range.");

            return Page();
        }

        if (!string.IsNullOrEmpty(Status) &&
            Status != "Draft" &&
            Status != "Paid")
        {
            return BadRequest();
        }

        string keyword = Search?.Trim() ?? "";

        if (keyword.Length > 100)
        {
            ModelState.AddModelError(
                "",
                "Search cannot exceed 100 characters.");

            return Page();
        }

        DateTime endExclusive = endDate.AddDays(1);

        // Join saved bills to their reservations and customers.
        var query =
            from bill in _db.Bills.AsNoTracking()
            join reservation in _db.Reservations.AsNoTracking()
                on bill.ReservationID equals reservation.ReservationID
            join customer in _db.Customers.AsNoTracking()
                on reservation.CustomerID equals customer.CustomerID
            where reservation.ReservationDate >= startDate &&
                  reservation.ReservationDate < endExclusive
            select new
            {
                bill,
                reservation,
                customer
            };

        // Filter by the saved bill's status.
        if (!string.IsNullOrEmpty(Status))
        {
            query = query.Where(x =>
                x.bill.BillStatus == Status);
        }

        // Search by actual customer name or booking reference.
        if (keyword.Length > 0)
        {
            query = query.Where(x =>
                x.reservation.BookingReference.Contains(keyword) ||
                x.customer.CustomerName.Contains(keyword));
        }

        Bills = await query
            .OrderByDescending(x => x.reservation.ReservationDate)
            .ThenByDescending(x => x.reservation.StartTime)
            .ThenByDescending(x => x.bill.BillID)
            .Select(x => new BillingHistoryRow
            {
                BillID = x.bill.BillID,
                ReservationID = x.reservation.ReservationID,
                BookingReference = x.reservation.BookingReference,
                CustomerName = x.customer.CustomerName,
                ReservationDate = x.reservation.ReservationDate,
                StartTime = x.reservation.StartTime,
                ReservationStatus = x.reservation.ReservationStatus,
                TotalBill = x.bill.TotalBill,
                DepositApplied = x.bill.DepositApplied,
                BalanceToPay = x.bill.BalanceToPay,
                BillStatus = x.bill.BillStatus
            })
            .ToListAsync();

        return Page();
    }
}
