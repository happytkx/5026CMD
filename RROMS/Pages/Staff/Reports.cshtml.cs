using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Services;

namespace RROMS.Pages.Staff;

[Authorize(Policy = "Staff")]
public class ReportsModel : PageModel
{
    private readonly ReportingService _reportingService;
    
    public ReportsModel(ReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; } =
        ReservationPolicy.Now.Date.AddDays(-30);

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; } =
        ReservationPolicy.Now.Date;

    public ReservationReportSummary? Summary { get; set; }

    public List<DailyReservationCount> DailyReservations { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Reject invalid date ranges
        if (StartDate.Date > EndDate.Date)
        {
            ModelState.AddModelError(
                "",
                "End date cannot be earlier than start date.");

            return;
        }

        // Generate the report using ReportingService
        Summary = await _reportingService
                .GetReservationSummaryAsync(StartDate, EndDate);

        // Generate the daily reservation trend
        DailyReservations = await _reportingService
            .GetDailyReservationTrendAsync(StartDate, EndDate);
    }
}
