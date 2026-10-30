// Reservation business operations: validates live availability, creates payment holds and handles payment, modification, cancellation, late arrival, staff check-in and expiry.
using System.Data;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Services;

// All mutation paths share serializable transactions, including the expiry worker.
// Availability must be checked inside the transaction, not only when displaying slots.
public class ReservationService(ApplicationDbContext db, TableAllocationService allocation, IHttpContextAccessor? http = null)
{
    // Fixed agreed coursework deposit. Opening/closing hours are read from RestaurantSettings.
    public decimal CalculateDeposit(int partySize) => partySize * 50m;
    public string GenerateBookingReference() => "BK" + Guid.NewGuid().ToString("N");
    public async Task<RestaurantSetting> Settings() => await db.RestaurantSettings.OrderBy(s => s.SettingID).FirstAsync();

    public async Task<DateTime> ValidateSlot(int size, string area, DateTime start)
    {
        var settings = await Settings();
        if (!ReservationPolicy.Areas(size).Contains(area))
            throw new InvalidOperationException("Choose an allowed dining area for 1–12 guests.");
        if (!ReservationPolicy.MeetsAdvance(area, start, ReservationPolicy.Now))
            throw new InvalidOperationException("Book Indoor/Outdoor at least 2 hours ahead, or VIP at least 24 hours ahead (Malaysia time). Choose a later slot.");
        var end = ReservationPolicy.End(start, area, settings.ClosingTime);
        if (start <= ReservationPolicy.Now || start.TimeOfDay < settings.OpeningTime ||
            end.Date != start.Date || end.TimeOfDay > settings.ClosingTime || end <= start ||
            start.TimeOfDay.Ticks % TimeSpan.FromMinutes(30).Ticks != 0)
            throw new InvalidOperationException("Choose a future half-hour slot within opening hours. Indoor and Outdoor require two hours before closing.");
        return end;
    }

    public async Task<List<DateTime>> Slots(int size, string area, DateTime date, int? excluding = null)
    {
        await Expire();
        var settings = await Settings();
        var result = new List<DateTime>();
        for (var start = date.Date.Add(settings.OpeningTime); start < date.Date.Add(settings.ClosingTime); start = start.AddMinutes(30))
        {
            if (!ReservationPolicy.MeetsAdvance(area, start, ReservationPolicy.Now))
                continue;
            var end = ReservationPolicy.End(start, area, settings.ClosingTime);
            if (end <= date.Date.Add(settings.ClosingTime) && end > start &&
                (await allocation.Available(size, area, start, end, excluding)).Count > 0)
                result.Add(start);
        }
        return result;
    }

