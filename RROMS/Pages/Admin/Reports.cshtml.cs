
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class ReportsModel : PageModel
{
    private readonly ReportingService _reportingService;
    private readonly BusinessAnalyticsService _businessAnalytics;

    public ReportsModel(ReportingService reportingService, BusinessAnalyticsService businessAnalytics)
    {
        _reportingService = reportingService;
        _businessAnalytics = businessAnalytics;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; } =
        ReservationPolicy.Now.Date.AddDays(-30);

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; } =
        ReservationPolicy.Now.Date;

    public ReservationReportSummary? Summary { get; set; }
    public BusinessAnalyticsReport? Insights { get; set; }

    public List<DailyReservationCount> DailyReservations
    { get; set; } = new();

    public List<DailyDepositCollection> DailyDeposits
    { get; set; } = new();

    // Keep charts legible across longer reporting periods while preserving the daily CSV.
    public record ReservationChartPoint(DateTime StartDate, DateTime EndDate, int Count);
    public List<ReservationChartPoint> TrendPoints { get; private set; } = [];
    public string TrendGranularity { get; private set; } = "day";

    private void PrepareTrendChart()
    {
        var days = DailyReservations;
        if (days.Count > 365)
        {
            TrendGranularity = "month";
            TrendPoints = days.GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => new ReservationChartPoint(g.First().Date, g.Last().Date, g.Sum(d => d.Count)))
                .ToList();
        }
        else if (days.Count > 45)
        {
            TrendGranularity = "week";
            TrendPoints = days.GroupBy(d => d.Date.AddDays(-(((int)d.Date.DayOfWeek + 6) % 7)))
                .OrderBy(g => g.Key)
                .Select(g => new ReservationChartPoint(g.First().Date, g.Last().Date, g.Sum(d => d.Count)))
                .ToList();
        }
        else
        {
            TrendGranularity = "day";
            TrendPoints = days.Select(d => new ReservationChartPoint(d.Date, d.Date, d.Count)).ToList();
        }
    }

    // Display reports on the website.
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

        Summary = await _reportingService
            .GetReservationSummaryAsync(StartDate, EndDate);

        DailyReservations = await _reportingService
            .GetDailyReservationTrendAsync(StartDate, EndDate);
        PrepareTrendChart();

        DailyDeposits = await _reportingService
            .GetDailyDepositCollectionAsync(StartDate, EndDate);
        Insights = await _businessAnalytics.GetReportAsync(StartDate, EndDate);
    }

    // Export the selected report to an Excel-compatible CSV file.
    public async Task<IActionResult> OnGetExportAsync()
    {
        DateTime from = StartDate.Date;
        DateTime to = EndDate.Date;

        // Prevent invalid date ranges.
        if (from > to || to == DateTime.MaxValue.Date)
        {
            return BadRequest(
                "Please select a valid reporting date range.");
        }

        // Use the same data sources as the displayed reports.
        var summary = await _reportingService
            .GetReservationSummaryAsync(from, to);

        var dailyReservations = await _reportingService
            .GetDailyReservationTrendAsync(from, to);

        var dailyDeposits = await _reportingService
            .GetDailyDepositCollectionAsync(from, to);
        var insights = await _businessAnalytics.GetReportAsync(from, to);

        var csv = new StringBuilder();

        // Escape CSV fields properly.
        void Row(params string[] values)
        {
            csv.AppendLine(string.Join(",",
                values.Select(value =>
                    "\"" + value.Replace("\"", "\"\"") + "\"")));
        }

        string Number(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        string Money(decimal value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture);

        string Date(DateTime value) =>
            value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Report heading and selected dates.
        Row("Celeste Reservation & Deposit Report");
        Row("Start Date", Date(from));
        Row("End Date", Date(to));
        Row();

        Row("Section", "Metric / Date", "Count", "Amount (RM)");

        // Reservation summary.
        Row("Reservation Summary", "Total Reservations",
            Number(summary.TotalReservations), "");

        Row("Reservation Status", "Pending",
            Number(summary.PendingReservations), "");

        Row("Reservation Status", "Confirmed",
            Number(summary.ConfirmedReservations), "");

        Row("Reservation Status", "Seated",
            Number(summary.SeatedReservations), "");

        Row("Reservation Status", "Completed",
            Number(summary.CompletedReservations), "");

        Row("Reservation Status", "Cancelled",
            Number(summary.CancelledReservations), "");

        Row("Reservation Status", "No-show",
            Number(summary.NoShowReservations), "");

        Row("Reservation Status", "Expired",
            Number(summary.ExpiredReservations), "");

        // Deposit totals based on scheduled reservation dates.
        Row("Deposit Summary - Reservation Date",
            "Paid Deposits", "",
            Money(summary.PaidDeposits));

        Row("Deposit Summary - Reservation Date",
            "Forfeited Deposits", "",
            Money(summary.ForfeitedDeposits));

        // Deposit collection totals based on actual payment dates.
        Row("Deposit Collections - Payment Date",
            "Successful Transactions",
            Number(dailyDeposits.Sum(d => d.PaymentCount)), "");

        Row("Deposit Collections - Payment Date",
            "Total Collected", "",
            Money(dailyDeposits.Sum(d => d.TotalAmount)));

        // Daily reservation trend.
        foreach (var day in dailyReservations)
        {
            Row("Daily Reservations - Scheduled Date",
                Date(day.Date),
                Number(day.Count), "");
        }

        // Daily successful deposit collections.
        foreach (var day in dailyDeposits)
        {
            Row("Daily Deposit Collections - Payment Date",
                Date(day.Date),
                Number(day.PaymentCount),
                Money(day.TotalAmount));
        }

        Row();
        Row("Customer Behaviour", "Active Customers", Number(insights.ActiveCustomers), "");
        Row("Customer Behaviour", "First-time Customers", Number(insights.NewCustomers), "");
        Row("Customer Behaviour", "Returning Customers", Number(insights.ReturningCustomers), "");
        Row("Customer Behaviour", "Average Party Size", insights.AveragePartySize.ToString("0.0", CultureInfo.InvariantCulture), "");
        Row("Customer Behaviour", "Orders Per Reservation", insights.OrdersPerReservation.ToString("0.00", CultureInfo.InvariantCulture), "");
        Row();
        // Menu performance is exported independently from its dedicated report.

        // Explain the different date rules.
        Row();
        Row("Notes",
            "Reservation counts use scheduled dining dates.");

        Row("Notes",
            "Deposit summary uses scheduled dining dates.");

        Row("Notes",
            "Forfeited deposits are included in paid deposits.");

        Row("Notes",
            "Daily deposit collections use payment dates.");

        // Add a UTF-8 BOM so Excel displays text correctly.
        byte[] content =
        [
            .. Encoding.UTF8.GetPreamble(),
            .. Encoding.UTF8.GetBytes(csv.ToString())
        ];

        string fileName =
            $"Celeste_Report_{Date(from)}_to_{Date(to)}.csv";

        return File(
            content,
            "text/csv; charset=utf-8",
            fileName);
    }
}
