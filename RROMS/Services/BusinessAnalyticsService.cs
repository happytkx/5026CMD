using Microsoft.EntityFrameworkCore;
using RROMS.Data;

namespace RROMS.Services;

// Aggregates existing reservation and order data; no new tables or personal details are stored.
public record MenuPerformanceRow(string Name, string Category, int Quantity, decimal Sales);
public record CategorySalesRow(string Category, int Quantity, decimal Sales);
public record BusinessAnalyticsReport(
    int ActiveCustomers,
    int NewCustomers,
    int ReturningCustomers,
    decimal AveragePartySize,
    decimal OrdersPerReservation,
    decimal AverageDiningSpendPerCompletedReservation,
    decimal AverageSpendPerGuest,
    int SettledCompletedReservations,
    List<MenuPerformanceRow> LeastOrdered,
    List<CategorySalesRow> CategorySales);

public class BusinessAnalyticsService(ApplicationDbContext db)
{
    public async Task<BusinessAnalyticsReport> GetReportAsync(DateTime startDate, DateTime endDate)
    {
        var from = startDate.Date;
        var to = endDate.Date;
        if (to < from || to == DateTime.MaxValue.Date)
            throw new ArgumentException("Invalid reporting period.");

        var endExclusive = to.AddDays(1);

        // Only meaningful reservations count as actual guest activity.
        var bookings = await db.Reservations.AsNoTracking()
            .Where(r => r.ReservationDate < endExclusive &&
                (r.ReservationStatus == "Confirmed" ||
                 r.ReservationStatus == "Seated" ||
                 r.ReservationStatus == "Completed"))
            .Select(r => new { r.CustomerID, r.ReservationID, r.ReservationDate, r.PartySize, r.ReservationStatus })
            .ToListAsync();

        var currentBookings = bookings
            .Where(r => r.ReservationDate >= from)
            .ToList();

        var currentCustomers = currentBookings
            .Select(r => r.CustomerID).Distinct().ToHashSet();

        var repeatCustomerIds = bookings.GroupBy(r => r.CustomerID)
            .Where(g => g.Count() >= 2).Select(g => g.Key).ToHashSet();

        int returning = currentCustomers.Count(id => repeatCustomerIds.Contains(id));
        int firstTime = currentCustomers.Count - returning;
        decimal averageGuests = currentBookings.Count == 0 ? 0m :
            Math.Round((decimal)currentBookings.Average(r => r.PartySize), 1);

        var menu = await db.MenuItems.AsNoTracking()
            .Select(i => new { i.MenuItemID, i.Name, i.Category })
            .ToListAsync();
        var menuCategories = menu.ToDictionary(i => i.MenuItemID, i => i.Category);

        // Order date drives sales/ordering patterns; exclude cancelled orders.
        var orders = await db.Orders.AsNoTracking().Include(o => o.Lines)
            .Where(o => o.CreatedAt >= from && o.CreatedAt < endExclusive &&
                        o.Status != "Cancelled")
            .ToListAsync();
        var lines = orders.SelectMany(o => o.Lines.Where(l => !l.IsCancelled)).ToList();

        var totalsByDish = lines.GroupBy(l => l.MenuItemID).ToDictionary(
            g => g.Key,
            g => new { Quantity = g.Sum(l => l.Quantity), Sales = g.Sum(l => l.LineTotal) });

        // Include zero-order dishes so underperformers are visible.
        var leastOrdered = menu.Select(item =>
        {
            totalsByDish.TryGetValue(item.MenuItemID, out var totals);
            return new MenuPerformanceRow(
                item.Name, item.Category, totals?.Quantity ?? 0, totals?.Sales ?? 0m);
        }).OrderBy(row => row.Quantity).ThenBy(row => row.Sales)
          .ThenBy(row => row.Name).Take(8).ToList();

        var categorySales = lines
            .GroupBy(l => menuCategories.GetValueOrDefault(l.MenuItemID, "Uncategorised"))
            .Select(g => new CategorySalesRow(
                g.Key, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
            .OrderByDescending(row => row.Sales).ToList();

        // Average spend is based on settled final bills attached to completed
        // dining visits. Deposits are already applied within TotalBill and
        // must not be added again as extra restaurant revenue.
        var completedIds = currentBookings
            .Where(r => r.ReservationStatus == "Completed")
            .Select(r => r.ReservationID).ToHashSet();
        var settledBills = await db.Bills.AsNoTracking()
            .Where(b => b.BillStatus == "Paid" && completedIds.Contains(b.ReservationID))
            .Select(b => new { b.ReservationID, b.TotalBill })
            .ToListAsync();
        var billedIds = settledBills.Select(b => b.ReservationID).ToHashSet();
        var billedVisits = currentBookings.Where(r => billedIds.Contains(r.ReservationID)).ToList();
        decimal totalBilled = settledBills.Sum(b => b.TotalBill);
        decimal spendPerReservation = billedVisits.Count == 0 ? 0m :
            Math.Round(totalBilled / billedVisits.Count, 2);
        int totalGuests = billedVisits.Sum(r => r.PartySize);
        decimal spendPerGuest = totalGuests == 0 ? 0m :
            Math.Round(totalBilled / totalGuests, 2);

        var currentIds = currentBookings.Select(r => r.ReservationID).ToHashSet();
        decimal ordersPerReservation = currentIds.Count == 0 ? 0m :
            Math.Round((decimal)orders.Count(o => currentIds.Contains(o.ReservationID)) /
                currentIds.Count, 2);

        return new BusinessAnalyticsReport(
            currentCustomers.Count, firstTime, returning,
            averageGuests, ordersPerReservation,
            spendPerReservation, spendPerGuest, billedVisits.Count,
            leastOrdered, categorySales);
    }
}
