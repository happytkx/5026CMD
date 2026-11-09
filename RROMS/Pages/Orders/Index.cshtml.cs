// Customer order history: the signed-in customer's own orders with live status, plus one review per completed order.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Orders;

[Authorize(Policy = "Customer")]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<CustomerOrder> Items { get; set; } = [];
    public HashSet<int> ReviewedOrderIds { get; set; } = [];

    public async Task OnGetAsync()
    {
        var id = ReservationIdentity.CustomerId(User);
        Items = await db.Orders.AsNoTracking().Include(o => o.Lines).Include(o => o.Reservation)
            .Where(o => o.CustomerID == id).OrderByDescending(o => o.CreatedAt).ToListAsync();
        ReviewedOrderIds = (await db.Reviews.Where(r => r.CustomerID == id && r.OrderID.HasValue).Select(r => r.OrderID!.Value).ToListAsync()).ToHashSet();
    }

    // Authenticated SSE snapshots catch changes and reconnects without reloading review forms.
    public async Task<IActionResult> OnGetUpdatesAsync(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        var id = ReservationIdentity.CustomerId(User);
        try
        {
            // End periodically so reconnect revalidates the authentication cookie.
            for (var tick = 0; tick < 30 && !cancellationToken.IsCancellationRequested; tick++)
            {
                var orders = await db.Orders.AsNoTracking().Include(o => o.Lines)
                    .Where(o => o.CustomerID == id).ToListAsync(cancellationToken);
                var statuses = orders.GroupBy(o => o.ReservationID).SelectMany(session =>
                    session.Select(o => new {
                        orderId = o.OrderID, status = o.Status,
                        roundStatus = DiningOrderPresentation.RoundStatus(o),
                        sessionStatus = DiningOrderPresentation.Status(session),
                        sessionCount = DiningOrderPresentation.ActiveItemCount(session),
                        sessionTotal = DiningOrderPresentation.Total(session),
                        lines = o.Lines.Select(line => new {
                            lineId = line.OrderLineID,
                            cancelled = line.IsCancelled || o.Status == "Cancelled",
                            status = line.IsCancelled || o.Status == "Cancelled" ? "Cancelled" : line.KitchenStatus,
                            total = line.LineTotal
                        })
                    }));
                await Response.WriteAsync("data: " + System.Text.Json.JsonSerializer.Serialize(statuses) + "\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        return new EmptyResult();
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
