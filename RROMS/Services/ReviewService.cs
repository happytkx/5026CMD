// Review queries and moderation shared by the admin, staff and public guest-review pages. Only reviews of Completed orders are ever listed; hidden reviews are excluded from public results.
using Microsoft.EntityFrameworkCore;
using RROMS.Data;

namespace RROMS.Services;

public record ReviewSummary(int Total, decimal Average, IReadOnlyList<int> RatingCounts, int HiddenCount = 0)
{
    // RatingCounts[0] is 1 star ... RatingCounts[4] is 5 stars.
    public int CountFor(int rating) => RatingCounts[rating - 1];
    public int PercentFor(int rating) => Total == 0 ? 0 : (int)Math.Round(CountFor(rating) * 100m / Total);
}

public record ReviewEntry(
    int ReviewID,
    int Rating,
    string Comment,
    DateTime CreatedAt,
    string CustomerName,
    string OrderReference,
    bool IsHidden = false,
    string? HiddenBy = null)
{
    // Public pages show first names only to protect customer privacy.
    public string FirstName => ReviewService.FirstName(CustomerName);
}

public record ReviewPage(IReadOnlyList<ReviewEntry> Items, int Total, int PageNumber, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public class ReviewService(ApplicationDbContext db)
{
    public const int PageSize = 10;

    public static string FirstName(string? fullName)
    {
        var parts = (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "Guest" : parts[0];
    }

    // Public callers never see hidden reviews; staff and administrators pass includeHidden to moderate them.
    private IQueryable<Models.Review> Visible(bool includeHidden) =>
        db.Reviews.AsNoTracking().Where(r => r.Order.Status == "Completed" && (includeHidden || !r.IsHidden));

    public async Task<ReviewSummary> GetSummaryAsync(bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var query = Visible(includeHidden);
        var grouped = await query.GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var counts = Enumerable.Range(1, 5).Select(star => grouped.FirstOrDefault(g => g.Rating == star)?.Count ?? 0).ToList();
        var total = counts.Sum();
        var average = total == 0 ? 0m : Math.Round(counts.Select((count, index) => count * (index + 1m)).Sum() / total, 1);
        var hidden = includeHidden ? await query.CountAsync(r => r.IsHidden, cancellationToken) : 0;
        return new ReviewSummary(total, average, counts, hidden);
    }

    // visibility: "visible" or "hidden" narrows moderator results; anything else lists both.
    public async Task<ReviewPage> GetPageAsync(int? rating, int pageNumber, bool includeHidden = false,
        string? visibility = null, CancellationToken cancellationToken = default)
    {
        var query = Visible(includeHidden);
        if (rating is >= 1 and <= 5)
            query = query.Where(r => r.Rating == rating);
        if (includeHidden && visibility == "hidden")
            query = query.Where(r => r.IsHidden);
        else if (includeHidden && visibility == "visible")
            query = query.Where(r => !r.IsHidden);
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        pageNumber = Math.Clamp(pageNumber, 1, pages);
        var rows = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReviewID)
            .Skip((pageNumber - 1) * PageSize).Take(PageSize)
            .Select(r => new ReviewEntry(r.ReviewID, r.Rating, r.Comment, r.CreatedAt, r.Customer.CustomerName,
                r.Order.Reference, r.IsHidden, r.HiddenBy))
            .ToListAsync(cancellationToken);
        return new ReviewPage(rows, total, pageNumber, PageSize);
    }

    // Hides or restores one review. Returns false when the review does not exist or its order is not Completed.
    public async Task<bool> SetHiddenAsync(int reviewId, bool hidden, string moderator, CancellationToken cancellationToken = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.ReviewID == reviewId && r.Order.Status == "Completed", cancellationToken);
        if (review is null)
            return false;
        if (review.IsHidden == hidden)
            return true;
        review.IsHidden = hidden;
        review.HiddenAt = hidden ? ReservationPolicy.Now : null;
        review.HiddenBy = hidden ? (moderator.Length > 100 ? moderator[..100] : moderator) : null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
