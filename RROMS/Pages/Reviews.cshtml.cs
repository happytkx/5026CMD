using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages;

[AllowAnonymous]
public class ReviewsModel(ReviewService reviews, ApplicationDbContext db) : ReviewListModel(reviews)
{
    public List<CustomerOrder> ReviewableOrders { get; private set; } = [];
    public bool CanSubmitGeneralReview { get; private set; }
    public bool IsSignedInCustomer => User.IsInRole(Customer.CustomerRole);

    public override async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await base.OnGetAsync(cancellationToken);
        if (!IsSignedInCustomer) return;

        var customerId = ReservationIdentity.CustomerId(User);
        CanSubmitGeneralReview = !await db.Reviews.AnyAsync(r => r.CustomerID == customerId && r.OrderID == null, cancellationToken);
        ReviewableOrders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerID == customerId && o.Status == "Completed" &&
                !db.Reviews.Any(r => r.CustomerID == customerId && r.OrderID == o.OrderID))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostReviewAsync(int orderId, int rating, string? comment)
    {
        if (!IsSignedInCustomer)
            return Challenge();

        var customerId = ReservationIdentity.CustomerId(User);
        if (orderId < 0)
        {
            TempData["ReviewError"] = "Choose a valid review type.";
            return RedirectToPage();
        }
        if (orderId > 0)
        {
            var eligible = await db.Orders.AsNoTracking().AnyAsync(o => o.OrderID == orderId &&
                o.CustomerID == customerId && o.Status == "Completed");
            if (!eligible)
            {
                TempData["ReviewError"] = "Only your own completed orders can be reviewed.";
                return RedirectToPage();
            }
        }

        if (rating is < 1 or > 5 || string.IsNullOrWhiteSpace(comment) ||
            comment.Trim().Length > 800)
        {
            TempData["ReviewError"] = "Please choose a rating and write a review (maximum 800 characters).";
            return RedirectToPage();
        }

        if (await db.Reviews.AnyAsync(r => r.CustomerID == customerId && r.OrderID == (orderId == 0 ? (int?)null : orderId)))
        {
            TempData["ReviewError"] = orderId == 0 ? "You have already submitted general feedback." : "You have already reviewed this order.";
            return RedirectToPage();
        }

        db.Reviews.Add(new Review
        {
            CustomerID = customerId,
            OrderID = orderId == 0 ? null : orderId,
            Rating = rating,
            Comment = comment.Trim(),
            CreatedAt = ReservationPolicy.Now
        });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["ReviewError"] = "Your review could not be saved. Please check whether it was already submitted.";
            return RedirectToPage();
        }

        TempData["ReviewSuccess"] = "Thank you for sharing your experience at Céleste!";
        return RedirectToPage();
    }
}
