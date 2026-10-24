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
}