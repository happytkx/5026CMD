
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
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

    // Validate the selected reporting period.
    private bool IsValidPeriod()
    {
        return StartDate.Date <= EndDate.Date &&
               EndDate.Date != DateTime.MaxValue.Date;
    }

    // Display the report on the website.
    public async Task OnGetAsync()
    {
        if (!IsValidPeriod())
        {
            ModelState.AddModelError(
                "",
                "Please select a valid reporting date range.");

            return;
        }

        Report = await _reportingService
            .GetPaymentCollectionsAsync(StartDate, EndDate);
    }

    // Export successful payment collections to CSV.
    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!IsValidPeriod())
        {
            return BadRequest(
                "Please select a valid reporting date range.");
        }

        // Retrieve the same data used by the website.
        PaymentCollectionsResult report =
            await _reportingService
                .GetPaymentCollectionsAsync(
                    StartDate,
                    EndDate);

        var csv = new StringBuilder();

        // Escape CSV values and protect spreadsheet
        // applications from interpreting text as formulas.
        static string EscapeCsv(string value)
        {
            string trimmed = value.TrimStart();

            if (trimmed.Length > 0 &&
                "=+-@".Contains(trimmed[0]))
            {
                value = "'" + value;
            }

            return "\"" +
                value.Replace("\"", "\"\"") +
                "\"";
        }

        void Row(params string[] values)
        {
            csv.AppendLine(
                string.Join(",",
                    values.Select(EscapeCsv)));
        }

        string Date(DateTime value) =>
            value.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

        string DateTimeText(DateTime value) =>
            value.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture);

        string Money(decimal value) =>
            value.ToString(
                "0.00",
                CultureInfo.InvariantCulture);

        string Number(int value) =>
            value.ToString(
                CultureInfo.InvariantCulture);

        // Report information.
        Row("Celeste Payment Collections Report");
        Row("Start Date", Date(StartDate));
        Row("End Date", Date(EndDate));
        Row();

        // Payment summary.
        Row("Payment Summary");
        Row("Metric", "Amount (RM)", "Count");

        Row("Paid Deposits",
            Money(report.Deposits), "");

        Row("Final Dining Payments",
            Money(report.DiningPayments), "");

        Row("Total Recorded Collections",
            Money(report.TotalCollected), "");

        Row("Successful Transactions",
            "", Number(report.TransactionCount));

        Row();

        // Daily payment collections.
        Row("Daily Payment Collections");

        Row(
            "Payment Date",
            "Deposits (RM)",
            "Final Payments (RM)",
            "Total (RM)",
            "Transactions");

        foreach (var day in report.Daily)
        {
            Row(
                Date(day.Date),
                Money(day.Deposits),
                Money(day.DiningPayments),
                Money(day.Total),
                Number(day.TransactionCount));
        }

        Row();

        // Individual successful payments.
        Row("Successful Payment Transactions");

        Row(
            "Payment ID",
            "Reservation ID",
            "Payment Date",
            "Booking Reference",
            "Customer Name",
            "Payment Type",
            "Payment Method",
            "Amount (RM)");

        foreach (var payment in report.Transactions)
        {
            Row(
                Number(payment.PaymentID),
                Number(payment.ReservationID),
                DateTimeText(payment.PaymentDate),
                payment.BookingReference,
                payment.CustomerName,
                payment.PaymentType,
                payment.PaymentMethod,
                Money(payment.Amount));
        }

        Row();

        // Report explanations.
        Row("Notes",
            "Only successfully paid transactions are included.");

        Row("Notes",
            "Collections are grouped by payment date.");

        Row("Notes",
            "Deposit payments and final dining payments are reported separately.");

        Row("Notes",
            "Recorded collections do not represent independently verified bank settlements.");

        // UTF-8 BOM improves Excel compatibility.
        byte[] content = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        string fileName =
            $"Celeste_PaymentCollections_" +
            $"{Date(StartDate)}_to_{Date(EndDate)}.csv";

        return File(
            content,
            "text/csv; charset=utf-8",
            fileName);
    }
}
