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

        // Each order contains a snapshot of its original prices and
        // separately rounded tax. Do not recalculate tax at bill time.
        foreach (var order in orders)
        {
            if (order.Subtotal < 0m || order.Tax < 0m ||
                order.Total != order.Subtotal + order.Tax ||
                order.Subtotal != order.Lines.Sum(l => l.LineTotal) ||
                order.Lines.Any(l =>
                    l.Quantity <= 0 ||
                    l.UnitPrice < 0m ||
                    l.LineTotal != l.Quantity * l.UnitPrice))
            {
                throw new InvalidOperationException(
                    "An order contains inconsistent stored billing amounts.");
            }
        }

        var lines = orders
            .SelectMany(o => o.Lines)
            .Select(l => new OrderBillingLine(l.ItemName, l.Quantity, l.UnitPrice))
            .ToList();

        return new OrderBillingSnapshot(
            reservationId,
            orders.All(o => o.Status is "Served" or "Completed"),
            lines,
            orders.Sum(o => o.Subtotal),
            orders.Sum(o => o.Tax),
            orders.Sum(o => o.Total));
    }
}
