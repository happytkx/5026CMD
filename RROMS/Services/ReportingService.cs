using Microsoft.EntityFrameworkCore;
using RROMS.Data;

namespace RROMS.Services;

// Stores the results of a reservation report
public record ReservationReportSummary(
    DateTime StartDate,
    DateTime EndDate,
    int TotalReservations,
    int PendingReservations,
    int ConfirmedReservations,
    int SeatedReservations,
    int CompletedReservations,
    int CancelledReservations,
    int NoShowReservations,
    int ExpiredReservations,
    decimal PaidDeposits,
    decimal ForfeitedDeposits);

// Stores the reservation count for one date
public record DailyReservationCount(
    DateTime Date,
    int Count);

// Stores successful deposit collections for one date
public record DailyDepositCollection(
    DateTime Date,
    decimal TotalAmount,
    int PaymentCount);


public class ReportingService
{
    private readonly ApplicationDbContext _db;

    public ReportingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ReservationReportSummary> GetReservationSummaryAsync(
        DateTime startDate,
        DateTime endDate)

    {
        // Use dates without time components
        DateTime from = startDate.Date;
        DateTime to = endDate.Date;

        // Reject an invalid date range
        if (from > to)
        {
            throw new ArgumentException(
                "End date cannot be earlier than start date.",
                nameof(endDate));
        }

        DateTime endExclusive = to.AddDays(1);

        // Retrieve reservations within the selected date range
        var reservations = await _db.Reservations
            .AsNoTracking()
            .Where(r => r.ReservationDate >= from
                    && r.ReservationDate < endExclusive)
            .Select(r => new
            {
                r.ReservationStatus,
                r.DepositStatus,
                r.DepositAmount
            })
            .ToListAsync();

        // Sum successful reservation deposit payments
        decimal paidDeposits = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaymentType == "Deposit"
                    && p.PaymentStatus == "Paid"
                    && p.Reservation.ReservationDate >= from
                    && p.Reservation.ReservationDate < endExclusive)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        // Sum deposits forfeited due to cancellations or no-shows
        decimal forfeitedDeposits = reservations
            .Where(r => r.DepositStatus == "Forfeited")
            .Sum(r => r.DepositAmount);

