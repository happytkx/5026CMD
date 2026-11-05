// Shared review listing: staff pages may include hidden reviews, the public page never does. Derived pages add their own authorisation and wording.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Services;

namespace RROMS.Pages;

public abstract class ReviewListModel(ReviewService reviews) : PageModel
{
    protected ReviewService Service { get; } = reviews;

    [BindProperty(SupportsGet = true)]
    public int? Rating { get; set; }

    // Moderator filter: all, visible or hidden. Ignored on the public page.
    [BindProperty(SupportsGet = true)]
    public string? Show { get; set; }

    [BindProperty(Name = "p", SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    protected virtual bool IncludeHidden => false;

    public ReviewSummary Summary { get; private set; } = new(0, 0, [0, 0, 0, 0, 0]);
    public ReviewPage Reviews { get; private set; } = new([], 0, 1, ReviewService.PageSize);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (Rating is < 1 or > 5)
            Rating = null;
        if (!IncludeHidden || Show is not ("visible" or "hidden"))
            Show = null;
        Summary = await Service.GetSummaryAsync(IncludeHidden, cancellationToken);
        Reviews = await Service.GetPageAsync(Rating, PageNumber, IncludeHidden, Show, cancellationToken);
        PageNumber = Reviews.PageNumber;
    }
}

// Admin and staff pages: lists hidden reviews too and lets the signed-in team hide or restore them. Reviews cannot be edited or deleted.
public abstract class ReviewModerationModel(ReviewService reviews) : ReviewListModel(reviews)
{
    protected override bool IncludeHidden => true;

    public async Task<IActionResult> OnPostVisibilityAsync(int id, bool hide, CancellationToken cancellationToken)
    {
        var moderator = User.Identity?.Name ?? "Staff";
        if (await Service.SetHiddenAsync(id, hide, moderator, cancellationToken))
            TempData["Success"] = hide ? "Review hidden. Guests can no longer see it." : "Review restored. Guests can see it again.";
        else
            TempData["Error"] = "That review could not be found.";
        return RedirectToPage(new { Rating, Show, p = PageNumber });
    }
}

// How a review board is presented: which page it belongs to, how much customer detail it may show and whether moderation controls appear.
public record ReviewBoardView(ReviewListModel Page, string PagePath, bool FullNames, bool ShowOrder, bool Public, bool Moderation = false);
