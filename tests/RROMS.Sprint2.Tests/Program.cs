// Console verification runner: checks policy boundaries and, with --integration, creates an isolated temporary SQL database to verify migrations and workflows. SMTP uses a fake sender.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition)
        throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
async Task Reject(Func<Task> operation, string name)
{
    try
    {
        await operation();
    }
    catch (InvalidOperationException) { Check(true, name); return; }
    throw new Exception("FAIL: expected rejection: " + name);
}
var now = ReservationPolicy.Now;
Check(ReservationPolicy.Areas(1).SequenceEqual(new[] { "Indoor", "Outdoor" }), "1 guest area filter");
Check(ReservationPolicy.Areas(3).Length == 2 && ReservationPolicy.Areas(4).Length == 2 && ReservationPolicy.Areas(5).Length == 2 && ReservationPolicy.Areas(6).Length == 3 && ReservationPolicy.Areas(8).Length == 3, "3/4/5/6/8 guest boundaries");
Check(ReservationPolicy.Areas(9).SequenceEqual(new[] { "VIP Room" }) && ReservationPolicy.Areas(12).Length == 1, "9–12 VIP only");
Check(ReservationPolicy.Areas(0).Length == 0 && ReservationPolicy.Areas(13).Length == 0, "Invalid party sizes");
Check(ReservationPolicy.MinimumSpend(6, "VIP Room") == 2400 && ReservationPolicy.MinimumSpend(8, "VIP Room") == 2400 && ReservationPolicy.MinimumSpend(9, "VIP Room") == 3600, "VIP minimum spend tiers");
Check(ReservationPolicy.MinimumSpend(3, "Indoor") == 150, "Standard minimum spend");
var start = now.Date.AddDays(10).AddHours(18);
Check(ReservationPolicy.End(start, "Indoor", TimeSpan.FromHours(22)) == start.AddHours(2), "Standard duration 2 hours");
Check(ReservationPolicy.End(start, "VIP Room", TimeSpan.FromHours(22)) == start.Date.AddHours(22), "VIP ends at closing");
Check(!ReservationPolicy.Overlaps(start, start.AddHours(2), start.AddHours(2), start.AddHours(4)), "Adjacent bookings allowed");
Check(ReservationPolicy.Overlaps(start, start.AddHours(2), start.AddMinutes(119), start.AddHours(4)), "Partial overlap rejected");
Check(ReservationPolicy.CanModify(now.AddHours(168), 0, now) && !ReservationPolicy.CanModify(now.AddHours(168).AddTicks(-1), 0, now), "Exact 168-hour boundary");
Check(!ReservationPolicy.CanModify(now.AddDays(20), 1, now), "Second modification rejected");
Check(ReservationPolicy.IsExpired(now.AddMinutes(-10), false, now) && !ReservationPolicy.IsExpired(now.AddMinutes(-10), true, now), "Normal 10-minute expiry");
Check(ReservationPolicy.IsExpired(now.AddMinutes(-30), true, now), "Notified 30-minute expiry");
if (!args.Contains("--integration")) { Console.WriteLine($"{passed} policy checks passed. Use --integration for SQL Server workflow checks."); return; }

