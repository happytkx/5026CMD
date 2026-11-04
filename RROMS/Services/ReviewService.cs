// Read-only review queries shared by the admin, staff and public guest-review pages. Only reviews of Completed orders are ever listed.
using Microsoft.EntityFrameworkCore;
using RROMS.Data;

namespace RROMS.Services;

public record ReviewSummary(int Total, decimal Average, IReadOnlyList<int> RatingCounts)
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
    string OrderReference)
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

    private IQueryable<Models.Review> Visible() =>
        db.Reviews.AsNoTracking().Where(r => r.Order.Status == "Completed");

    public async Task<ReviewSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var grouped = await Visible().GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var counts = Enumerable.Range(1, 5).Select(star => grouped.FirstOrDefault(g => g.Rating == star)?.Count ?? 0).ToList();
        var total = counts.Sum();
        var average = total == 0 ? 0m : Math.Round(counts.Select((count, index) => count * (index + 1m)).Sum() / total, 1);
        return new ReviewSummary(total, average, counts);
    }

    public async Task<ReviewPage> GetPageAsync(int? rating, int pageNumber, CancellationToken cancellationToken = default)
    {
        var query = Visible();
        if (rating is >= 1 and <= 5)
            query = query.Where(r => r.Rating == rating);
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        pageNumber = Math.Clamp(pageNumber, 1, pages);
        var rows = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReviewID)
            .Skip((pageNumber - 1) * PageSize).Take(PageSize)
            .Select(r => new ReviewEntry(r.ReviewID, r.Rating, r.Comment, r.CreatedAt, r.Customer.CustomerName, r.Order.Reference))
            .ToListAsync(cancellationToken);
        return new ReviewPage(rows, total, pageNumber, PageSize);
    }
}
