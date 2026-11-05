// Administrator review moderation: remove inappropriate reviews and inspect customer profiles.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class CustomerReviewsModel(ReviewService reviews, ApplicationDbContext db) : ReviewListModel(reviews)
{
    public async Task<IActionResult> OnPostRemoveAsync(int reviewId)
    {
        var review = await db.Reviews.FindAsync(reviewId);
        if (review is null) return NotFound();
        db.Reviews.Remove(review);
        await db.SaveChangesAsync();
        TempData["ReviewModerationSuccess"] = "Review removed.";
        return RedirectToPage();
    }
}
