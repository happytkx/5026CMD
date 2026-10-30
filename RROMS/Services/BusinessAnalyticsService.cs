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
            .Select(r => new { r.CustomerID, r.ReservationID, r.ReservationDate, r.PartySize })
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
        var lines = orders.SelectMany(o => o.Lines).ToList();

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

        var currentIds = currentBookings.Select(r => r.ReservationID).ToHashSet();
        decimal ordersPerReservation = currentIds.Count == 0 ? 0m :
            Math.Round((decimal)orders.Count(o => currentIds.Contains(o.ReservationID)) /
                currentIds.Count, 2);

        return new BusinessAnalyticsReport(
            currentCustomers.Count, firstTime, returning,
            averageGuests, ordersPerReservation, leastOrdered, categorySales);
    }
}