        // Return the report summary
        return new ReservationReportSummary(
            from,
            to,
            reservations.Count,
            reservations.Count(r => r.ReservationStatus == "Pending"),
            reservations.Count(r => r.ReservationStatus == "Confirmed"),
            reservations.Count(r => r.ReservationStatus == "Seated"),
            reservations.Count(r => r.ReservationStatus == "Completed"),
            reservations.Count(r => r.ReservationStatus == "Cancelled"),
            reservations.Count(r => r.ReservationStatus == "No-show"),
            reservations.Count(r => r.ReservationStatus == "Expired"),
            paidDeposits,
            forfeitedDeposits);
    }

    // Get the number of reservations schedule for each date
    public async Task<List<DailyReservationCount>> GetDailyReservationTrendAsync(
        DateTime startDate,
        DateTime endDate)
    {
        DateTime from = startDate.Date;
        DateTime to = endDate.Date;

        if (from > to)
        {
            throw new ArgumentException(
                "End date cannot be earlier then start date.",
                nameof(endDate));
        }

        DateTime endExclusive = to.AddDays(1);

        // Group reservations by their scheduled dining date
        var dailyCounts = await _db.Reservations
            .AsNoTracking()
            .Where(r => r.ReservationDate >= from
                    && r.ReservationDate < endExclusive)
            .GroupBy(r => r.ReservationDate.Date)
            .Select(Group => new
            {
                Date = Group.Key,
                Count = Group.Count()
            })
            .ToListAsync();

        // Look up reservation counts by date
        var countsByDate = dailyCounts.ToDictionary(
            d => d.Date,
            d => d.Count);

        var dailyTrend = new List<DailyReservationCount>();

        // Include dates with zero reservations
        for (DateTime date = from; date <= to; date = date.AddDays(1))
        {
            int count = countsByDate.GetValueOrDefault(date);
        
            dailyTrend.Add(new DailyReservationCount(date, count));
        }

        return dailyTrend;

    }

    // Get successful deposit collections by actual payment date
    public async Task<List<DailyDepositCollection>> GetDailyDepositCollectionAsync(
        DateTime startDate,
        DateTime endDate)

    {
        DateTime from = startDate.Date;
        DateTime to = endDate.Date;

        if (from > to)
        {
            throw new ArgumentException(
                "End date cannot be earlier than start date.",
                nameof(endDate));
        }

        DateTime endExclusive = to.AddDays(1);

        // Group successful deposit payments by payment date
        var dailyPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaymentType == "Deposit"
                    && p.PaymentStatus == "Paid"
                    && p.PaymentDate >= from
                    && p.PaymentDate < endExclusive)
            .GroupBy(p => p.PaymentDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                TotalAmount = group.Sum(p => p.Amount),
                PaymentCount = group.Count()
            })
            .ToListAsync();

        // Create a lookup using payment date as the key
        var paymentsByDate = dailyPayments.ToDictionary(
            p => p.Date);

        var dailyCollections = new List<DailyDepositCollection>();

        // Include dates without successful deposit payments
        for (DateTime date = from; date <= to; date = date.AddDays(1))
        {
            if (paymentsByDate.TryGetValue(date, out var payment))
            {
                dailyCollections.Add(new DailyDepositCollection(
                    date,
                    payment.TotalAmount,
                    payment.PaymentCount));
            }
            else
            {
                dailyCollections.Add(new DailyDepositCollection(
                    date,
                    0m,
                    0));
            }
        }

        return dailyCollections;
    }

    // Retrieve successful payment collections by payment date.
    public async Task<PaymentCollectionsResult>
        GetPaymentCollectionsAsync(
            DateTime startDate,
            DateTime endDate)
    {
        DateTime from = startDate.Date;
        DateTime to = endDate.Date;

        if (from > to || to == DateTime.MaxValue.Date)
        {
            throw new ArgumentException(
                "Invalid reporting date range.");
        }

        DateTime endExclusive = to.AddDays(1);

        // Only count successful deposit and final dining payments.
        var paymentRecords = await _db.Payments
            .AsNoTracking()
            .Where(p =>
                p.PaymentStatus == "Paid" &&
                (p.PaymentType == "Deposit" ||
                 p.PaymentType == "DiningBill") &&
                p.PaymentDate >= from &&
                p.PaymentDate < endExclusive)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .Select(p => new
            {
                p.PaymentID,
                p.ReservationID,
                BookingReference = p.Reservation.BookingReference,
                CustomerName = p.Reservation.Customer.CustomerName,
                p.PaymentDate,
                p.PaymentType,
                p.PaymentMethod,
                p.Amount
            })
            .ToListAsync();

        // Convert the retrieved database results into
        // payment collection records.
        var payments = paymentRecords
            .Select(p => new PaymentCollectionRow(
                p.PaymentID,
                p.ReservationID,
                p.BookingReference,
                p.CustomerName,
                p.PaymentDate,
                p.PaymentType,
                p.PaymentMethod,
                p.Amount))
            .ToList();


        decimal deposits = payments
            .Where(p => p.PaymentType == "Deposit")
            .Sum(p => p.Amount);

        decimal finalPayments = payments
            .Where(p => p.PaymentType == "DiningBill")
            .Sum(p => p.Amount);

        // Group collections by the actual payment date.
        var daily = payments
            .GroupBy(p => p.PaymentDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyPaymentSummary(
                g.Key,
                g.Where(p => p.PaymentType == "Deposit")
                    .Sum(p => p.Amount),
                g.Where(p => p.PaymentType == "DiningBill")
                    .Sum(p => p.Amount),
                g.Count()))
            .ToList();

        return new PaymentCollectionsResult(
            payments,
            daily,
            deposits,
            finalPayments);
    }

}

public record PaymentCollectionRow(
    int PaymentID,
    int ReservationID,
    string BookingReference,
    string CustomerName,
    DateTime PaymentDate,
    string PaymentType,
    string PaymentMethod,
    decimal Amount);

public record DailyPaymentSummary(
    DateTime Date,
    decimal Deposits,
    decimal DiningPayments,
    int TransactionCount)
{
    public decimal Total => Deposits + DiningPayments;
}

public record PaymentCollectionsResult(
    List<PaymentCollectionRow> Transactions,
    List<DailyPaymentSummary> Daily,
    decimal Deposits,
    decimal DiningPayments)
{
    public decimal TotalCollected =>
        Deposits + DiningPayments;

    public int TransactionCount =>
        Transactions.Count;
}
