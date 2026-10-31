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
        // A broad, deterministic customer pool prevents hundreds of bookings being attributed to four people.
        // All accounts remain visibly fictional and use reserved example.test addresses.
        string[] firstNames = [
            "Amir", "Aina", "Alicia", "Arif", "Benjamin", "Chloe", "Daniel", "Daphne",
            "Emily", "Farah", "Grace", "Hafiz", "Hannah", "Imran", "Isabelle", "Jason",
            "Jia En", "Jun Wei", "Kavitha", "Li Wen", "Marcus", "Mei Ling", "Nadia", "Nora",
            "Priya", "Rachel", "Sarah", "Siti", "Wei Jie", "Yasmin"
        ];
        string[] surnames = ["Tan", "Lim", "Wong", "Lee", "Hassan", "Kumar"];
        for (var i = 0; i < firstNames.Length * surnames.Length; i++)
        {
            var email = $"guest{i + 1:D3}@celeste.example.test";
            var normalized = CustomerAccountService.Normalize(email);
            var customer = await db.Customers.SingleOrDefaultAsync(c =>
                c.NormalizedEmail == normalized || c.Email == email);
            if (customer == null)
            {
                customer = new Customer {
                    CustomerName = $"Demo {firstNames[i / surnames.Length]} {surnames[i % surnames.Length]}",
                    Email = email, NormalizedEmail = normalized,
                    PhoneNo = "019" + (1000000 + i).ToString("D7"),
                    CreatedAt = new DateTime(2025, 1, 1).AddDays((i * 11) % 365),
                    IsDemo = true, EmailConfirmed = true
                };
                customer.PasswordHash = new PasswordHasher<Customer>().HashPassword(
                    customer, config["DemoData:CustomerPassword"] ?? CustomerPassword);
                db.Customers.Add(customer);
            }
            if (customer.IsDemo) customers.Add(customer);
        }
        await db.SaveChangesAsync();
        if (config.GetValue<bool>("DemoData:RefreshLegacyCalendarFixtures"))
            await RemoveUnusedLegacyCalendarFixturesAsync(db);
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
        // V2 fixtures: deterministic variation by weekday and party size, not two visits every day.
        // New references cannot collide with old DEMO-RES-YEAR fixtures during a controlled refresh.
        var calendarRandom = new Random(2718 + now.Year);
        var yearStart = new DateTime(now.Year, 1, 1);
        (int Size, string Area)[] parties = [
            (2, "Indoor"), (2, "Outdoor"), (4, "Indoor"), (4, "Outdoor"),
            (6, "Indoor"), (8, "Indoor"), (6, "VIP Room"), (9, "VIP Room")
        ];
        for (var date = yearStart; date < yearStart.AddYears(1); date = date.AddDays(1))
        {
            var visits = date.DayOfWeek switch
            {
                DayOfWeek.Friday => 2 + calendarRandom.Next(3),
                DayOfWeek.Saturday => 3 + calendarRandom.Next(4),
                DayOfWeek.Sunday => 1 + calendarRandom.Next(4),
                _ => calendarRandom.Next(3)
            };
            for (var slot = 0; slot < visits; slot++)
            {
                var party = parties[calendarRandom.Next(parties.Length)];
                var visit = date.Add(settings.OpeningTime).AddMinutes((slot % 4) * 150);
                var visitEnd = ReservationPolicy.End(visit, party.Area, settings.ClosingTime);
                if (visit < now && visitEnd > now) continue;
                var outcome = calendarRandom.Next(100);
                var status = visit >= now ? "Confirmed" :
                    outcome < 86 ? "Completed" :
                    outcome < 94 ? "Cancelled" :
                    outcome < 98 ? "No-show" : "Expired";
                samples.Add(($"SEASON-{date:yyyyMMdd}-{slot + 1}", status, party.Size, party.Area, visit));
            }
        }
        var inventory = await db.RestaurantTables.Include(t => t.DiningArea).OrderBy(t => t.Capacity).ThenBy(t => t.TableID).ToListAsync();
        var menuByCategory = (await db.MenuItems.AsNoTracking()
            .Where(m => m.IsAvailable && m.Price > 0m)
            .OrderBy(m => m.MenuItemID).ToListAsync())
            .GroupBy(m => m.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
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
            var customer = customers[(index * 73) % customers.Count];
            index++;
            var paid = item.Status is not ("Pending" or "Expired");
            var booking = new Reservation { CustomerID = customer.CustomerID, TableID = table.TableID,
                BookingReference = reference, PartySize = item.Size, PreferredDiningOption = item.Area,
                ReservationDate = item.Start.Date, StartTime = item.Start.TimeOfDay, EndTime = end.TimeOfDay,
                OriginalStart = item.Start, CreatedDate = item.Start.AddDays(-8 - (index % 20)) < now ? item.Start.AddDays(-8 - (index % 20)) : now,
                DepositAmount = item.Size * 50m,
                MinimumSpend = ReservationPolicy.MinimumSpend(item.Size, item.Area), ReservationStatus = item.Status,
                DepositStatus = item.Status is "Cancelled" or "No-show" ? "Forfeited" : paid ? "Paid" : "Unpaid",
                HoldExpiresAt = item.Status == "Pending" ? (now.AddMinutes(10) < item.Start ? now.AddMinutes(10) : item.Start) : null,
                CheckedInAt = item.Status == "Seated" ? now : item.Status == "Completed" ? item.Start : null,
                CancellationDate = item.Status == "Cancelled" ? item.Start.AddDays(-2) : null,
                LateArrivalNotified = item.Status == "Seated" && now >= item.Start.AddMinutes(10),
                DietaryRequest = index % 11 == 0 ? "Vegetarian meal requested." :
                    index % 17 == 0 ? "Please note a peanut allergy." :
                    index % 13 == 0 ? "Birthday dinner, if possible." : null };
            db.Reservations.Add(booking);
            if (paid) db.Payments.Add(new Payment {
                Reservation = booking, PaymentType = "Deposit", PaymentMethod = "Card",
                PaymentReference = reference + "-PAY", Amount = booking.DepositAmount,
                PaymentStatus = "Paid", PaymentDate = booking.CreatedDate
            });
            db.Notifications.Add(new Notification {
                Reservation = booking, NotificationType = "Demo",
                Message = "Fictional development sample; no real payment or email.", SentDate = now
            });
            // Bill has only ReservationID (no navigation); persist its parent before billing.
            await db.SaveChangesAsync();

            // Historical visits have corresponding dishes, settled bills and collections.
            // This gives menu and revenue reporting credible sample inputs, not just bookings.
            if (item.Status == "Completed" && item.Key.StartsWith("SEASON-") &&
                menuByCategory.ContainsKey("Main Course"))
            {
                var orderRandom = new Random(item.Start.Year + item.Start.DayOfYear * 37 + index * 101);
                var chosen = new Dictionary<int, (MenuItem Dish, int Quantity)>();
                void AddDish(string category, bool premium = false)
                {
                    if (!menuByCategory.TryGetValue(category, out var options) || options.Count == 0) return;
                    var choices = premium ? options.OrderByDescending(m => m.Price).Take(2).ToList() : options;
                    var dish = choices[orderRandom.Next(choices.Count)];
                    if (chosen.TryGetValue(dish.MenuItemID, out var current))
                        chosen[dish.MenuItemID] = (dish, current.Quantity + 1);
                    else chosen[dish.MenuItemID] = (dish, 1);
                }

                var vip = item.Area == "VIP Room";
                for (var guest = 0; guest < item.Size; guest++)
                {
                    AddDish("Main Course", vip);
                    if (vip || orderRandom.Next(100) < 60) AddDish("Appetizers", vip);
                    if (vip || orderRandom.Next(100) < 38) AddDish("Soups", vip);
                    if (vip || orderRandom.Next(100) < 65) AddDish("Desserts", vip);
                    if (vip || orderRandom.Next(100) < 82) AddDish("Beverages", vip);
                }
                decimal Subtotal() => chosen.Values.Sum(v => v.Dish.Price * v.Quantity);
                // VIP rooms have a minimum spend of RM2,400/RM3,600. Extra shared
                // courses are plausible for group dining; no artificial price adjustments.
                var extras = 0;
                while (vip && Subtotal() < booking.MinimumSpend && extras < item.Size * 3)
                {
                    AddDish(extras % 2 == 0 ? "Appetizers" : "Main Course", premium: true);
                    extras++;
                }
                if (chosen.Count > 0)
                {
                    var subtotal = Subtotal();
                    var tax = Math.Round(subtotal * 0.06m, 2, MidpointRounding.AwayFromZero);
                    var total = subtotal + tax;
                    var order = new CustomerOrder {
                        ReservationID = booking.ReservationID, CustomerID = customer.CustomerID,
                        CreatedAt = item.Start.AddMinutes(20),
                        Status = "Completed",
                        Subtotal = subtotal, Tax = tax, Total = total,
                        Reference = $"DEMO{item.Start:MMdd}{index:D6}"
                    };
                    foreach (var entry in chosen.Values)
                        order.Lines.Add(new OrderLine {
                            MenuItemID = entry.Dish.MenuItemID, ItemName = entry.Dish.Name,
                            UnitPrice = entry.Dish.Price, Quantity = entry.Quantity,
                            LineTotal = entry.Dish.Price * entry.Quantity,
                            KitchenStatus = "Served"
                        });
                    db.Orders.Add(order);
                    var depositApplied = Math.Min(booking.DepositAmount, total);
                    var balance = total - depositApplied;
                    db.Bills.Add(new Bill {
                        ReservationID = booking.ReservationID, FoodSubtotal = subtotal, TaxAmount = tax,
                        TotalBill = total, MinSpend = booking.MinimumSpend,
                        DepositApplied = depositApplied, BalanceToPay = balance,
                        BillStatus = "Paid", CreatedAt = item.Start.AddHours(2)
                    });
                    if (balance > 0)
                        db.Payments.Add(new Payment {
                            Reservation = booking, PaymentType = "DiningBill",
                            PaymentMethod = index % 3 == 0 ? "Cash" : index % 3 == 1 ? "Card" : "EWallet",
                            PaymentReference = $"DEMO-BILL-{item.Start:MMdd}-{index:D6}",
                            Amount = balance, PaymentStatus = "Paid",
                            PaymentDate = item.Start.AddHours(2)
                        });
                }
            }
            await db.SaveChangesAsync();
            // A long reporting calendar should not keep thousands of saved entities tracked.
            db.ChangeTracker.Clear();
        }
        await transaction.CommitAsync();
    }
    // Explicit, one-time opt-in: remove only the original calendar fixtures from the old
    // seeder, and preserve any booking that someone has used for orders or other activity.
    // Never touch real customers, manually created bookings or the new SEASON fixtures.
    private static async Task RemoveUnusedLegacyCalendarFixturesAsync(ApplicationDbContext db)
    {
        var legacy = await db.Reservations.AsNoTracking()
            .Where(r => r.BookingReference.StartsWith("DEMO-RES-YEAR-") && r.Customer.IsDemo)
            .Select(r => new { r.ReservationID, r.BookingReference, r.ModificationCount, r.ReservationStatus })
            .ToListAsync();
        if (legacy.Count == 0) return;

        var ids = legacy.Select(r => r.ReservationID).ToList();
        var protectedIds = (await db.Orders.AsNoTracking()
            .Where(o => ids.Contains(o.ReservationID))
            .Select(o => o.ReservationID).ToListAsync()).ToHashSet();
        protectedIds.UnionWith(await db.Bills.AsNoTracking()
            .Where(b => ids.Contains(b.ReservationID))
            .Select(b => b.ReservationID).ToListAsync());
        protectedIds.UnionWith(await db.ReservationHistories.AsNoTracking()
            .Where(h => ids.Contains(h.ReservationID))
            .Select(h => h.ReservationID).ToListAsync());
        protectedIds.UnionWith(await db.ReservationEmails.AsNoTracking()
            .Where(e => ids.Contains(e.ReservationID))
            .Select(e => e.ReservationID).ToListAsync());

        foreach (var booking in legacy)
            if (booking.ModificationCount != 0 || booking.ReservationStatus is "Pending" or "Seated")
                protectedIds.Add(booking.ReservationID);
        var referenceById = legacy.ToDictionary(r => r.ReservationID, r => r.BookingReference);
        var payments = await db.Payments.Where(p => ids.Contains(p.ReservationID)).ToListAsync();
        foreach (var payment in payments)
            if (payment.PaymentType != "Demo deposit" ||
                payment.PaymentReference != referenceById[payment.ReservationID] + "-PAY")
                protectedIds.Add(payment.ReservationID);
        var notifications = await db.Notifications.Where(n => ids.Contains(n.ReservationID)).ToListAsync();
        foreach (var notification in notifications)
            if (notification.NotificationType != "Demo")
                protectedIds.Add(notification.ReservationID);

        var removableIds = ids.Where(id => !protectedIds.Contains(id)).ToHashSet();
        if (removableIds.Count == 0) return;
        db.Payments.RemoveRange(payments.Where(p => removableIds.Contains(p.ReservationID)));
        db.Notifications.RemoveRange(notifications.Where(n => removableIds.Contains(n.ReservationID)));
        var unusedReservations = await db.Reservations
            .Where(r => removableIds.Contains(r.ReservationID)).ToListAsync();
        db.Reservations.RemoveRange(unusedReservations);
        await db.SaveChangesAsync();
    }

}
