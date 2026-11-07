// Console verification runner: checks policy boundaries and, with --integration, creates an isolated temporary SQL database to verify migrations and workflows. SMTP uses a fake sender.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
// Reservation details must use the same staff-role hierarchy as the application's Staff policy.
foreach (var role in new[] { "Administrator", "Restaurant Staff", "Staff", "Customer" })
{
 var details = new RROMS.Pages.Reservations.DetailsModel(null!, null!, null!, new ConfigurationBuilder().Build());
 details.PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext {
  HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(
   new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role) }, "Test")) }
 };
 Check(details.IsStaff == (role != "Customer"), role + " reservation management access");
}
var now = ReservationPolicy.Now;
Check(ReservationPolicy.PaymentDeadline("Indoor", now.AddHours(2).AddMinutes(3), now) == now.AddMinutes(3), "Indoor hold capped at advance cutoff");
Check(ReservationPolicy.PaymentDeadline("VIP Room", now.AddHours(24).AddMinutes(4), now) == now.AddMinutes(4), "VIP hold capped at advance cutoff");
Check(ReservationPolicy.PaymentDeadline("Indoor", now.AddDays(1), now) == now.AddMinutes(10), "Normal hold remains ten minutes");
foreach (var area in new[] { "Indoor", "Outdoor", "VIP Room" }) {
 var hours = area == "VIP Room" ? 24 : 2;
 Check(ReservationPolicy.MeetsAdvance(area, now.AddHours(hours), now), area + " exact advance boundary allowed");
 Check(!ReservationPolicy.MeetsAdvance(area, now.AddHours(hours).AddTicks(-1), now), area + " below advance boundary rejected");
 Check(ReservationPolicy.MeetsAdvance(area, now.AddHours(hours).AddTicks(1), now), area + " above advance boundary allowed");
}
SimulatedDeposit.Validate("TNG", "Demo Guest", null, null, null, null, null);
SimulatedDeposit.Validate("Card", null, "Demo Guest", "4242424242424242", "12/30", "123", null);
var currentExpiry = ReservationPolicy.Now.ToString("MM/yy", System.Globalization.CultureInfo.InvariantCulture);
SimulatedDeposit.Validate("Card", null, "Test Guest", "5555-5555-5555-4444", currentExpiry, "9876", null);
SimulatedDeposit.Validate("Card", null, "Test Guest", "4111 1111 1111 1111", currentExpiry, "456", null);
Check(true, "Different card numbers, formatting and security codes accepted");
await Reject(() => { SimulatedDeposit.Validate("Card", null, "Test", "5555555555554444", "13/30", "123", null); return Task.CompletedTask; }, "Invalid expiry month rejected");
await Reject(() => { SimulatedDeposit.Validate("Card", null, "Test", "5555555555554444", "01/20", "123", null); return Task.CompletedTask; }, "Expired card rejected");
await Reject(() => { SimulatedDeposit.Validate("Card", null, "Test", "5555555555554444", currentExpiry, "ab3", null); return Task.CompletedTask; }, "Non-numeric security code rejected");
SimulatedDeposit.Validate("Bank Transfer", null, null, null, null, null, "DEMO-123");
await Reject(() => { SimulatedDeposit.Validate("Card", null, "Demo", "12345", "12/30", "123", null); return Task.CompletedTask; }, "Short card number rejected");
await Reject(() => { SimulatedDeposit.Validate("Bank Transfer", null, null, null, null, null, ""); return Task.CompletedTask; }, "Missing bank reference rejected");
await Reject(() => { SimulatedDeposit.Validate("TNG", "", null, null, null, null, null); return Task.CompletedTask; }, "Missing TNG account name rejected");
await Reject(() => { SimulatedDeposit.Validate("TNG", "0123456789", null, null, null, null, null); return Task.CompletedTask; }, "Phone number rejected as TNG account name");

Check(ReservationPolicy.Areas(1).SequenceEqual(new[] { "Indoor", "Outdoor" }), "1 guest area filter");
Check(ReservationPolicy.Areas(3).Length == 2 && ReservationPolicy.Areas(4).Length == 2 && ReservationPolicy.Areas(5).Length == 2 && ReservationPolicy.Areas(6).Length == 3 && ReservationPolicy.Areas(8).Length == 3, "3/4/5/6/8 guest boundaries");
Check(ReservationPolicy.Areas(9).SequenceEqual(new[] { "VIP Room" }) && ReservationPolicy.Areas(12).Length == 1, "9–12 VIP only");
Check(ReservationPolicy.Areas(0).Length == 0 && ReservationPolicy.Areas(13).Length == 0, "Invalid party sizes");
Check(ReservationPolicy.SuitableCapacity(1, "Indoor", 2) && ReservationPolicy.SuitableCapacity(2, "Outdoor", 4)
    && !ReservationPolicy.SuitableCapacity(1, "Indoor", 6), "Small parties restricted to 2–4 seats");
Check(ReservationPolicy.SuitableCapacity(3, "Indoor", 4) && ReservationPolicy.SuitableCapacity(4, "Indoor", 6)
    && !ReservationPolicy.SuitableCapacity(3, "Indoor", 8), "3–4 guests restricted to 4–6 seats");
Check(ReservationPolicy.SuitableCapacity(5, "Outdoor", 6) && ReservationPolicy.SuitableCapacity(6, "Indoor", 8)
    && !ReservationPolicy.SuitableCapacity(5, "Indoor", 10), "5–6 guests restricted to 6–8 seats");
Check(ReservationPolicy.SuitableCapacity(7, "Indoor", 8) && ReservationPolicy.SuitableCapacity(8, "Indoor", 8)
    && !ReservationPolicy.SuitableCapacity(7, "Indoor", 10) && !ReservationPolicy.SuitableCapacity(7, "Indoor", 6), "7–8 guests require 8 seats");
Check(ReservationPolicy.SuitableCapacity(6, "VIP Room", 12) && ReservationPolicy.SuitableCapacity(12, "VIP Room", 12)
    && !ReservationPolicy.SuitableCapacity(1, "VIP Room", 12), "VIP eligibility and actual room capacity preserved");