// This run owns a uniquely named database. Never use the application's real database.
var database = "RROMS_Sprint2_Test_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
await using var db = new ApplicationDbContext(options);
try
{
    var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
    await migrator.MigrateAsync("20261011054116_InitialCreate");
    await db.Database.ExecuteSqlRawAsync("INSERT INTO RestaurantSettings (DepositPerPerson,ReservationDurationMinutes,MaxModificationCount,GracePeriod,LateArrivalPeriod) VALUES (50,150,1,10,30)");
    DbInitializer.Initialize(db);
    var unusedColumns = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id WHERE (t.name='Customers' AND c.name='Password') OR (t.name='DiningAreas' AND c.name IN ('AreaName','Description')) OR (t.name='RestaurantSettings' AND c.name IN ('DepositPerPerson','ReservationDurationMinutes','MaxModificationCount','GracePeriod','LateArrivalPeriod'))").SingleAsync();
    Check(unusedColumns == 0, "Unused columns removed by schema upgrade");
    var migratedSettings = await db.RestaurantSettings.FirstAsync();
    Check(migratedSettings.OpeningTime == TimeSpan.FromHours(12) && migratedSettings.ClosingTime == TimeSpan.FromHours(22), "Existing settings upgraded with usable opening hours");
    var service = new ReservationService(db, new TableAllocationService(db));
    var allocation = new TableAllocationService(db);
    var customer = await db.Customers.FirstAsync();
    var other = new Customer { CustomerName = "Other Customer", Email = "other@example.test", PhoneNo = "000" };
    db.Customers.Add(other);
    await db.SaveChangesAsync();
    var booking = await service.Create(customer.CustomerID, 2, "Indoor", start, "No peanuts");
    Check(booking.ReservationStatus == "Pending" && booking.DepositAmount == 100 && booking.TableID == "T01", "Create pending; smallest suitable table; deposit calculated");
    Check(booking.EndTime == TimeSpan.FromHours(20), "Persisted 2-hour booking");
    await Reject(() => service.Change(booking.ReservationID, other.CustomerID, false, "Cancel"), "Other customer cannot manage booking");
    await service.Change(booking.ReservationID, customer.CustomerID, false, "Pay", paymentSuccess: false);
    Check(booking.ReservationStatus == "Pending" && await db.Payments.CountAsync() == 1, "Failed payment records attempt; remains pending");
    Check(await db.ReservationEmails.CountAsync() == 0, "Failed deposit does not queue confirmation email");
    await service.Change(booking.ReservationID, customer.CustomerID, false, "Pay");
    Check(booking.ReservationStatus == "Confirmed" && booking.DepositStatus == "Paid", "Successful simulated deposit confirms booking");
    Check(await db.ReservationEmails.CountAsync() == 1, "Successful deposit atomically queues one confirmation email");
    Check(await db.Payments.AnyAsync(p => p.ReservationID == booking.ReservationID && p.PaymentStatus == "Paid" && p.PaymentMethod == "Simulation" && p.PaymentReference.StartsWith("SIM-")), "Simulated payment receipt stores method and reference");
    await Reject(() => service.Change(booking.ReservationID, customer.CustomerID, false, "Pay"), "Duplicate successful payment rejected");
    Check(await db.Payments.CountAsync() == 2, "Duplicate payment adds no record");
    await Reject(() => service.Change(booking.ReservationID, customer.CustomerID, false, "CheckIn"), "Customer cannot check in themselves");
    Check(!(await allocation.Available(2, "Indoor", start, start.AddHours(2))).Any(t => t.TableID == booking.TableID), "Booked table excluded from overlapping availability");
    Check((await allocation.Available(2, "Indoor", start.AddHours(2), start.AddHours(4))).Any(t => t.TableID == booking.TableID), "Table available for adjacent reservation");
    await service.Change(booking.ReservationID, customer.CustomerID, false, "Modify", start.AddDays(1));
    Check(booking.ModificationCount == 1 && booking.OriginalStart == start && booking.DepositAmount == 100 && booking.DepositStatus == "Paid" && await db.ReservationHistories.CountAsync() == 1, "Modify preserves original deadline and deposit; creates history");
    await Reject(() => service.Change(booking.ReservationID, null, true, "Modify", start.AddDays(2)), "Staff cannot override one-modification limit");
    await service.Change(booking.ReservationID, customer.CustomerID, false, "Cancel");
    Check(booking.ReservationStatus == "Cancelled" && booking.DepositStatus == "Forfeited", "Cancellation forfeits deposit");
    await Reject(() => service.Create(customer.CustomerID, 3, "VIP Room", start, null), "VIP denied for 1–3 guests");
    await Reject(() => service.Create(customer.CustomerID, 4, "VIP Room", start, null), "VIP denied for 4 guests");
    await Reject(() => service.Create(customer.CustomerID, 5, "VIP Room", start, null), "VIP denied for 5 guests");
    await Reject(() => service.Create(customer.CustomerID, 9, "Outdoor", start, null), "Non-VIP denied for 9–12 guests");
    await Reject(() => service.Create(customer.CustomerID, 2, "Indoor", start.Date.AddHours(21), null), "Standard booking cannot cross closing");
    var vip = await service.Create(customer.CustomerID, 9, "VIP Room", start, null);
    Check(vip.MinimumSpend == 3600 && vip.EndTime == TimeSpan.FromHours(22) && vip.TableID == "VIP02", "Large VIP reservation minimum and closing time");
    await Reject(() => service.Create(customer.CustomerID, 9, "VIP Room", start.AddHours(1), null), "VIP blocked through closing");
    await service.Change(vip.ReservationID, customer.CustomerID, false, "Pay");
    await Reject(() => service.Change(vip.ReservationID, null, true, "Reassign", tableId: "T03"), "Staff reassignment cannot cross area/capacity");
    await service.Change(vip.ReservationID, customer.CustomerID, false, "Late");
    Check(vip.LateArrivalNotified, "Late notification persisted");
    await service.Change(vip.ReservationID, customer.CustomerID, false, "Cancel");
    var expired = await service.Create(customer.CustomerID, 6, "VIP Room", start, null);
    expired.HoldExpiresAt = ReservationPolicy.Now.AddMinutes(-1);
    await db.SaveChangesAsync();
    await service.Expire();
    Check(expired.ReservationStatus == "Expired" && expired.DepositStatus == "Unpaid", "Unpaid hold expiry releases room without forfeiting unpaid deposit");
    var arrival = await service.Create(customer.CustomerID, 2, "Indoor", start, null);
    await service.Change(arrival.ReservationID, customer.CustomerID, false, "Pay");
    arrival.ReservationDate = ReservationPolicy.Now.Date;
    arrival.StartTime = ReservationPolicy.Now.TimeOfDay.Subtract(TimeSpan.FromMinutes(5));
    arrival.EndTime = arrival.StartTime.Add(TimeSpan.FromHours(2));
    await db.SaveChangesAsync();
    await service.Change(arrival.ReservationID, null, true, "CheckIn");
    Check(arrival.ReservationStatus == "Seated" && arrival.OrderingEnabled && arrival.CheckedInAt != null, "Staff check-in activates ordering");
    await service.Change(arrival.ReservationID, null, true, "Complete");
    Check(arrival.ReservationStatus == "Completed" && !arrival.OrderingEnabled, "Completion closes ordering");
    var noShow = await service.Create(customer.CustomerID, 2, "Indoor", start, null);
    await service.Change(noShow.ReservationID, customer.CustomerID, false, "Pay");
    noShow.ReservationDate = ReservationPolicy.Now.Date;
    noShow.StartTime = ReservationPolicy.Now.TimeOfDay.Subtract(TimeSpan.FromMinutes(11));
    await db.SaveChangesAsync();
    await service.Expire();
    Check(noShow.ReservationStatus == "No-show" && noShow.DepositStatus == "Forfeited", "Automatic normal no-show forfeits deposit");
    var notified = await service.Create(customer.CustomerID, 2, "Indoor", start.AddDays(3), null);
    await service.Change(notified.ReservationID, customer.CustomerID, false, "Pay");
    await service.Change(notified.ReservationID, customer.CustomerID, false, "Late");
    notified.ReservationDate = ReservationPolicy.Now.Date;
    notified.StartTime = ReservationPolicy.Now.TimeOfDay.Subtract(TimeSpan.FromMinutes(11));
    await db.SaveChangesAsync();
    await service.Expire();
    Check(notified.ReservationStatus == "Confirmed", "Notified booking survives 10-minute deadline");
    notified.StartTime = ReservationPolicy.Now.TimeOfDay.Subtract(TimeSpan.FromMinutes(31));
    await db.SaveChangesAsync();
    await service.Expire();
    Check(notified.ReservationStatus == "No-show", "Notified booking expires after 30 minutes");
    async Task<bool> ConcurrentBooking()
    {
        await using var concurrentDb = new ApplicationDbContext(options);
        var concurrentService = new ReservationService(concurrentDb, new TableAllocationService(concurrentDb));
        try
        {
            await concurrentService.Create(customer.CustomerID, 12, "VIP Room", start.AddDays(40), null);
            return true;
        }
        catch (InvalidOperationException) { return false; }
        catch (DbUpdateException) { return false; }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1205) { return false; }
    }
    var race = await Task.WhenAll(ConcurrentBooking(), ConcurrentBooking());
    Check(race.Count(success => success) == 1 && await db.Reservations.CountAsync(r => r.ReservationDate == start.AddDays(40).Date && r.TableID == "VIP02") == 1,
        "Simultaneous requests cannot double-book a VIP room");
    var emailBooking = await service.Create(customer.CustomerID, 6, "VIP Room", start.AddDays(60), null);
    await service.Change(emailBooking.ReservationID, customer.CustomerID, false, "Pay");
    var fakeSender = new FakeEmailSender();
    var emailOptions = Microsoft.Extensions.Options.Options.Create(new EmailOptions { Enabled = true, ReminderHours = [24] });
    var emailService = new ReservationEmailService(db, fakeSender, emailOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<ReservationEmailService>.Instance);
    await emailService.QueueReminders(ReservationPolicy.Now);
    await emailService.QueueReminders(ReservationPolicy.Now);
    var reminderKey = $"reminder:{emailBooking.ReservationID}:0:24";
    Check(await db.ReservationEmails.CountAsync(e => e.EventKey == reminderKey) == 1, "Reminder scheduling is idempotent");
    await emailService.Dispatch(ReservationPolicy.Now);
    Check(fakeSender.Bodies.Any(body => body.Contains(emailBooking.BookingReference) && body.Contains("RM2,400.00") && body.Contains("UTC+8")), "Confirmation email contains booking, VIP policy and Malaysia time");
    Check(!fakeSender.Bodies.Any(body => body.Contains(booking.BookingReference)), "Cancelled booking emails are suppressed");
    var sentBefore = fakeSender.Bodies.Count;
    await emailService.Dispatch(ReservationPolicy.Now);
    Check(fakeSender.Bodies.Count == sentBefore, "Sent confirmations are not resent on next poll");
    var dueAt = emailBooking.ReservationDate.Add(emailBooking.StartTime).AddHours(-24);
    await emailService.Dispatch(dueAt.AddSeconds(-1));
    Check(!fakeSender.Bodies.Any(body => body.Contains("look forward") && body.Contains(emailBooking.BookingReference)), "Arrival reminder is not sent before its due time");
    fakeSender.FailNext = true;
    await emailService.Dispatch(dueAt);
    var retryMail = await db.ReservationEmails.AsNoTracking().SingleAsync(e => e.EventKey == reminderKey);
    Check(retryMail.Status == "Queued" && retryMail.Attempts == 1 && retryMail.DueAt > dueAt, "SMTP failure records retry without changing booking");
    await emailService.Dispatch(retryMail.DueAt);
    Check(await db.ReservationEmails.AsNoTracking().AnyAsync(e => e.EventKey == reminderKey && e.Status == "Sent"), "Arrival reminder is delivered after retry");
    await service.Change(emailBooking.ReservationID, customer.CustomerID, false, "Modify", start.AddDays(61));
    await emailService.QueueReminders(ReservationPolicy.Now);
    Check(await db.ReservationEmails.AnyAsync(e => e.EventKey == $"reminder:{emailBooking.ReservationID}:1:24" && e.BookingStart == start.AddDays(61)), "Rescheduling creates a reminder for the updated booking");
    var stopped = new ReservationEmailService(db, fakeSender, Microsoft.Extensions.Options.Options.Create(new EmailOptions { Enabled = false }), Microsoft.Extensions.Logging.Abstractions.NullLogger<ReservationEmailService>.Instance);
    sentBefore = fakeSender.Bodies.Count;
    await stopped.Dispatch(ReservationPolicy.Now);
    Check(fakeSender.Bodies.Count == sentBefore, "Disabled SMTP never sends messages");
    Console.WriteLine($"All {passed} checks passed against SQL Server.");
}

finally
{
    if (!database.StartsWith("RROMS_Sprint2_Test_"))
        throw new Exception("Invalid test cleanup target.");
    await db.Database.EnsureDeletedAsync();
}

class FakeEmailSender : IEmailSender
{
    public List<string> Bodies { get; } = [];
    public bool FailNext
    {
        get; set;
    }
    public Task SendAsync(string recipient, string subject, string body, string messageId, CancellationToken cancellationToken)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new IOException("Simulated SMTP outage");
        }
        Bodies.Add(body);
        return Task.CompletedTask;
    }
}

