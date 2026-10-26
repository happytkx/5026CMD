
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class PaymentCollectionsModel : PageModel
{
    private readonly ReportingService _reportingService;

    public PaymentCollectionsModel(
        ReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; } =
        ReservationPolicy.Now.Date.AddDays(-30);

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; } =
        ReservationPolicy.Now.Date;

    public PaymentCollectionsResult? Report { get; set; }

    public async Task OnGetAsync()
    {
        if (StartDate.Date > EndDate.Date ||
            EndDate.Date == DateTime.MaxValue.Date)
        {
            ModelState.AddModelError(
                "",
                "Please select a valid reporting date range.");

            return;
        }

        Report = await _reportingService
            .GetPaymentCollectionsAsync(StartDate, EndDate);
    }
}
