// Restaurant staff view of customer feedback: read it, and hide or restore reviews for guests. No edit or delete.
using Microsoft.AspNetCore.Authorization;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class CustomerReviewsModel(ReviewService reviews) : ReviewModerationModel(reviews)
{
}
