// Staff dining-order queue: lists recent orders, advances their kitchen status through the permitted transitions and notifies the customer.
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

    // Statuses each status may move to; Completed and Cancelled are final.
    public static string[] NextStatuses(string status) => status switch
    {
        "Received" => ["Preparing", "Cancelled"],
        "Preparing" => ["Ready to Serve", "Cancelled"],
        "Ready to Serve" => ["Served", "Cancelled"],
        "Served" => ["Completed"],
        _ => []
    };

    public async Task OnGetAsync()
    {
        Orders = await db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.Lines).Include(o => o.Reservation)
            .OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync();
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
        order.Status = status;
        db.CustomerNotifications.Add(new CustomerNotification
        {
            CustomerID = order.CustomerID,
            Title = "Order status updated",
            Message = $"Your order {order.Reference} is now {status}.",
            Type = "Order",
            RelatedOrderID = order.OrderID,
            CreatedAt = ReservationPolicy.Now
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Order status updated and customer notified.";
        return RedirectToPage();
    }
}
