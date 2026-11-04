// Shared read-only review listing for the admin and staff workspaces; derived pages only add their own authorisation and wording.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Services;

namespace RROMS.Pages;

public abstract class ReviewListModel(ReviewService reviews) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Rating { get; set; }

    [BindProperty(Name = "p", SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public ReviewSummary Summary { get; private set; } = new(0, 0, [0, 0, 0, 0, 0]);
    public ReviewPage Reviews { get; private set; } = new([], 0, 1, ReviewService.PageSize);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (Rating is < 1 or > 5)
            Rating = null;
        Summary = await reviews.GetSummaryAsync(cancellationToken);
        Reviews = await reviews.GetPageAsync(Rating, PageNumber, cancellationToken);
        PageNumber = Reviews.PageNumber;
    }
}

// How a review board is presented: which page it belongs to and how much customer detail it may show.
public record ReviewBoardView(ReviewListModel Page, string PagePath, bool FullNames, bool ShowOrder, bool Public);
