// Fictional, idempotent development fixtures. Program only calls this in Development with DemoData enabled.
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RROMS.Models;
using RROMS.Services;
namespace RROMS.Data;

public static class DevelopmentDataSeeder
{
    public const string StaffEmail = "staff@celeste.example.test";
    public const string StaffPassword = "CelesteStaff@2026!";
    public const string CustomerPassword = "CelesteGuest@2026!";
    public static async Task SeedTables(ApplicationDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var areas = await db.DiningAreas.ToListAsync();
        var existingIds = (await db.RestaurantTables.Select(t => t.TableID).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        (string Id, string Area, int Capacity)[] additions = [
            ("T04", "Indoor", 2), ("T05", "Indoor", 4), ("T06", "Indoor", 6), ("T07", "Indoor", 8),
            ("T08", "Outdoor", 2), ("T09", "Outdoor", 4), ("T10", "Outdoor", 8),
            ("VIP03", "VIP Room", 8), ("VIP04", "VIP Room", 12)];
        foreach (var table in additions)
        {
            // Preserve any existing table's area, capacity and availability.
            if (existingIds.Contains(table.Id)) continue;
            var area = areas.Single(a => a.AreaType == table.Area);
            db.RestaurantTables.Add(new RestaurantTable { TableID = table.Id, DiningAreaID = area.DiningAreaID,
                Capacity = table.Capacity, Status = "Available" });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public static async Task Seed(ApplicationDbContext db, IConfiguration config)
    {
        await SeedTables(db);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await db.StaffAccounts.AnyAsync(s => s.NormalizedEmail == CustomerAccountService.Normalize(StaffEmail)))
        {
            var staff = new StaffAccount { StaffName = "Céleste Demo Staff", Email = StaffEmail,
                NormalizedEmail = CustomerAccountService.Normalize(StaffEmail), IsDemo = true };
            staff.PasswordHash = new PasswordHasher<StaffAccount>().HashPassword(staff, config["DemoData:StaffPassword"] ?? StaffPassword);
            db.StaffAccounts.Add(staff);
        }
        var customers = new List<Customer>();
        string[] names = ["Demo Amy Tan", "Demo Daniel Lim", "Demo Mei Wong", "Demo Aisha Hassan"];
        for (var i = 0; i < names.Length; i++)
        {
            var email = $"customer{i + 1}@celeste.example.test";
            var normalized = CustomerAccountService.Normalize(email);
            var customer = await db.Customers.SingleOrDefaultAsync(c => c.NormalizedEmail == normalized || c.Email == email);
            if (customer == null)
            {
                customer = new Customer { CustomerName = names[i], Email = email, NormalizedEmail = normalized,
                    PhoneNo = "010000000" + (i + 1), IsDemo = true, EmailConfirmed = true };
                customer.PasswordHash = new PasswordHasher<Customer>().HashPassword(customer, config["DemoData:CustomerPassword"] ?? CustomerPassword);
                db.Customers.Add(customer);
            }
            if (customer.IsDemo) customers.Add(customer); // Never claim a real customer with a colliding address.
        }
        await db.SaveChangesAsync();
        if (customers.Count == 0) { await transaction.CommitAsync(); return; }
        var settings = await db.RestaurantSettings.OrderBy(s => s.SettingID).FirstAsync();
        var now = ReservationPolicy.Now;
        var open = now.Date.Add(settings.OpeningTime);
        var start = now < open ? open : new DateTime(((now.AddMinutes(30).Ticks + TimeSpan.TicksPerMinute * 30 - 1) /
            (TimeSpan.TicksPerMinute * 30)) * (TimeSpan.TicksPerMinute * 30), DateTimeKind.Unspecified);
        if (start.AddHours(2) > now.Date.Add(settings.ClosingTime)) start = open.AddDays(1);
        var samples = new List<(string Key, string Status, int Size, string Area, DateTime Start)> {
            ("01", "Confirmed", 2, "Indoor", start), ("02", "Confirmed", 4, "Indoor", start),
            ("03", "Confirmed", 6, "Outdoor", start), ("04", "Confirmed", 6, "VIP Room", start),
            ("05", "Confirmed", 9, "VIP Room", start),
            ("06", "Pending", 2, "Indoor", start.Date.AddDays(1).Add(settings.OpeningTime)),
            ("07", "Completed", 2, "Indoor", open.AddDays(-1)),
            ("08", "Cancelled", 6, "VIP Room", open.AddDays(-1).AddHours(1)),
            ("09", "No-show", 4, "Indoor", open.AddDays(-1).AddHours(2)),
            ("10", "Expired", 2, "Indoor", open.AddDays(-1).AddHours(4)),
            ("11", "Confirmed", 4, "Indoor", open.AddDays(10)),
            ("12", "Confirmed", 6, "VIP Room", open.AddDays(14)) };
        if (now >= open && now.AddHours(2) < now.Date.Add(settings.ClosingTime))
            samples.Insert(0, ("13", "Seated", 2, "Indoor", new DateTime(now.Ticks / (TimeSpan.TicksPerMinute * 30) * (TimeSpan.TicksPerMinute * 30))));
        // Stable date-based references make the full calendar year safe to seed repeatedly.
        var yearStart = new DateTime(now.Year, 1, 1);
        (int Size, string Area)[] parties = [(2, "Indoor"), (4, "Outdoor"), (6, "Indoor"),
            (8, "Outdoor"), (6, "VIP Room"), (9, "VIP Room")];
        for (var date = yearStart; date < yearStart.AddYears(1); date = date.AddDays(1))
        {
            for (var slot = 0; slot < 2; slot++)
            {
                var sequence = (date.DayOfYear - 1) * 2 + slot;
                var party = parties[sequence % parties.Length];
                var visit = date.Add(settings.OpeningTime).AddHours(slot * 3);
                var visitEnd = ReservationPolicy.End(visit, party.Area, settings.ClosingTime);
                // Do not invent an in-progress visit when seeding during opening hours.
                if (visit < now && visitEnd > now) continue;
                var status = visit >= now ? "Confirmed" : (sequence % 10) switch
                {
                    7 => "Cancelled",
                    8 => "No-show",
                    9 => "Expired",
                    _ => "Completed"
                };
                samples.Add(($"YEAR-{date:yyyyMMdd}-{slot + 1}", status, party.Size, party.Area, visit));
            }
        }
        var inventory = await db.RestaurantTables.Include(t => t.DiningArea).OrderBy(t => t.Capacity).ThenBy(t => t.TableID).ToListAsync();
        var index = 0;
        foreach (var item in samples)
        {
            var reference = "DEMO-RES-" + item.Key;
            if (await db.Reservations.AnyAsync(r => r.BookingReference == reference)) continue;
            var end = ReservationPolicy.End(item.Start, item.Area, settings.ClosingTime);
            if (item.Start.TimeOfDay < settings.OpeningTime || end.TimeOfDay > settings.ClosingTime || end <= item.Start) continue;
            var occupied = await db.Reservations.Where(r => r.ReservationDate == item.Start.Date &&
                (r.ReservationStatus == "Confirmed" || r.ReservationStatus == "Seated" || r.ReservationStatus == "Completed" ||
                (r.ReservationStatus == "Pending" && r.HoldExpiresAt > now))).ToListAsync();
            var table = inventory.FirstOrDefault(t => t.Status == "Available" && t.DiningArea.AreaType == item.Area &&
                ReservationPolicy.SuitableCapacity(item.Size, item.Area, t.Capacity) &&
                !occupied.Any(r => r.TableID == t.TableID && ReservationPolicy.Overlaps(item.Start, end,
                    r.ReservationDate.Add(r.StartTime), r.ReservationDate.Add(r.EndTime))));
            if (table == null) continue; // Preserve real bookings and skip any conflicting sample.
            var customer = customers[index++ % customers.Count];
            var paid = item.Status is not ("Pending" or "Expired");
            var booking = new Reservation { CustomerID = customer.CustomerID, TableID = table.TableID,
                BookingReference = reference, PartySize = item.Size, PreferredDiningOption = item.Area,
                ReservationDate = item.Start.Date, StartTime = item.Start.TimeOfDay, EndTime = end.TimeOfDay,
                OriginalStart = item.Start, CreatedDate = item.Start.AddDays(-10) < now ? item.Start.AddDays(-10) : now, DepositAmount = item.Size * 50m,
                MinimumSpend = ReservationPolicy.MinimumSpend(item.Size, item.Area), ReservationStatus = item.Status,
                DepositStatus = item.Status is "Cancelled" or "No-show" ? "Forfeited" : paid ? "Paid" : "Unpaid",
                HoldExpiresAt = item.Status == "Pending" ? (now.AddMinutes(10) < item.Start ? now.AddMinutes(10) : item.Start) : null,
                CheckedInAt = item.Status == "Seated" ? now : item.Status == "Completed" ? item.Start : null,
                CancellationDate = item.Status == "Cancelled" ? item.Start.AddDays(-1) : null,
                LateArrivalNotified = item.Status == "Seated" && now >= item.Start.AddMinutes(10),
                DietaryRequest = index % 2 == 0 ? "Dietary preference: No Pork. Allergy: peanuts. Sample request only." : "Vegetarian preference. Sample birthday visit." };
            db.Reservations.Add(booking);
            if (paid) db.Payments.Add(new Payment { Reservation = booking, PaymentType = "Demo deposit", PaymentMethod = "Demo",
                PaymentReference = reference + "-PAY", Amount = booking.DepositAmount, PaymentStatus = "Paid", PaymentDate = booking.CreatedDate });
            db.Notifications.Add(new Notification { Reservation = booking, NotificationType = "Demo", Message = "Fictional development sample; no real payment or email.", SentDate = now });
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }
}
