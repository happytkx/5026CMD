
using System.Globalization;
using System.Text;
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

    private bool IsValidPeriod()
    {
        return StartDate.Date <= EndDate.Date &&
               EndDate.Date != DateTime.MaxValue.Date &&
               (EndDate.Date - StartDate.Date).Days <= 89;
    }

    // Display the report in the browser.
    public async Task OnGetAsync()
    {
        if (!IsValidPeriod())
        {
            ModelState.AddModelError(
                "",
                "Please select a valid period of up to 90 days.");

            return;
        }

        Report = await _service.GetReportAsync(
            StartDate, EndDate);
    }

    // Download the report as an Excel-compatible CSV.
    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!IsValidPeriod())
        {
            return BadRequest(
                "Please select a valid period of up to 90 days.");
        }

        var report = await _service.GetReportAsync(
            StartDate, EndDate);

        var csv = new StringBuilder();

        // Escape special CSV characters and prevent
        // spreadsheet formulas in text fields.
        static string EscapeCsv(string value)
        {
            if (value.Length > 0 &&
                "=+-@\t\r".Contains(value[0]))
            {
                value = "'" + value;
            }

            return "\"" +
                value.Replace("\"", "\"\"") + "\"";
        }

        void Row(params string[] values)
        {
            csv.AppendLine(
                string.Join(",", values.Select(EscapeCsv)));
        }

        string Number(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        string Amount(decimal value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture);

        string Date(DateTime value) =>
            value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Row("Celeste Table Utilisation and Peak Hours");
        Row("Start Date", Date(report.StartDate));
        Row("End Date", Date(report.EndDate));
        Row("Opening Time", report.OpeningTime.ToString(@"hh\:mm"));
        Row("Closing Time", report.ClosingTime.ToString(@"hh\:mm"));
        Row();

        // Overall statistics
        Row("Summary", "Metric", "Value");

        Row("Summary", "Scheduled Reservations",
            Number(report.ReservationCount));

        Row("Summary", "Reserved Guests",
            Number(report.GuestCount));

        Row("Summary", "Booked Table Hours",
            Amount(report.BookedHours));

        Row("Summary", "Available Table Hours",
            Amount(report.AvailableHours));

        Row("Summary", "Scheduled Utilisation (%)",
            Amount(report.UtilisationPercent));

        Row();

        // Performance by dining area
        Row("Dining Area Performance");

        Row(
            "Dining Area",
            "Tables / Rooms",
            "Reservations",
            "Guests",
            "Booked Hours",
            "Available Hours",
            "Utilisation (%)");

        foreach (var area in report.Areas)
        {
            Row(
                area.Area,
                Number(area.TableCount),
                Number(area.ReservationCount),
                Number(area.GuestCount),
                Amount(area.BookedHours),
                Amount(area.AvailableHours),
                Amount(area.UtilisationPercent));
        }

        Row();

        // Reservation arrival distribution
        Row("Peak Reservation Arrival Hours");
        Row("Hour", "Reservations");

        foreach (var hour in report.ArrivalHours)
        {
            Row(
                hour.Hour.ToString("00") + ":00",
                Number(hour.ReservationCount));
        }

        Row();

        Row("Notes",
            "Figures represent scheduled table use, not verified physical occupancy.");

        Row("Notes",
            "Confirmed, Seated and Completed reservations are included.");

        Row("Notes",
            "Utilisation uses the currently configured table inventory.");

        // Add UTF-8 BOM for compatibility with Excel.
        byte[] content = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        string fileName =
            $"Celeste_Occupancy_{Date(report.StartDate)}" +
            $"_to_{Date(report.EndDate)}.csv";

        return File(
            content,
            "text/csv; charset=utf-8",
            fileName);
    }
}