Check(ReservationPolicy.MinimumSpend(6, "VIP Room") == 2400 && ReservationPolicy.MinimumSpend(8, "VIP Room") == 2400 && ReservationPolicy.MinimumSpend(9, "VIP Room") == 3600, "VIP minimum spend tiers");
Check(ReservationPolicy.MinimumSpend(3, "Indoor") == 150, "Standard minimum spend");
var start = now.Date.AddDays(10).AddHours(18);
Check(ReservationPolicy.End(start, "Indoor", TimeSpan.FromHours(22)) == start.AddHours(2), "Standard duration 2 hours");
Check(ReservationPolicy.End(start, "VIP Room", TimeSpan.FromHours(22)) == start.Date.AddHours(22), "VIP ends at closing");
Check(!ReservationPolicy.Overlaps(start, start.AddHours(2), start.AddHours(2), start.AddHours(4)), "Adjacent bookings allowed");
Check(ReservationPolicy.Overlaps(start, start.AddHours(2), start.AddMinutes(119), start.AddHours(4)), "Partial overlap rejected");
Check(ReservationPolicy.CanModify(now.AddHours(168).AddTicks(1), 0, now), "Modification allowed outside the final 168 hours");
Check(!ReservationPolicy.CanModify(now.AddHours(168), 0, now), "Modification blocked at exactly 168 hours");
Check(!ReservationPolicy.CanModify(now.AddHours(168).AddTicks(-1), 0, now), "Modification blocked inside the final 168 hours");
Check(!ReservationPolicy.CanModify(now.AddDays(20), 1, now), "Second modification rejected");
Check(ReservationPolicy.IsExpired(now.AddMinutes(-10), false, now) && !ReservationPolicy.IsExpired(now.AddMinutes(-10), true, now), "Normal 10-minute expiry");
Check(ReservationPolicy.IsExpired(now.AddMinutes(-30), true, now), "Notified 30-minute expiry");
if (!args.Contains("--integration")) { Console.WriteLine($"{passed} policy checks passed. Use --integration for SQL Server workflow checks."); return; }

// This run owns a uniquely named database. Never use the application's real database.
var database = "RROMS_Sprint2_Test_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
if (Environment.GetEnvironmentVariable("RROMS_TEST_SERVER") is { Length: > 0 } testServer)
    connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection) { DataSource = testServer }.ConnectionString;
