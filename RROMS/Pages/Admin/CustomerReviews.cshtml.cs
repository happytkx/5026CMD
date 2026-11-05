// Administrator view of all customer ratings and comments: filter by rating, see averages, and hide or restore reviews for guests.
using Microsoft.AspNetCore.Authorization;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class CustomerReviewsModel(ReviewService reviews) : ReviewModerationModel(reviews)
{
}
