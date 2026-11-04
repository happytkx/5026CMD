// Administrator view of all customer ratings and comments with rating filter and averages (read-only).
using Microsoft.AspNetCore.Authorization;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class CustomerReviewsModel(ReviewService reviews) : ReviewListModel(reviews)
{
}
