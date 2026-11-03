// Restaurant staff view of customer feedback; read-only, with no edit or delete handlers.
using Microsoft.AspNetCore.Authorization;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class CustomerReviewsModel(ReviewService reviews) : ReviewListModel(reviews)
{
}