connection = (await DevelopmentLocalDb.Resolve(connection, true))!;
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
await using var db = new ApplicationDbContext(options);
try
{
    var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
    await migrator.MigrateAsync("20261011054116_InitialCreate");
    await db.Database.ExecuteSqlRawAsync("INSERT INTO RestaurantSettings (DepositPerPerson,ReservationDurationMinutes,MaxModificationCount,GracePeriod,LateArrivalPeriod) VALUES (50,150,1,10,30)");
    await migrator.MigrateAsync("20261030210000_TrackDishKitchenStatuses");
    await db.Database.ExecuteSqlRawAsync("""
        INSERT INTO Customers (CustomerName, Email, PhoneNo, Role, CreatedAt, FailedLoginCount, EmailConfirmed, SessionVersion, IsDemo)
        VALUES (N'Legacy administrator', N'legacy-admin@example.test', N'', N'Administrator', GETDATE(), 0, 1, 0, 0);
        INSERT INTO MenuCategories (Name) VALUES (N'Legacy category');
        INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, IsPopular, DietaryTags, ProteinType, SpiceLevel, Allergens)
        VALUES (N'Legacy dish', N'', 10, N'Legacy category', 1, 0, N'', N'Vegetarian', 0, N'');
        """);
    const string legacyAdminPassword = "Legacy-admin-2026!";
    var legacyHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Customer>().HashPassword(new Customer(), legacyAdminPassword);
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Customers SET PasswordHash={legacyHash} WHERE Email='legacy-admin@example.test'");
    var legacyStaffHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Customer>().HashPassword(new Customer(), "Staff123!");
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Customers (CustomerName,Email,PhoneNo,Role,CreatedAt,FailedLoginCount,EmailConfirmed,SessionVersion,IsDemo,PasswordHash) VALUES ('Legacy staff','staff@celeste.local','','Restaurant Staff',GETDATE(),0,1,0,0,{legacyStaffHash})");
    DbInitializer.Initialize(db);
    Check(await db.Administrators.AnyAsync(a => a.Email == "legacy-admin@example.test"), "Migration preserves and links legacy administrator accounts");
    Check(await db.MenuItems.AnyAsync(i => i.Name == "Legacy dish" && i.MenuCategory.Name == "Legacy category"), "Migration backfills category foreign keys without losing menu data");
    await AdministratorRelationshipChecks.Run(options, Check);
    await AdministratorAccountChecks.Run(options, Check);
    Check(!await db.Customers.AnyAsync(c => c.Role != Customer.CustomerRole), "Customers contains only customer accounts after migration");
    var migratedAdmin = await db.Administrators.SingleAsync(a => a.Email == "legacy-admin@example.test");
    Check(new Microsoft.AspNetCore.Identity.PasswordHasher<Administrator>().VerifyHashedPassword(migratedAdmin, migratedAdmin.PasswordHash, legacyAdminPassword) != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed,
        "Administrator migration preserves existing password hash");
    Check(await db.StaffAccounts.AnyAsync(a => a.Email == "staff@celeste.local" && a.PasswordHash == legacyStaffHash), "Legacy staff credentials move into StaffAccounts");


    var unusedColumns = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id WHERE (t.name='Customers' AND c.name='Password') OR (t.name='DiningAreas' AND c.name IN ('AreaName','Description')) OR (t.name='RestaurantSettings' AND c.name IN ('DepositPerPerson','ReservationDurationMinutes','MaxModificationCount','GracePeriod','LateArrivalPeriod')) OR (t.name='MenuItems' AND c.name='SpiceLevel')").SingleAsync();
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
    Check(await db.Payments.AnyAsync(p => p.ReservationID == booking.ReservationID && p.PaymentStatus == "Paid" && p.PaymentMethod == "Card" && p.PaymentReference.StartsWith("SIM-")), "Deposit receipt stores configured method and reference");
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
    var arrivalStart = ReservationPolicy.Now.AddMinutes(-5);
    arrival.ReservationDate = arrivalStart.Date;
    arrival.StartTime = arrivalStart.TimeOfDay;
    arrival.EndTime = TimeSpan.FromTicks(Math.Min(arrival.StartTime.Add(TimeSpan.FromHours(2)).Ticks, TimeSpan.TicksPerDay - 1));
    await db.SaveChangesAsync();
    await service.Change(arrival.ReservationID, null, true, "CheckIn");
    Check(arrival.ReservationStatus == "Seated" && arrival.OrderingEnabled && arrival.CheckedInAt != null, "Staff check-in activates ordering");
    await Reject(() => service.Change(arrival.ReservationID, null, true, "Complete"), "Completion blocked before settlement");
    db.Bills.Add(new Bill { ReservationID = arrival.ReservationID, BillStatus = "Paid", TotalBill = 100, MinSpend = 100, DepositApplied = 100 });
    await db.SaveChangesAsync();
    await service.Change(arrival.ReservationID, null, true, "Complete");
    Check(arrival.ReservationStatus == "Completed" && !arrival.OrderingEnabled, "Completion closes ordering");
    var noShow = await service.Create(customer.CustomerID, 2, "Indoor", start, null);
    await service.Change(noShow.ReservationID, customer.CustomerID, false, "Pay");
    noShow.ReservationDate = ReservationPolicy.Now.Date;
    noShow.ReservationDate = ReservationPolicy.Now.AddMinutes(-11).Date;
    noShow.StartTime = ReservationPolicy.Now.AddMinutes(-11).TimeOfDay;
    await db.SaveChangesAsync();
    await service.Expire();
    Check(noShow.ReservationStatus == "No-show" && noShow.DepositStatus == "Forfeited", "Automatic normal no-show forfeits deposit");
    await service.Change(noShow.ReservationID, null, true, "NoShow");
    Check(noShow.ReservationStatus == "No-show", "Repeated manual no-show succeeds after expiry");
    Check(await db.Notifications.AnyAsync(n => n.ReservationID == arrival.ReservationID && n.NotificationType == "Audit"), "Reservation actions create audit entries");
    var notified = await service.Create(customer.CustomerID, 2, "Indoor", start.AddDays(3), null);
    await service.Change(notified.ReservationID, customer.CustomerID, false, "Pay");
    await service.Change(notified.ReservationID, customer.CustomerID, false, "Late");
    notified.ReservationDate = ReservationPolicy.Now.Date;
    notified.ReservationDate = ReservationPolicy.Now.AddMinutes(-11).Date;
    notified.StartTime = ReservationPolicy.Now.AddMinutes(-11).TimeOfDay;
    await db.SaveChangesAsync();
    await service.Expire();
    Check(notified.ReservationStatus == "Confirmed", "Notified booking survives 10-minute deadline");
    notified.ReservationDate = ReservationPolicy.Now.AddMinutes(-31).Date;
    notified.StartTime = ReservationPolicy.Now.AddMinutes(-31).TimeOfDay;
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
    // Capacity limits and explicit staff exceptions use a separate future date.
    var capacityStart = start.AddDays(80);
    Check((await allocation.Available(1, "Indoor", capacityStart, capacityStart.AddHours(2)))
        .Select(t => t.Capacity).SequenceEqual(new[] { 2, 4 }), "Small party availability includes only suitable tables in smallest-first order");
    Check((await service.Slots(1, "Outdoor", capacityStart.Date)).Count == 0,
        "No arrival slots when only oversized tables exist in selected area");
    await Reject(() => service.Create(customer.CustomerID, 1, "Outdoor", capacityStart, null),
        "Customer cannot automatically book an oversized table");
    var smallBooking = await service.Create(customer.CustomerID, 1, "Indoor", capacityStart, null);
    await service.Change(smallBooking.ReservationID, customer.CustomerID, false, "Pay");
    var indoorArea = await db.DiningAreas.SingleAsync(a => a.AreaType == "Indoor");
    db.RestaurantTables.Add(new RROMS.Models.RestaurantTable { TableID = "TEST-LARGE", DiningAreaID = indoorArea.DiningAreaID, Capacity = 8, Status = "Available" });
    await db.SaveChangesAsync();
    await Reject(() => service.Change(smallBooking.ReservationID, null, true, "Reassign", tableId: "TEST-LARGE"),
        "Staff cannot silently reassign to oversized table");
    await Reject(() => service.Change(smallBooking.ReservationID, customer.CustomerID, false, "Reassign", tableId: "TEST-LARGE", allowLargerTable: true, reassignmentReason: "Exception"),
        "Customer cannot request staff capacity exception");
    await Reject(() => service.Change(smallBooking.ReservationID, null, true, "Reassign", tableId: "TEST-LARGE", allowLargerTable: true),
        "Larger table exception requires reason");
    await service.Change(smallBooking.ReservationID, null, true, "Reassign", tableId: "TEST-LARGE", allowLargerTable: true, reassignmentReason: "Accessible seating requested");
    Check(smallBooking.TableID == "TEST-LARGE" && await db.Notifications.AnyAsync(n => n.ReservationID == smallBooking.ReservationID && n.Message.Contains("Accessible seating requested")),
        "Staff exception succeeds and records reason");
    Check(!(await allocation.Available(1, "Indoor", capacityStart, capacityStart.AddHours(2), allowLargerTable: true)).Any(t => t.TableID == "TEST-LARGE"),
        "Capacity exception still excludes conflicting tables");
    var accountSender = new FakeEmailSender();
    var accountConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Accounts:PublicBaseUrl"] = "https://localhost:7096" }).Build();
    var accounts = new CustomerAccountService(db, new Microsoft.AspNetCore.Identity.PasswordHasher<Customer>(), accountSender,
        accountConfig, Microsoft.Extensions.Logging.Abstractions.NullLogger<CustomerAccountService>.Instance);
    const string accountPassword = "A-test-passphrase-2026";
    var registered = await accounts.Register("Registered Customer", "New.Account@example.test", "0123456789", accountPassword);
    Check(registered.CustomerID > 0 && registered.PasswordHash != accountPassword && registered.NormalizedEmail == "NEW.ACCOUNT@EXAMPLE.TEST",
        "Registration creates reservation-compatible customer with hashed password and normalized email");
    await Reject(() => accounts.Register("Duplicate", "new.account@EXAMPLE.test", "000", accountPassword), "Case-insensitive duplicate registration rejected");
    await Reject(() => accounts.Register("Legacy takeover", customer.Email, "000", accountPassword), "Signup cannot take over existing reservation customer");
    Check(await accounts.Authenticate(registered.Email, accountPassword) == null, "Unverified account cannot log in");
    Check(await accounts.SendVerification(registered, CancellationToken.None), "Verification email sent through fake transport");
    var confirmationUrl = new Uri(accountSender.Bodies.Last().Split('\n').Single(line => line.StartsWith("https://")));
    var confirmationQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(confirmationUrl.Query);
    var accountToken = confirmationQuery["token"].ToString();
    Check(registered.VerificationTokenHash != accountToken && accountToken.Length == 64 && confirmationUrl.Host == "localhost",
        "Verification stores token hash and uses configured origin");
    Check(!await accounts.SendVerification(registered, CancellationToken.None), "Verification resend has cooldown");
    Check(!await accounts.Confirm(registered.CustomerID, "invalid"), "Malformed verification token rejected");
    registered.VerificationExpiresUtc = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
    Check(!await accounts.Confirm(registered.CustomerID, accountToken), "Expired verification link rejected");
    registered.VerificationSentUtc = DateTime.UtcNow.AddMinutes(-2);
    await db.SaveChangesAsync(); await accounts.SendVerification(registered, CancellationToken.None);
    confirmationUrl = new Uri(accountSender.Bodies.Last().Split('\n').Single(line => line.StartsWith("https://")));
    var replacementToken = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(confirmationUrl.Query)["token"].ToString();
    Check(!await accounts.Confirm(registered.CustomerID, accountToken), "Resending invalidates previous verification token");
    Check(await accounts.Confirm(registered.CustomerID, replacementToken), "Valid verification confirms customer email");
    Check(!await accounts.Confirm(registered.CustomerID, replacementToken), "Verification link is single-use");
    db.ChangeTracker.Clear();
    Check(await accounts.Authenticate("new.account@EXAMPLE.test", accountPassword) != null, "Confirmed account signs in case-insensitively");
    for (var attempt = 0; attempt < 5; attempt++) await accounts.Authenticate(registered.Email, "IncorrectPassword");
    Check(await accounts.Authenticate(registered.Email, accountPassword) == null, "Repeated failed passwords lock the account temporarily");
    var lockedCustomer = await db.Customers.SingleAsync(c => c.CustomerID == registered.CustomerID);
    lockedCustomer.LockedUntilUtc = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
    Check(await accounts.Authenticate(registered.Email, accountPassword) != null, "Correct password works after lockout expires");
    var registeredBooking = await service.Create(registered.CustomerID, 2, "Indoor", start.AddDays(90), "Account integration test");
    Check(registeredBooking.CustomerID == registered.CustomerID, "New account can own a reservation without changing existing records");
    var resetAccount = await accounts.Register("Reset Customer", "reset-check@example.test", "0123456789", accountPassword);
    resetAccount.EmailConfirmed = true; await db.SaveChangesAsync();
    Check(!await accounts.SendPasswordReset("unknown@example.test", CancellationToken.None)
        && !await accounts.SendPasswordReset(customer.Email, CancellationToken.None), "Reset does not claim unknown or legacy accounts");
    Check(await accounts.SendPasswordReset(resetAccount.Email, CancellationToken.None), "Verified account receives password reset link through fake sender");
    string LastResetToken() => Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(accountSender.Bodies.Last()
        .Split('\n').Single(line => line.StartsWith("https://"))).Query)["token"].ToString();
    var oldResetToken = LastResetToken();
    Check(resetAccount.PasswordResetTokenHash != oldResetToken && oldResetToken.Length == 64, "Password reset stores only a cryptographic token hash");
    Check(!await accounts.SendPasswordReset(resetAccount.Email, CancellationToken.None), "Password reset requests have resend cooldown");
    Check(!await accounts.CanResetPassword(resetAccount.CustomerID, "bad-token")
        && !await accounts.CanResetPassword(registered.CustomerID, oldResetToken), "Invalid and cross-account reset tokens rejected");
    resetAccount.PasswordResetExpiresUtc = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
    Check(!await accounts.ResetPassword(resetAccount.CustomerID, oldResetToken, "New-test-passphrase-2026"), "Expired reset token cannot change password");
    resetAccount.PasswordResetSentUtc = DateTime.UtcNow.AddMinutes(-2); await db.SaveChangesAsync();
    await accounts.SendPasswordReset(resetAccount.Email, CancellationToken.None);
    var newResetToken = LastResetToken();
    Check(!await accounts.CanResetPassword(resetAccount.CustomerID, oldResetToken), "New reset request invalidates previous reset link");
    Check(!await accounts.ResetPassword(resetAccount.CustomerID, newResetToken, "short"), "Reset enforces password length on server");
    Check(await accounts.ResetPassword(resetAccount.CustomerID, newResetToken, "New-test-passphrase-2026"), "Valid reset replaces password");
    Check(!await accounts.ResetPassword(resetAccount.CustomerID, newResetToken, accountPassword), "Password reset link cannot be reused");
    db.ChangeTracker.Clear();
    Check(await accounts.Authenticate(resetAccount.Email, accountPassword) == null
        && await accounts.Authenticate(resetAccount.Email, "New-test-passphrase-2026") != null, "Only new password signs in after reset");
    Check(await db.Customers.AnyAsync(c => c.CustomerID == resetAccount.CustomerID && c.SessionVersion == 1 && c.PasswordResetTokenHash == null),
        "Reset consumes token and increments cookie session version");
    // Exercise the actual Razor endpoints and cookie/antiforgery integration against this test database.
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start(); var webPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var startInfo = new System.Diagnostics.ProcessStartInfo("dotnet") { UseShellExecute = false };
    foreach (var argument in new[] { typeof(ReservationService).Assembly.Location, "--contentRoot", Path.GetFullPath("RROMS"),
        "--urls", $"https://localhost:{webPort}", "--ConnectionStrings:DefaultConnection", connection,
        "--Email:Enabled", "false", "--DemoData:Enabled", "false", "--Logging:LogLevel:Default", "Critical" }) startInfo.ArgumentList.Add(argument);
    startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    using var webServer = System.Diagnostics.Process.Start(startInfo)!;
    try
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new System.Net.CookieContainer() })
            { BaseAddress = new Uri($"https://localhost:{webPort}"), Timeout = TimeSpan.FromSeconds(10) };
        HttpResponseMessage? loginResponse = null;
        for (var retry = 0; retry < 40; retry++)
        {
            try { loginResponse = await http.GetAsync("/Account/Login"); break; }
            catch (HttpRequestException) { if (webServer.HasExited) throw; await Task.Delay(250); }
        }
        Check(loginResponse?.StatusCode == System.Net.HttpStatusCode.OK, "Login Razor page starts over HTTPS");
        var loginHtml = await loginResponse!.Content.ReadAsStringAsync();
        string Antiforgery(string html) => System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var registerPage = await http.GetAsync("/Account/Register");
        var registerHtml = await registerPage.Content.ReadAsStringAsync();
        Check(registerPage.IsSuccessStatusCode && registerHtml.Contains("FullName"), "Registration page renders fields and restaurant styling");
        var invalidSignup = await http.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["FullName"] = "Web Customer", ["Email"] = "http-account@example.test", ["PhoneNo"] = "0123456789",
            ["Password"] = accountPassword, ["ConfirmPassword"] = "Different password",
            ["__RequestVerificationToken"] = Antiforgery(registerHtml) }));
        Check(invalidSignup.IsSuccessStatusCode && !await db.Customers.AnyAsync(c => c.Email == "http-account@example.test"),
            "Registration rejects mismatched passwords without creating account");
        registerHtml = await (await http.GetAsync("/Account/Register")).Content.ReadAsStringAsync();
        var signup = await http.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["FullName"] = "Web Customer", ["Email"] = "http-account@example.test", ["PhoneNo"] = "0123456789",
            ["Password"] = accountPassword, ["ConfirmPassword"] = accountPassword,
            ["__RequestVerificationToken"] = Antiforgery(registerHtml) }));
        Check(signup.StatusCode == System.Net.HttpStatusCode.Redirect && signup.Headers.Location!.ToString().Contains("VerifyEmail")
            && await db.Customers.AnyAsync(c => c.Email == "http-account@example.test" && !c.EmailConfirmed),
            "Signup saves unverified account and provides recovery when SMTP is disabled");
        Check((await http.GetAsync("/css/reservations.css")).IsSuccessStatusCode && (await http.GetAsync("/images/celeste-indoor.jpg")).IsSuccessStatusCode,
            "Account stylesheet and restaurant photograph load");
        var protectedPage = await http.GetAsync("/Reservations/Index");
        Check(protectedPage.StatusCode == System.Net.HttpStatusCode.Redirect && protectedPage.Headers.Location!.ToString().Contains("/Account/Login"),
            "Unauthenticated reservations redirect to real login");
        Check((await http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Email"] = registered.Email, ["Password"] = accountPassword }))).StatusCode == System.Net.HttpStatusCode.BadRequest,
            "Login submission without antiforgery token rejected");
        var loginPost = await http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Email"] = registered.Email, ["Password"] = accountPassword,
            ["ReturnUrl"] = "https://example.test/untrusted", ["__RequestVerificationToken"] = Antiforgery(loginHtml) }));
        Check(loginPost.StatusCode == System.Net.HttpStatusCode.Redirect && loginPost.Headers.Location!.ToString() == "/Reservations",
            "Login issues customer cookie and rejects external return URL");
        var ownList = await http.GetAsync("/Reservations/Index");
        var ownHtml = await ownList.Content.ReadAsStringAsync();
        Check(ownList.IsSuccessStatusCode && ownHtml.Contains(registeredBooking.BookingReference), "Authenticated customer sees own reservation");
        Check((await http.GetAsync($"/Reservations/Details/{booking.ReservationID}")).StatusCode == System.Net.HttpStatusCode.NotFound,
            "Customer cookie cannot access another customer's booking");
        var staffPage = await http.GetAsync("/Staff");
        Check(staffPage.StatusCode == System.Net.HttpStatusCode.Redirect && staffPage.Headers.Location!.ToString().Contains("/Account/Denied"),
            "Public registration cannot grant staff access");
        var forgotPage = await http.GetAsync("/Account/ForgotPassword");
        var forgotHtml = await forgotPage.Content.ReadAsStringAsync();
        Check(forgotPage.IsSuccessStatusCode && loginHtml.Contains("Forgot your password?"), "Forgot-password page and login link are available");
        var unknownReset = await http.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["AccountType"] = "Customer", ["Email"] = "nobody@example.test", ["__RequestVerificationToken"] = Antiforgery(forgotHtml) }));
        var knownReset = await http.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["AccountType"] = "Customer", ["Email"] = registered.Email, ["__RequestVerificationToken"] = Antiforgery(forgotHtml) }));
        Check(unknownReset.IsSuccessStatusCode && knownReset.IsSuccessStatusCode
            && (await unknownReset.Content.ReadAsStringAsync()).Contains("If this email belongs to an verified customer account")
            && (await knownReset.Content.ReadAsStringAsync()).Contains("If this email belongs to an verified customer account"),
            "Forgot-password response does not disclose whether an email is registered");
        db.ChangeTracker.Clear();
        await accounts.SendPasswordReset(registered.Email, CancellationToken.None);
        var webResetToken = LastResetToken();
        var resetPage = await http.GetAsync($"/Account/ResetPassword?id={registered.CustomerID}&token={webResetToken}");
        var resetHtml = await resetPage.Content.ReadAsStringAsync();
        Check(resetPage.IsSuccessStatusCode && resetHtml.Contains("Choose a new password")
            && resetPage.Headers.CacheControl!.NoStore, "Valid reset page renders without caching");
        var badReset = await http.PostAsync("/Account/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Id"] = registered.CustomerID.ToString(), ["Token"] = webResetToken,
            ["Password"] = "Reset-through-form-2026", ["ConfirmPassword"] = "Mismatch-password",
            ["__RequestVerificationToken"] = Antiforgery(resetHtml) }));
        Check(badReset.IsSuccessStatusCode && await accounts.CanResetPassword(registered.CustomerID, webResetToken),
            "Mismatched reset passwords do not consume valid link");
        var validReset = await http.PostAsync("/Account/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Id"] = registered.CustomerID.ToString(), ["Token"] = webResetToken,
            ["Password"] = "Reset-through-form-2026", ["ConfirmPassword"] = "Reset-through-form-2026",
            ["__RequestVerificationToken"] = Antiforgery(resetHtml) }));
        if (validReset.StatusCode != System.Net.HttpStatusCode.Redirect)
            Console.WriteLine("Reset response: " + validReset.StatusCode + " " + string.Join("; ", System.Text.RegularExpressions.Regex.Matches(await validReset.Content.ReadAsStringAsync(), "<li>([^<]+)</li>").Select(m => m.Groups[1].Value)));
        Check(validReset.StatusCode == System.Net.HttpStatusCode.Redirect && validReset.Headers.Location!.ToString().Contains("Login"),
            "Successful password reset redirects to sign in");
        Check((await http.GetAsync("/Reservations/Index")).StatusCode == System.Net.HttpStatusCode.Redirect,
            "Password reset invalidates existing signed-in customer cookie");
        loginHtml = await (await http.GetAsync("/Account/Login")).Content.ReadAsStringAsync();
        var newPasswordLogin = await http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Email"] = registered.Email, ["Password"] = "Reset-through-form-2026",
            ["__RequestVerificationToken"] = Antiforgery(loginHtml) }));
        Check(newPasswordLogin.StatusCode == System.Net.HttpStatusCode.Redirect, "Customer can sign in with reset password through login page");
        // Verify the merged Sprint 3 workflow against a Sprint 2 reservation.
        var menuResponse = await http.GetAsync("/Menu");
        var menuHtml = await menuResponse.Content.ReadAsStringAsync();
        Check(menuResponse.IsSuccessStatusCode, "Merged digital menu renders for reservation customer");
        var menuItem = await db.MenuItems.FirstAsync(i => i.IsAvailable);
        await http.PostAsync("/Menu?handler=Add", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["itemId"] = menuItem.MenuItemID.ToString(), ["__RequestVerificationToken"] = Antiforgery(menuHtml) }));
        var checkoutHtml = await (await http.GetAsync("/Orders/Checkout")).Content.ReadAsStringAsync();
        var beforeOrders = await db.Orders.CountAsync();
        await http.PostAsync("/Orders/Checkout?handler=Place", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["ReservationId"] = registeredBooking.ReservationID.ToString(), ["__RequestVerificationToken"] = Antiforgery(checkoutHtml) }));
        Check(await db.Orders.CountAsync() == beforeOrders, "Ordering before staff check-in is rejected");
        await db.Reservations.Where(r => r.ReservationID == registeredBooking.ReservationID)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.ReservationStatus, "Seated")
                .SetProperty(r => r.ReservationDate, ReservationPolicy.Now.Date));
        menuHtml = await (await http.GetAsync("/Menu")).Content.ReadAsStringAsync();
        await http.PostAsync("/Menu?handler=Add", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["itemId"] = menuItem.MenuItemID.ToString(), ["__RequestVerificationToken"] = Antiforgery(menuHtml) }));
        checkoutHtml = await (await http.GetAsync("/Orders/Checkout")).Content.ReadAsStringAsync();
        var placeOrder = await http.PostAsync("/Orders/Checkout?handler=Place", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["ReservationId"] = registeredBooking.ReservationID.ToString(), ["__RequestVerificationToken"] = Antiforgery(checkoutHtml) }));
        Check(placeOrder.StatusCode == System.Net.HttpStatusCode.Redirect && await db.Orders.AnyAsync(o =>
            o.ReservationID == registeredBooking.ReservationID && o.CustomerID == registered.CustomerID),
            "Checked-in reservation can place a linked dine-in order");
        Check((await http.GetAsync("/Orders")).IsSuccessStatusCode, "Customer order history renders after placing order");
        var receiptPayment = new Payment { ReservationID = registeredBooking.ReservationID, PaymentType = "Deposit", PaymentMethod = "TNG", PaymentReference = "TEST-RECEIPT", PaymentStatus = "Paid", Amount = 100, PaymentDate = ReservationPolicy.Now };
        db.Payments.Add(receiptPayment);
        await db.SaveChangesAsync();
        var receiptResponse = await http.GetAsync($"/Reservations/Receipt/{receiptPayment.PaymentID}");
        Check(receiptResponse.IsSuccessStatusCode && (await receiptResponse.Content.ReadAsStringAsync()).Contains("TEST-RECEIPT"), "Customer can view printable owned receipt");
        var otherPaymentId = await db.Payments.Where(p => p.Reservation.CustomerID != registered.CustomerID && p.PaymentStatus == "Paid").Select(p => p.PaymentID).FirstAsync();
        Check((await http.GetAsync($"/Reservations/Receipt/{otherPaymentId}")).StatusCode == System.Net.HttpStatusCode.NotFound, "Other customer's receipt is inaccessible");
        receiptPayment.PaymentStatus = "Failed"; await db.SaveChangesAsync();
        Check((await http.GetAsync($"/Reservations/Receipt/{receiptPayment.PaymentID}")).StatusCode == System.Net.HttpStatusCode.NotFound, "Failed payment cannot generate receipt");
        receiptPayment.PaymentStatus = "Paid"; await db.SaveChangesAsync();
        using (var streamTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        using (var liveResponse = await http.GetAsync("/Orders?handler=Updates", HttpCompletionOption.ResponseHeadersRead, streamTimeout.Token))
        using (var reader = new StreamReader(await liveResponse.Content.ReadAsStreamAsync(streamTimeout.Token)))
        {
            Check(liveResponse.Content.Headers.ContentType?.MediaType == "text/event-stream", "Order updates use an authenticated event stream");
            var line = await reader.ReadLineAsync(streamTimeout.Token);
            var ownedOrder = await db.Orders.SingleAsync(o => o.ReservationID == registeredBooking.ReservationID);
            Check(line != null && line.Contains("\"orderId\":" + ownedOrder.OrderID), "Live stream includes owned order");
            using (var snapshot = System.Text.Json.JsonDocument.Parse(line![6..]))
            {
                var streamedOrder = snapshot.RootElement.EnumerateArray().Single(o => o.GetProperty("orderId").GetInt32() == ownedOrder.OrderID);
                Check(streamedOrder.TryGetProperty("sessionStatus", out _) && streamedOrder.TryGetProperty("sessionTotal", out _), "Live stream includes grouped dining session status and total");
                Check(streamedOrder.GetProperty("lines").GetArrayLength() > 0, "Live stream includes individual dish statuses");
            }
            await db.Orders.Where(o => o.OrderID == ownedOrder.OrderID).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "Preparing"));
            string? update;
            do { update = await reader.ReadLineAsync(streamTimeout.Token); }
            while (update != null && !update.Contains("Preparing"));
            Check(update != null, "Status change arrives on existing stream without page reload");
        }
        using var adminHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new System.Net.CookieContainer() }) { BaseAddress = http.BaseAddress };
        var adminLoginHtml = await (await adminHttp.GetAsync("/Account/StaffLogin")).Content.ReadAsStringAsync();
        var adminLogin = await adminHttp.PostAsync("/Account/StaffLogin", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["Email"] = "legacy-admin@example.test", ["Password"] = legacyAdminPassword, ["__RequestVerificationToken"] = Antiforgery(adminLoginHtml) }));
        Check(adminLogin.StatusCode == System.Net.HttpStatusCode.Redirect && adminLogin.Headers.Location!.ToString() == "/Admin", "Migrated administrator signs in through staff/admin login");
        Check((await adminHttp.GetAsync("/Admin")).IsSuccessStatusCode && (await adminHttp.GetAsync("/Staff")).IsSuccessStatusCode, "Independent administrator retains admin and staff access");
        Check((await adminHttp.GetAsync("/Reservations/Create")).StatusCode == System.Net.HttpStatusCode.Redirect, "Administrator does not gain customer booking identity");
        foreach (var mergedPage in new[] { "/Reviews", "/Admin/CustomerReviews", "/Staff/CustomerReviews", "/Admin/TableManagement", "/Admin/MenuPerformance?handler=Export", "/Admin/Reports", "/Admin/PaymentCollections", "/Admin/Occupancy", "/Staff/Notifications" })
            Check((await adminHttp.GetAsync(mergedPage)).IsSuccessStatusCode, "Merged page renders for administrator: " + mergedPage);
        var adminProfile = await (await adminHttp.GetAsync("/Account/Profile")).Content.ReadAsStringAsync();
        Check(adminProfile.Contains("legacy-admin@example.test") && !adminProfile.Contains("name=\"Input.PhoneNo\""), "Administrator profile reads own account without customer fields");
        var adminSave = await adminHttp.PostAsync("/Account/Profile", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["Input.FullName"] = "Updated administrator", ["__RequestVerificationToken"] = Antiforgery(adminProfile) }));
        Check(adminSave.StatusCode == System.Net.HttpStatusCode.Redirect && await db.Administrators.AnyAsync(a => a.AdministratorID == migratedAdmin.AdministratorID && a.Name == "Updated administrator"), "Administrator can edit own name in Administrators");
        var deletableDish = new MenuItem { Name = "Delete test dish", Category = await db.MenuCategories.Select(c => c.Name).FirstAsync(), Price = 15 };
        db.MenuItems.Add(deletableDish); await db.SaveChangesAsync();
        var adminMenuHtml = await (await adminHttp.GetAsync("/Admin/MenuManagement")).Content.ReadAsStringAsync();
        Check(adminMenuHtml.Contains("handler=DeleteItem") && adminMenuHtml.Contains("This cannot be undone"), "Menu management displays confirmed deletion action");
        var noTokenDelete = await adminHttp.PostAsync("/Admin/MenuManagement?handler=DeleteItem", new FormUrlEncodedContent(new Dictionary<string,string> { ["itemId"] = deletableDish.MenuItemID.ToString() }));
        Check(noTokenDelete.StatusCode == System.Net.HttpStatusCode.BadRequest && await db.MenuItems.AnyAsync(i => i.MenuItemID == deletableDish.MenuItemID), "Dish deletion requires antiforgery token");
        var deleteDish = await adminHttp.PostAsync("/Admin/MenuManagement?handler=DeleteItem", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["itemId"] = deletableDish.MenuItemID.ToString(), ["__RequestVerificationToken"] = Antiforgery(adminMenuHtml) }));
        Check(deleteDish.StatusCode == System.Net.HttpStatusCode.Redirect && !await db.MenuItems.AnyAsync(i => i.MenuItemID == deletableDish.MenuItemID), "Administrator can delete a dish without order history");
        var referencedDishId = await db.OrderLines.Select(l => l.MenuItemID).FirstAsync();
        var originalOrderLineCount = await db.OrderLines.CountAsync(l => l.MenuItemID == referencedDishId);
        var blockedDelete = await adminHttp.PostAsync("/Admin/MenuManagement?handler=DeleteItem", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["itemId"] = referencedDishId.ToString(), ["__RequestVerificationToken"] = Antiforgery(adminMenuHtml) }));
        Check(blockedDelete.StatusCode == System.Net.HttpStatusCode.Redirect && await db.MenuItems.AnyAsync(i => i.MenuItemID == referencedDishId)
            && await db.OrderLines.CountAsync(l => l.MenuItemID == referencedDishId) == originalOrderLineCount, "Deleting an ordered dish preserves dish and order history");
        var blockedHtml = await (await adminHttp.GetAsync("/Admin/MenuManagement")).Content.ReadAsStringAsync();
        Check(blockedHtml.Contains("This dish is linked to orders and cannot be deleted"), "Blocked deletion explains the Disable alternative");
        var unknownDelete = await adminHttp.PostAsync("/Admin/MenuManagement?handler=DeleteItem", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["itemId"] = deletableDish.MenuItemID.ToString(), ["__RequestVerificationToken"] = Antiforgery(adminMenuHtml) }));
        Check(unknownDelete.StatusCode == System.Net.HttpStatusCode.NotFound, "Already deleted dish returns not found");
        await db.Administrators.Where(a => a.AdministratorID == migratedAdmin.AdministratorID).ExecuteUpdateAsync(u => u.SetProperty(a => a.SessionVersion, a => a.SessionVersion + 1));
        Check((await adminHttp.GetAsync("/Admin")).StatusCode == System.Net.HttpStatusCode.Redirect, "Administrator session version revokes stale cookies");
        using var staffHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new System.Net.CookieContainer() })
            { BaseAddress = http.BaseAddress };
        var staffLoginHtml = await (await staffHttp.GetAsync("/Account/StaffLogin")).Content.ReadAsStringAsync();
        var staffLogin = await staffHttp.PostAsync("/Account/StaffLogin", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Email"] = "staff@celeste.local", ["Password"] = "Staff123!", ["__RequestVerificationToken"] = Antiforgery(staffLoginHtml) }));
        Check(staffLogin.StatusCode == System.Net.HttpStatusCode.Redirect && (await staffHttp.GetAsync("/Staff")).IsSuccessStatusCode,
            "Incoming staff account can sign in and view reservation dashboard");
        Check((await staffHttp.GetAsync("/Staff/Orders")).IsSuccessStatusCode, "Staff can view the merged dine-in order queue");
        var staffMenuDelete = await staffHttp.PostAsync("/Admin/MenuManagement?handler=DeleteItem", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["itemId"] = referencedDishId.ToString(), ["__RequestVerificationToken"] = Antiforgery(staffLoginHtml) }));
        Check(staffMenuDelete.StatusCode == System.Net.HttpStatusCode.Redirect && await db.MenuItems.AnyAsync(i => i.MenuItemID == referencedDishId), "Staff cannot invoke administrator dish deletion");
        Check((await staffHttp.GetAsync("/Admin")).StatusCode == System.Net.HttpStatusCode.Redirect, "Staff cannot access administrator dashboard");
        ownHtml = await (await http.GetAsync("/Reservations/Index")).Content.ReadAsStringAsync();
        var logout = await http.PostAsync("/Access?handler=Logout", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = Antiforgery(ownHtml) }));
        Check(logout.StatusCode == System.Net.HttpStatusCode.Redirect && (await http.GetAsync("/Reservations/Index")).StatusCode == System.Net.HttpStatusCode.Redirect,
            "Logout clears the customer authentication cookie");

        var staffAccount = new StaffAccount { StaffName = "Shared Login Staff", Email = "shared-staff@example.test",
            NormalizedEmail = "SHARED-STAFF@EXAMPLE.TEST", IsActive = true };
        staffAccount.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<StaffAccount>().HashPassword(staffAccount, accountPassword);
        db.StaffAccounts.Add(staffAccount);
        await db.SaveChangesAsync();
        var internalLogin = await http.GetAsync("/Account/StaffLogin?ReturnUrl=%2FStaff");
        Check(internalLogin.IsSuccessStatusCode, "Staff login page renders with return URL");
        loginHtml = await (await http.GetAsync("/Account/StaffLogin")).Content.ReadAsStringAsync();
        var invalidStaffLogin = await http.PostAsync("/Account/StaffLogin", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Email"] = staffAccount.Email, ["Password"] = "Wrong-password",
            ["__RequestVerificationToken"] = Antiforgery(loginHtml) }));
        Check(invalidStaffLogin.IsSuccessStatusCode && (await invalidStaffLogin.Content.ReadAsStringAsync()).Contains("Invalid email or password."),
            "Staff login rejects invalid staff credentials");
        var sharedStaffLogin = await http.PostAsync("/Account/StaffLogin", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Email"] = staffAccount.Email, ["Password"] = accountPassword, ["RememberMe"] = "true",
            ["ReturnUrl"] = "https://example.test/untrusted", ["__RequestVerificationToken"] = Antiforgery(loginHtml) }));
        Check(sharedStaffLogin.StatusCode == System.Net.HttpStatusCode.Redirect && sharedStaffLogin.Headers.Location!.ToString() == "/Staff",
            "Staff login sends staff to dashboard and rejects external return URL");
        Check(sharedStaffLogin.Headers.GetValues("Set-Cookie").Any(value => value.Contains("expires=", StringComparison.OrdinalIgnoreCase)),
            "Staff login honors remember me for staff");
        Check((await http.GetAsync("/Staff")).IsSuccessStatusCode, "Staff login issues a valid staff session");
        Check((await http.GetAsync("/Admin")).StatusCode == System.Net.HttpStatusCode.Redirect,
            "Staff sign-in does not grant administrator access");
    }
    finally { if (!webServer.HasExited) { webServer.Kill(entireProcessTree: true); await webServer.WaitForExitAsync(); } }
    foreach (var method in new[] { "Card", "TNG", "Bank Transfer" })
    {
        var attempt = await service.Create(customer.CustomerID, 2, "Indoor", start.AddDays(40), null);
        await service.Change(attempt.ReservationID, customer.CustomerID, false, "Pay", paymentSuccess: false, paymentMethod: method);
        Check(attempt.ReservationStatus == "Pending", method + " failed attempt stays pending");
        await service.Change(attempt.ReservationID, customer.CustomerID, false, "Pay", paymentReference: "TEST-RECEIPT", paymentMethod: method);
        Check(attempt.ReservationStatus == "Confirmed" && await db.Payments.AnyAsync(p => p.ReservationID == attempt.ReservationID && p.PaymentMethod == method && p.PaymentStatus == "Paid"), method + " successful retry recorded");
        await service.Change(attempt.ReservationID, customer.CustomerID, false, "Cancel");
    }
    db.ChangeTracker.Clear();
    await DevelopmentDataSeeder.Seed(db, new ConfigurationBuilder().Build());
    var demoCustomers = await db.Customers.CountAsync(c => c.IsDemo);
    var demoBookings = await db.Reservations.CountAsync(r => r.BookingReference.StartsWith("DEMO-RES-"));
    Check(demoCustomers >= 184 && await db.Customers.CountAsync(c => c.IsDemo && c.Email.StartsWith("guest") && c.Email.EndsWith("@celeste.example.test")) == 180 && demoBookings > 0 && await db.StaffAccounts.AnyAsync(s => s.IsDemo),
        "Development seed creates fictional staff, customers and bookings");
    var annualBookings = await db.Reservations.Include(r => r.Customer)
        .Where(r => r.BookingReference.StartsWith("DEMO-RES-SEASON-")).ToListAsync();
    Check(annualBookings.Any(r => r.ReservationDate < ReservationPolicy.Now.Date) &&
        annualBookings.Any(r => r.ReservationDate > ReservationPolicy.Now.Date) &&
        annualBookings.All(r => r.ReservationDate <= ReservationPolicy.Now.Date.AddDays(28)) &&
        annualBookings.All(r => r.ReservationDate.Year == ReservationPolicy.Now.Year && r.Customer.IsDemo),
        "Seasonal reservation samples cover past and upcoming visits using only demo customers");
    Check(annualBookings.All(r => r.CreatedDate <= ReservationPolicy.Now) &&
        annualBookings.Where(r => r.ReservationDate.Add(r.StartTime) > ReservationPolicy.Now).All(r => r.ReservationStatus == "Confirmed"),
        "Annual bookings have valid creation dates and confirmed future visits");
    await DevelopmentDataSeeder.Seed(db, new ConfigurationBuilder().Build());
    Check(await db.Customers.CountAsync(c => c.IsDemo) == demoCustomers &&
        await db.Reservations.CountAsync(r => r.BookingReference.StartsWith("DEMO-RES-")) == demoBookings,
        "Repeated development seeding preserves existing records without duplicates");
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
