
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class OccupancyModel : PageModel
{
    private readonly OccupancyReportingService _service;

    public OccupancyModel(
        OccupancyReportingService service)
    {
        _service = service;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; } =
        ReservationPolicy.Now.Date.AddDays(-30);

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; } =
        ReservationPolicy.Now.Date;

    public OccupancyReport? Report { get; set; }

    public async Task OnGetAsync()
    {
        if (StartDate.Date > EndDate.Date ||
            EndDate.Date == DateTime.MaxValue.Date ||
            (EndDate.Date - StartDate.Date).Days > 89)
        {
            ModelState.AddModelError(
                "",
                "Please select a valid period of up to 90 days.");

            return;
        }

        Report = await _service.GetReportAsync(
            StartDate, EndDate);
    }
}
