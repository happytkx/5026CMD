// Supplies the billing module with a reservation's actual dine-in orders. Cancelled orders are ignored, and the snapshot is ready for billing only when every other order has been served.
using Microsoft.EntityFrameworkCore;
using RROMS.Data;

namespace RROMS.Services;

public class OrderBillingSource(ApplicationDbContext db) : ReservationOrderBillingSource
{
    public async Task<OrderBillingSnapshot?> GetSnapshotAsync(
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => o.ReservationID == reservationId && o.Status != "Cancelled")
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
            return null;

        var lines = orders
            .SelectMany(o => o.Lines)
            .Select(l => new OrderBillingLine(l.ItemName, l.Quantity, l.UnitPrice))
            .ToList();

        return new OrderBillingSnapshot(
            reservationId,
            orders.All(o => o.Status is "Served" or "Completed"),
            lines);
    }
}
