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
    [BindProperty(SupportsGet = true)] public int? Expanded { get; set; }

    // Each dish follows its own kitchen progression. The parent status is
    // the earliest outstanding step, so billing never treats a partially
    // served round as fully served.
    public static string? NextDishStatus(string status) => status switch
    {
        "Received" => "Preparing",
        "Preparing" => "Ready to Serve",
        "Ready to Serve" => "Served",
        _ => null
    };

    public static bool CanCancelDish(string status) =>
        status is "Received" or "Preparing" or "Ready to Serve";

    private static string DeriveRoundStatus(IEnumerable<OrderLine> lines)
    {
        var active = lines.Where(line => !line.IsCancelled).ToList();
        if (active.Count == 0) return "Cancelled";
        if (active.Any(line => line.KitchenStatus == "Received")) return "Received";
        if (active.Any(line => line.KitchenStatus == "Preparing")) return "Preparing";
        if (active.Any(line => line.KitchenStatus == "Ready to Serve")) return "Ready to Serve";
        if (active.Any(line => line.KitchenStatus == "Served")) return "Served";
        return "Completed";
    }

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

    public async Task<IActionResult> OnPostUpdateDishesAsync(int orderId, string status, int[]? lineIds)
    {
        if (orderId <= 0 || status is not ("Preparing" or "Ready to Serve" or "Served"))
            return BadRequest();

        var selectedIds = (lineIds ?? []).Distinct().ToArray();
        if (selectedIds.Length == 0)
        {
            TempData["Error"] = "Select at least one dish to update.";
            return RedirectToPage();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await db.Orders
            .Include(o => o.Lines)
            .Include(o => o.Reservation)
            .SingleOrDefaultAsync(o => o.OrderID == orderId);

        if (order == null)
            return NotFound();

        if (order.Reservation.ReservationStatus != "Seated" ||
            await db.Bills.AnyAsync(b => b.ReservationID == order.ReservationID && b.BillStatus == "Paid"))
        {
            TempData["Error"] = "Kitchen statuses cannot be changed after checkout or when the reservation is not seated.";
            return RedirectToPage(new { expanded = order.ReservationID });
        }

        var selectedLines = order.Lines.Where(l => selectedIds.Contains(l.OrderLineID)).ToList();
        if (selectedLines.Count != selectedIds.Length ||
            selectedLines.Any(l => l.IsCancelled || NextDishStatus(l.KitchenStatus) != status))
        {
            TempData["Error"] = "A selected dish is no longer eligible for that status. Refresh and try again.";
            return RedirectToPage(new { expanded = order.ReservationID });
        }

        foreach (var line in selectedLines)
            line.KitchenStatus = status;

        order.Status = DeriveRoundStatus(order.Lines);
        var names = string.Join(", ", selectedLines.Select(l => l.ItemName).Distinct());
        db.CustomerNotifications.Add(new CustomerNotification
        {
            CustomerID = order.CustomerID,
            Title = "Dish status updated",
            Message = $"Kitchen update: {names[..Math.Min(names.Length, 320)]} now {status}.",
            Type = "Order",
            RelatedOrderID = order.OrderID,
            CreatedAt = ReservationPolicy.Now
        });

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "The dish statuses could not be saved. Refresh and try again.";
            return RedirectToPage(new { expanded = order.ReservationID });
        }

        TempData["Success"] = $"{selectedLines.Count} dish(es) updated to {status}. The customer was notified.";
        return RedirectToPage(new { expanded = order.ReservationID });
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
            .ThenInclude(o => o.Lines)
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

        if (!CanCancelDish(line.KitchenStatus) || order.Reservation.ReservationStatus != "Seated")
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
        line.KitchenStatus = "Cancelled";
        line.CancelledAt = ReservationPolicy.Now;
        var staffName = User.Identity?.Name ?? "Restaurant Staff";
        line.CancelledBy = staffName.Length > 100 ? staffName[..100] : staffName;

        order.Subtotal = remaining.Sum(l => l.LineTotal);
        order.Tax = Math.Round(order.Subtotal * .06m, 2);
        order.Total = order.Subtotal + order.Tax;

        order.Status = DeriveRoundStatus(order.Lines);

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
        return RedirectToPage(new { expanded = order.ReservationID });
    }
}
