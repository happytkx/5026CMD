// Public guest reviews: ratings and comments from completed orders, shown with first names only.
using Microsoft.AspNetCore.Authorization;
using RROMS.Services;

namespace RROMS.Pages;

[AllowAnonymous]
public class ReviewsModel(ReviewService reviews) : ReviewListModel(reviews)
{
}
