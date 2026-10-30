// Restaurant staff see one dining session per reservation, with individual kitchen rounds inside its details.
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class OrdersModel(ApplicationDbContext db) : PageModel
{
    public List<CustomerOrder> Orders { get; set; } = [];
    public HashSet<int> PaidReservationIds { get; set; } = [];

    // Kitchen rounds retain their own progress, even though the reservation displays as one session.
    public static string[] NextStatuses(string status) => status switch
    {
        "Received" => ["Preparing", "Cancelled"],
        "Preparing" => ["Ready to Serve", "Cancelled"],
        "Ready to Serve" => ["Served", "Cancelled"],
        "Served" => ["Completed"],
        _ => []
    };

    public static bool CanCancelDish(string status) =>
        status is "Received" or "Preparing" or "Ready to Serve";

    public async Task OnGetAsync()
    {
        // Choose recent reservations first, then retrieve all their rounds so
        // one dining session is never split across multiple screen entries.
        var recentReservationIds = (await db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .Select(o => o.ReservationID)
            .ToListAsync()).Distinct().ToList();
        Orders = await db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Lines)
            .Include(o => o.Reservation)
            .Where(o => recentReservationIds.Contains(o.ReservationID))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        var reservationIds = Orders.Select(o => o.ReservationID).Distinct().ToList();
        PaidReservationIds = (await db.Bills.AsNoTracking()
            .Where(b => reservationIds.Contains(b.ReservationID) && b.BillStatus == "Paid")
            .Select(b => b.ReservationID).ToListAsync()).ToHashSet();
    }

    public async Task<IActionResult> OnPostUpdateOrderAsync(int orderId, string status)
    {
        if (!CustomerOrder.Statuses.Contains(status))
            return BadRequest();

        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId);
        if (order == null)
            return NotFound();

        if (order.Status == status)
            return RedirectToPage();

        if (!NextStatuses(order.Status).Contains(status))
        {
            TempData["Error"] = $"Invalid status transition from {order.Status} to {status}.";
            return RedirectToPage();
        }

        if (status == "Cancelled" &&
            await db.Bills.AnyAsync(b => b.ReservationID == order.ReservationID && b.BillStatus == "Paid"))
        {
            TempData["Error"] = "This dining session has already been paid. The order cannot be cancelled.";
            return RedirectToPage();
        }

        order.Status = status;
        db.CustomerNotifications.Add(new CustomerNotification
        {
            CustomerID = order.CustomerID,
            Title = status == "Cancelled" ? "Order cancelled" : "Order status updated",
            Message = $"Dishes for your reservation are now {status}.",
            Type = "Order",
            RelatedOrderID = order.OrderID,
            CreatedAt = ReservationPolicy.Now
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Kitchen status updated and the customer notified.";
        return RedirectToPage();
    }

    // A cancellation retains the original line and its original price for audit,
    // but excludes its cost from current order totals and final payment.
    public async Task<IActionResult> OnPostCancelDishAsync(int orderLineId)
    {
        if (orderLineId <= 0)
            return BadRequest();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var line = await db.OrderLines
            .Include(l => l.Order)
            .ThenInclude(o => o.Reservation)
            .SingleOrDefaultAsync(l => l.OrderLineID == orderLineId);

        if (line == null)
            return NotFound();

        var order = line.Order;
        if (line.IsCancelled)
        {
            TempData["Error"] = "This dish has already been cancelled.";
            return RedirectToPage();
        }

        if (!CanCancelDish(order.Status) || order.Reservation.ReservationStatus != "Seated")
        {
            TempData["Error"] = "Only unserved dishes in an active seated reservation can be cancelled.";
            return RedirectToPage();
        }

        if (await db.Bills.AnyAsync(b => b.ReservationID == order.ReservationID && b.BillStatus == "Paid"))
        {
            TempData["Error"] = "The bill is already paid, so this dish cannot be cancelled.";
            return RedirectToPage();
        }

        var remaining = await db.OrderLines.AsNoTracking()
            .Where(l => l.OrderID == order.OrderID && l.OrderLineID != orderLineId && !l.IsCancelled)
            .ToListAsync();

        line.IsCancelled = true;
        line.CancelledAt = ReservationPolicy.Now;
        var staffName = User.Identity?.Name ?? "Restaurant Staff";
        line.CancelledBy = staffName.Length > 100 ? staffName[..100] : staffName;

        order.Subtotal = remaining.Sum(l => l.LineTotal);
        order.Tax = Math.Round(order.Subtotal * .06m, 2);
        order.Total = order.Subtotal + order.Tax;

        if (remaining.Count == 0)
            order.Status = "Cancelled";

        db.CustomerNotifications.Add(new CustomerNotification
        {
            CustomerID = order.CustomerID,
            Title = "Dish cancelled",
            Message = $"{line.ItemName} was cancelled from reservation {order.Reservation.BookingReference}. The bill has been updated.",
            Type = "Order",
            RelatedOrderID = order.OrderID,
            CreatedAt = line.CancelledAt.Value
        });

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "The cancellation could not be saved. Refresh the orders and try again.";
            return RedirectToPage();
        }

        TempData["Success"] =
            $"{line.ItemName} cancelled. The customer was notified. Refresh any saved draft bill before collecting payment.";
        return RedirectToPage();
    }
}
