// Loads an owned booking or staff-accessible booking and handles actions through ReservationService. Selects payment, confirmation, details, modify and late-arrival views.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;
namespace RROMS.Pages.Reservations;

[Authorize(Roles = "Customer,Restaurant Staff,Staff,Administrator")]
public class DetailsModel(ApplicationDbContext db, ReservationService service, TableAllocationService allocation, IConfiguration config) : PageModel
{
    public Reservation? Booking
    {
        get; set;
    }
    // Administrators inherit staff operations; business rules remain in ReservationService.
    public bool IsStaff => User.IsInRole("Restaurant Staff") || User.IsInRole("Staff") || User.IsInRole("Administrator");
    public List<Notification> Notifications { get; set; } = [];
    public List<ReservationEmail> EmailMessages { get; set; } = [];
    public string BankName => config["SimulatedPayments:BankName"] ?? "Demo Bank";
    public string BankAccountName => config["SimulatedPayments:AccountName"] ?? "Celeste Restaurant (Demo)";
    public string BankAccountNumber => config["SimulatedPayments:AccountNumber"] ?? "DEMO-00000000";
    public bool EmailEnabled => config.GetValue<bool>("Email:Enabled");
    public List<RestaurantTable> AvailableTables { get; set; } = [];
    public List<Reservation> CustomerHistory { get; set; } = [];
    public List<DateTime> ModifySlots { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string? View { get; set; } = "Details";
    [BindProperty(SupportsGet = true)] public DateTime NewDate { get; set; } = ReservationPolicy.Now.Date.AddDays(8);
    [BindProperty] public string NewTime { get; set; } = "";
    private async Task<bool> Load(int id)
    {
        await service.Expire();
        var customerId = ReservationIdentity.CustomerId(User);
        Booking = await db.Reservations.AsSplitQuery().Include(r => r.Customer).Include(r => r.Payments).Include(r => r.ReservationHistories)
            .SingleOrDefaultAsync(r => r.ReservationID == id && (IsStaff || r.CustomerID == customerId));
        if (Booking == null)
            return false;
        Notifications = await db.Notifications.Where(n => n.ReservationID == id && (IsStaff || n.NotificationType != "Audit")).OrderByDescending(n => n.SentDate).ToListAsync();
        EmailMessages = await db.ReservationEmails.AsNoTracking().Where(e => e.ReservationID == id)
            .OrderByDescending(e => e.ReservationEmailID).ToListAsync();
        if (Booking.ReservationStatus == "Confirmed")
        {
            ModifySlots = await service.Slots(Booking.PartySize, Booking.PreferredDiningOption, NewDate, id);
            if (IsStaff)
                AvailableTables = await allocation.Available(Booking.PartySize, Booking.PreferredDiningOption,
                Booking.ReservationDate.Add(Booking.StartTime), Booking.ReservationDate.Add(Booking.EndTime), id, allowLargerTable: true);
        }
        if (IsStaff)
            CustomerHistory = await db.Reservations.Where(r => r.CustomerID == Booking.CustomerID).OrderByDescending(r => r.ReservationDate).ToListAsync();
        return true;
    }
    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await Load(id))
            return NotFound();
        if (View is not ("Details" or "Payment" or "Confirmation" or "Modify" or "Late"))
            View = "Details";
        if (Booking!.ReservationStatus == "Pending" && !IsStaff)
            View = "Payment";
        else if ((View == "Payment" && Booking.ReservationStatus != "Pending") || (View == "Confirmation" && Booking.ReservationStatus != "Confirmed"))
            View = "Details";
        return Page();
    }
    public async Task<IActionResult> OnPostActionAsync(int id, string action, string? tableId, string? dummyReference,
        bool paymentSuccess = true, bool allowLargerTable = false, string? reassignmentReason = null,
        string paymentMethod = "", string? phone = null, string? cardholder = null, string? cardNumber = null, string? expiry = null, string? cvv = null)
    {
        if (!await Load(id))
            return NotFound();
        try
        {
            DateTime? newStart = null;
            if (action == "Modify")
            {
                if (!TimeSpan.TryParse(NewTime, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
                    throw new InvalidOperationException("Choose a valid rescheduling time.");
                newStart = NewDate.Date.Add(time);
            }
            if (action == "Pay")
            {
                // Never redisplay payment inputs through ModelState, or persist card/phone fields.
                ModelState.Clear();
                SimulatedDeposit.Validate(paymentMethod, phone, cardholder, cardNumber, expiry, cvv, dummyReference);
                if (paymentMethod != "Bank Transfer") dummyReference = "SIM-" + Guid.NewGuid().ToString("N");
            }
            await service.Change(id, ReservationIdentity.CustomerId(User), IsStaff, action, newStart, tableId, paymentSuccess, dummyReference,
                allowLargerTable, reassignmentReason, paymentMethod);
            TempData["Message"] = action == "Pay" && !paymentSuccess ? "Payment failed. Your booking remains pending; you can retry before the hold expires." : "Reservation updated.";
            return RedirectToPage(new
            {
                id,
                view = action == "Pay" && paymentSuccess ? "Confirmation" : action == "Pay" ? "Payment" : "Details"
            });
        }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        catch (DbUpdateException) { ModelState.AddModelError("", "This reservation changed while you were viewing it. Please refresh and retry."); }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1205)
        {
            ModelState.AddModelError("", "Another booking was processed at the same time. Please retry.");
        }
        db.ChangeTracker.Clear();
        await Load(id);
        if (Booking!.ReservationStatus == "Pending" && !IsStaff)
            View = "Payment";
        else if (Booking.ReservationStatus != "Pending" && View == "Payment")
            View = "Details";
        return Page();
    }
}
