// Customer order history: the signed-in customer's own orders with live status, plus one review per completed order.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Orders;

[Authorize]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<CustomerOrder> Items { get; set; } = [];
    public HashSet<int> ReviewedOrderIds { get; set; } = [];

    public async Task OnGetAsync()
    {
        var id = ReservationIdentity.CustomerId(User);
        Items = await db.Orders.AsNoTracking().Include(o => o.Lines).Include(o => o.Reservation)
            .Where(o => o.CustomerID == id).OrderByDescending(o => o.CreatedAt).ToListAsync();
        ReviewedOrderIds = (await db.Reviews.Where(r => r.CustomerID == id).Select(r => r.OrderID).ToListAsync()).ToHashSet();
    }

    public async Task<IActionResult> OnPostReviewAsync(int orderId, int rating, string? comment)
    {
        var id = ReservationIdentity.CustomerId(User);
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderID == orderId && o.CustomerID == id && o.Status == "Completed");
        if (order == null)
            return NotFound();
        if (await db.Reviews.AnyAsync(r => r.CustomerID == id && r.OrderID == orderId))
        {
            TempData["Error"] = "You have already reviewed this order.";
            return RedirectToPage();
        }
        if (rating is < 1 or > 5 || string.IsNullOrWhiteSpace(comment) || comment.Trim().Length > 800)
        {
            TempData["Error"] = "Please provide a rating and feedback (maximum 800 characters).";
            return RedirectToPage();
        }
        db.Reviews.Add(new Review { CustomerID = id, OrderID = orderId, Rating = rating, Comment = comment.Trim(), CreatedAt = ReservationPolicy.Now });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The unique (customer, order) index rejected a double-submit.
            TempData["Error"] = "You have already reviewed this order.";
            return RedirectToPage();
        }
        TempData["Success"] = "Thank you for your feedback.";
        return RedirectToPage();
    }
}
