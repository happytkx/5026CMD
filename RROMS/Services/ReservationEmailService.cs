// Creates confirmation/reminder messages and dispatches the durable queue with retries. Old, cancelled or no-longer-applicable booking messages are suppressed.
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RROMS.Data;
using RROMS.Models;
namespace RROMS.Services;

public class ReservationEmailService(ApplicationDbContext db, IEmailSender sender, IOptions<EmailOptions> options,
    ILogger<ReservationEmailService> logger)
{
    public static string Content(Reservation r, bool reminder) =>
        $"Hello {r.Customer.CustomerName},\n\n" +
        (reminder ? "We look forward to welcoming you for your reservation.\n" : "Your reservation is confirmed and your deposit payment succeeded.\n") +
        $"\nBooking reference: {r.BookingReference}\nDate: {r.ReservationDate:dddd, dd MMM yyyy}\n" +
        $"Arrival time: {r.ReservationDate.Add(r.StartTime):h:mm tt} (Malaysia time, UTC+8)\n" +
        $"Dining ends: {r.ReservationDate.Add(r.EndTime):h:mm tt}\nGuests: {r.PartySize}\nDining area: {r.PreferredDiningOption}\n" +
        $"Deposit paid: RM{r.DepositAmount:N2}\nMinimum dining spend: RM{r.MinimumSpend:N2}\n" +
        "\nPlease arrive at your booked time and show your booking reference to staff for check-in. " +
        "Your deposit is deducted from the final dining bill. All cancellations and no-shows forfeit the full deposit. " +
        $"If running late, notify us through your reservation page before the normal {r.ArrivalGraceMinutes}-minute arrival deadline. " +
        $"Notified arrivals have up to {r.ExtendedArrivalGraceMinutes} minutes from the booked time; the dining end time does not change.\n\nRROMS Restaurant";

    // Called inside the booking transaction; SMTP sending is deferred until the transaction has committed.
    public static void QueueConfirmation(ApplicationDbContext context, Reservation r, bool modified)
    {
        if (r.Customer.IsDemo) return;
        context.ReservationEmails.Add(new ReservationEmail
        {
            Reservation = r,
            EventKey = $"confirmation:{r.ReservationID}:{r.ModificationCount}",
            Recipient = r.Customer.Email,
            Subject = (modified ? "Reservation rescheduled — " : "Reservation confirmed — ") + r.BookingReference,
            Body = Content(r, false),
            BookingStart = r.ReservationDate.Add(r.StartTime),
            DueAt = ReservationPolicy.Now
        });
    }

    // Queue creation is serialized and uniquely keyed per booking revision/reminder.
    public async Task QueueReminders(DateTime now)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var bookings = await db.Reservations.Include(r => r.Customer).Where(r => !r.Customer.IsDemo && r.ReservationStatus == "Confirmed" && r.DepositStatus == "Paid" && r.ReservationDate >= now.Date).ToListAsync();
        foreach (var r in bookings)
        {
            var start = r.ReservationDate.Add(r.StartTime);
            if (start <= now)
                continue;
            foreach (var hours in options.Value.ReminderHours.Where(h => h > 0 && h <= 168).Distinct())
            {
                var key = $"reminder:{r.ReservationID}:{r.ModificationCount}:{hours}";
                if (await db.ReservationEmails.AnyAsync(e => e.EventKey == key))
                    continue;
                var due = start.AddHours(-hours);
                // Do not send multiple overdue reminders for last-minute bookings.
                if (due < r.CreatedDate)
                    continue;
                db.ReservationEmails.Add(new ReservationEmail
                {
                    Reservation = r,
                    EventKey = key,
                    Recipient = r.Customer.Email,
                    Subject = "Arrival reminder — " + r.BookingReference,
                    Body = Content(r, true),
                    BookingStart = start,
                    DueAt = due
                });
            }
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    // Claims due messages for one worker, verifies the booking still qualifies, then sends/retries outside the booking transaction.
    public async Task Dispatch(DateTime now, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return;
        var ids = await db.ReservationEmails.AsNoTracking().Where(e =>
            (e.Status == "Queued" || (e.Status == "Sending" && e.LeaseUntil <= now)) && e.DueAt <= now)
            .OrderBy(e => e.DueAt).Select(e => e.ReservationEmailID).Take(20).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            var claimed = await db.ReservationEmails.Where(e => e.ReservationEmailID == id &&
                (e.Status == "Queued" || (e.Status == "Sending" && e.LeaseUntil <= now)))
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, "Sending").SetProperty(e => e.LeaseUntil, now.AddMinutes(5)), cancellationToken);
            if (claimed == 0)
                continue;
            var mail = await db.ReservationEmails.AsNoTracking().Include(e => e.Reservation).SingleAsync(e => e.ReservationEmailID == id, cancellationToken);
            var r = mail.Reservation;
            var invalid = r.ReservationStatus != "Confirmed" || r.DepositStatus != "Paid" ||
                r.ReservationDate.Add(r.StartTime) != mail.BookingStart || r.ReservationDate.Add(r.StartTime) <= now;
            if (invalid)
            {
                await db.ReservationEmails.Where(e => e.ReservationEmailID == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, "Skipped").SetProperty(e => e.LeaseUntil, (DateTime?)null), cancellationToken);
                continue;
            }
            try
            {
                await sender.SendAsync(mail.Recipient, mail.Subject, mail.Body, $"rroms-{id}@notifications.rroms.local", cancellationToken);
                await db.ReservationEmails.Where(e => e.ReservationEmailID == id).ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, "Sent").SetProperty(e => e.DeliveredAt, (DateTime?)ReservationPolicy.Now)
                    .SetProperty(e => e.LeaseUntil, (DateTime?)null).SetProperty(e => e.LastError, (string?)null), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Reservation email {EmailId} failed ({FailureType}); delivery will be retried.", id, ex.GetType().Name);
                var attempts = mail.Attempts + 1;
                await db.ReservationEmails.Where(e => e.ReservationEmailID == id).ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, attempts >= 5 ? "Failed" : "Queued")
                    .SetProperty(e => e.Attempts, attempts).SetProperty(e => e.DueAt, now.AddMinutes(Math.Pow(2, attempts)))
                    .SetProperty(e => e.LeaseUntil, (DateTime?)null).SetProperty(e => e.LastError, ex.GetType().Name), cancellationToken);
            }
        }
    }
}

public class ReservationEmailWorker(IServiceScopeFactory scopes, ILogger<ReservationEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ReservationEmailService>();
                await service.QueueReminders(ReservationPolicy.Now);
                await service.Dispatch(ReservationPolicy.Now, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Reservation email processing failed; will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