    // Reserve the smallest suitable table inside one transaction. Pending bookings hold it for up to 10 minutes.
    public async Task<Reservation> Create(int customerId, int size, string area, DateTime start, string? requests)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await ExpireCore();
        var end = await ValidateSlot(size, area, start);
        if (!await db.Customers.AnyAsync(c => c.CustomerID == customerId))
            throw new InvalidOperationException("Your customer account could not be found. Please sign in again.");
        var table = (await allocation.Available(size, area, start, end)).FirstOrDefault()
            ?? throw new InvalidOperationException("That slot is no longer available. Choose another time.");
        var booking = new Reservation
        {
            CustomerID = customerId,
            TableID = table.TableID,
            PartySize = size,
            PreferredDiningOption = area,
            ReservationDate = start.Date,
            StartTime = start.TimeOfDay,
            EndTime = end.TimeOfDay,
            OriginalStart = start,
            CreatedDate = ReservationPolicy.Now,
            DepositAmount = CalculateDeposit(size),
            MinimumSpend = ReservationPolicy.MinimumSpend(size, area),
            DepositStatus = "Unpaid",
            ReservationStatus = "Pending",
            DietaryRequest = requests,
            HoldExpiresAt = ReservationPolicy.PaymentDeadline(area, start, ReservationPolicy.Now)
        };
        booking.BookingReference = GenerateBookingReference();
        db.Reservations.Add(booking);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return booking;
    }

    public async Task Change(int id, int? customerId, bool staff, string action,
        DateTime? newStart = null, string? tableId = null, bool paymentSuccess = true, string? paymentReference = null,
        bool allowLargerTable = false, string? reassignmentReason = null, string paymentMethod = "Card")
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await ExpireCore();
        var r = await db.Reservations.Include(r => r.Customer).SingleOrDefaultAsync(r => r.ReservationID == id &&
            (staff || r.CustomerID == customerId)) ?? throw new InvalidOperationException("Reservation not found.");
        var now = ReservationPolicy.Now;
        var start = r.ReservationDate.Add(r.StartTime);
        var previousStatus = r.ReservationStatus;
        var previousTable = r.TableID;
        var message = "";
        switch (action)
        {
            // Record each simulation attempt; only success confirms the booking and queues a confirmation email.
            case "Pay":
                if (paymentMethod is not ("Card" or "TNG" or "Bank Transfer"))
                    throw new InvalidOperationException("Choose a supported simulated payment method.");
                await ValidateSlot(r.PartySize, r.PreferredDiningOption, start);
                if (!(await allocation.Available(r.PartySize, r.PreferredDiningOption, start, r.ReservationDate.Add(r.EndTime), id)).Any(t => t.TableID == r.TableID))
                    throw new InvalidOperationException("The assigned table is no longer available.");
                r.DepositAmount = CalculateDeposit(r.PartySize);
                if (paymentReference?.Length > 40)
                    throw new InvalidOperationException("Payment reference must be at most 40 characters.");
                if (r.ReservationStatus != "Pending" || r.HoldExpiresAt <= now)
                    throw new InvalidOperationException("This payment hold has expired or the deposit was already paid.");
                db.Payments.Add(new Payment
                {
                    ReservationID = id,
                    PaymentType = "Deposit",
                    PaymentMethod = paymentMethod,
                    PaymentReference = paymentReference ?? "SIM-" + Guid.NewGuid().ToString("N"),
                    Amount = r.DepositAmount,
                    PaymentStatus = paymentSuccess ? "Paid" : "Failed",
                    PaymentDate = now
                });
                if (paymentSuccess)
                {
                    r.DepositStatus = "Paid";
                    r.ReservationStatus = "Confirmed";
                    r.HoldExpiresAt = null;
                    message = $"Confirmed {r.BookingReference}: {start:dd MMM yyyy HH:mm}, {r.PartySize} guests, {r.PreferredDiningOption}. Deposit RM{r.DepositAmount:0.00} paid. Minimum spend RM{r.MinimumSpend:0.00}. All cancellations forfeit the full deposit.";
                }
                else
                    message = "Simulated payment failed. Retry before the payment hold expires.";
                break;
            // Preserve party size, area, paid deposit and original deadline; allocate a table for the new time.
            case "Modify":
                if (r.ReservationStatus != "Confirmed" || !ReservationPolicy.CanModify(r.OriginalStart, r.ModificationCount, now))
                    throw new InvalidOperationException("Only one modification is allowed, more than 168 hours before the original booking.");
                var replacement = newStart ?? throw new InvalidOperationException("Choose a new date and time.");
                if (replacement == start)
                    throw new InvalidOperationException("Choose a different date or time.");
                var end = await ValidateSlot(r.PartySize, r.PreferredDiningOption, replacement);
                var table = (await allocation.Available(r.PartySize, r.PreferredDiningOption, replacement, end, id)).FirstOrDefault()
                    ?? throw new InvalidOperationException("No suitable table or room is available at that time.");
                db.ReservationHistories.Add(new ReservationHistory
                {
                    ReservationID = id,
                    OldDate = r.ReservationDate,
                    OldTime = r.StartTime,
                    NewDate = replacement.Date,
                    NewTime = replacement.TimeOfDay,
                    ChangedDate = now
                });
                r.ReservationDate = replacement.Date;
                r.StartTime = replacement.TimeOfDay;
                r.EndTime = end.TimeOfDay;
                r.TableID = table.TableID;
                r.ModificationCount++;
                r.LateArrivalNotified = false;
                message = "Reservation rescheduled. Your party size, dining area, minimum spend and paid deposit are retained.";
                break;
            // Cancelled and No-show are distinct statuses. Paid deposits are forfeited; unpaid holds have no charge.
            case "Cancel":
                if (r.ReservationStatus is not ("Pending" or "Confirmed"))
                    throw new InvalidOperationException("This reservation cannot be cancelled.");
                r.ReservationStatus = "Cancelled";
                r.CancellationDate = now;
                r.HoldExpiresAt = null;
                r.DepositStatus = r.DepositStatus == "Paid" ? "Forfeited" : "Unpaid";
                message = "Reservation cancelled. Any paid deposit is forfeited; no refund is issued.";
                break;
            case "Late":
                if (r.ReservationStatus != "Confirmed" || r.LateArrivalNotified || now >= start.AddMinutes(10))
                    throw new InvalidOperationException("Notify us before the normal 10-minute grace period expires.");
                r.LateArrivalNotified = true;
                message = $"Late arrival noted. Your table is held until {start.AddMinutes(30):HH:mm}. Your dining end time remains unchanged.";
                break;
            // Only staff can seat guests. The Seated status activates the computed OrderingEnabled property.
            case "CheckIn":
                if (!staff || r.ReservationStatus != "Confirmed" || now < start ||
                    ReservationPolicy.IsExpired(start, r.LateArrivalNotified, now))
                    throw new InvalidOperationException("Check-in is available from the booked time until the arrival deadline.");
                r.ReservationStatus = "Seated";
                r.CheckedInAt = now;
                message = "Checked in and seated. Dine-in ordering is now enabled.";
                break;
            case "Reassign":
                if (!staff || r.ReservationStatus != "Confirmed")
                    throw new InvalidOperationException("Only confirmed reservations can be reassigned.");
                var available = await allocation.Available(r.PartySize, r.PreferredDiningOption, start, r.ReservationDate.Add(r.EndTime), id,
                    allowLargerTable: allowLargerTable);
                if (!available.Any(t => t.TableID == tableId))
                    throw new InvalidOperationException("Select an available suitable table in the same area. Larger tables require an explicit staff exception.");
                var replacementTable = available.Single(t => t.TableID == tableId);
                var largerException = !ReservationPolicy.SuitableCapacity(r.PartySize, r.PreferredDiningOption, replacementTable.Capacity);
                if (largerException && (string.IsNullOrWhiteSpace(reassignmentReason) || reassignmentReason.Trim().Length > 200))
                    throw new InvalidOperationException("Enter a reason for the larger table exception, up to 200 characters.");
                r.TableID = tableId!;
                message = largerException
                    ? $"Staff assigned larger table {tableId} within your selected dining area. Reason: {reassignmentReason!.Trim()}"
                    : "Assigned table updated within your selected dining area.";
                break;
            case "Complete":
                if (!staff || r.ReservationStatus != "Seated")
                    throw new InvalidOperationException("Only seated reservations can be completed.");
                if (!await db.Bills.AnyAsync(b => b.ReservationID == id && b.BillStatus == "Paid"))
                    throw new InvalidOperationException("Settle the final dining bill before completing this reservation.");
                r.ReservationStatus = "Completed";
                message = "Dining session completed. Ordering is closed.";
                break;
            case "NoShow":
                if (staff && r.ReservationStatus == "No-show") { await tx.CommitAsync(); return; }

                if (!staff || r.ReservationStatus != "Confirmed" || !ReservationPolicy.IsExpired(start, r.LateArrivalNotified, now))
                    throw new InvalidOperationException("No-show is allowed only after the arrival deadline.");
                r.ReservationStatus = "No-show";
                r.DepositStatus = "Forfeited";
                message = "No-show recorded. Deposit forfeited and table released.";
                break;
            default:
                throw new InvalidOperationException("Unknown reservation action.");
        }
        var user = http?.HttpContext?.User;
        var actor = user?.Identity?.IsAuthenticated == true
            ? $"{user.Identity.Name} (ID: {user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}; role: {user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value})"
            : staff ? "Internal staff operation" : $"Customer ID: {customerId}";
        Notify(r, "Audit", $"Actor: {actor}. Action: {action}. Status: {previousStatus} -> {r.ReservationStatus}. Table: {previousTable} -> {r.TableID}. {message}");
        Notify(r, action, message);
        if ((action == "Pay" && paymentSuccess) || action == "Modify")
            ReservationEmailService.QueueConfirmation(db, r, action == "Modify");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private void Notify(Reservation r, string type, string message) => db.Notifications.Add(new Notification
    {
        Reservation = r,
        NotificationType = type,
        Message = message,
        SentDate = ReservationPolicy.Now
    });

    public async Task Expire()
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await ExpireCore();
        await tx.CommitAsync();
    }

    private async Task ExpireCore()
    {
        var now = ReservationPolicy.Now;
        var candidates = await db.Reservations.Where(r => r.ReservationStatus == "Pending" ||
            (r.ReservationStatus == "Confirmed" && r.ReservationDate <= now.Date)).ToListAsync();
        foreach (var r in candidates)
        {
            if (r.ReservationStatus == "Pending" && (r.HoldExpiresAt <= now || !ReservationPolicy.MeetsAdvance(r.PreferredDiningOption, r.ReservationDate.Add(r.StartTime), now)))
            {
                r.ReservationStatus = "Expired";
                r.HoldExpiresAt = null;
                Notify(r, "Expired", "Payment deadline passed. No deposit was charged and the table has been released. Choose a later slot to book again.");
            }
            else if (r.ReservationStatus == "Confirmed" && ReservationPolicy.IsExpired(r.ReservationDate.Add(r.StartTime), r.LateArrivalNotified, now))
            {
                r.ReservationStatus = "No-show";
                r.DepositStatus = "Forfeited";
                Notify(r, "No-show", "Arrival deadline passed. Deposit forfeited and table released.");
            }
        }
        await db.SaveChangesAsync();
    }
}
