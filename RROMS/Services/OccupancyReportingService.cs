
using Microsoft.EntityFrameworkCore;
using RROMS.Data;

namespace RROMS.Services;

public record AreaUtilisation(
    string Area,
    int TableCount,
    int ReservationCount,
    int GuestCount,
    decimal BookedHours,
    decimal AvailableHours,
    decimal UtilisationPercent);

public record HourlyArrival(
    int Hour,
    int ReservationCount);

public record OccupancyReport(
    DateTime StartDate,
    DateTime EndDate,
    TimeSpan OpeningTime,
    TimeSpan ClosingTime,
    int ReservationCount,
    int GuestCount,
    decimal BookedHours,
    decimal AvailableHours,
    decimal UtilisationPercent,
    List<AreaUtilisation> Areas,
    List<HourlyArrival> ArrivalHours);

public class OccupancyReportingService
{
    private readonly ApplicationDbContext _db;

    public OccupancyReportingService(ApplicationDbContext db)
    {
        _db = db;
    }

    private record BookingSlot(
        string TableID,
        DateTime Date,
        TimeSpan Start,
        TimeSpan End,
        int Guests);

    public async Task<OccupancyReport> GetReportAsync(
        DateTime startDate,
        DateTime endDate)
    {
        DateTime from = startDate.Date;
        DateTime to = endDate.Date;

        if (from > to ||
            to == DateTime.MaxValue.Date ||
            (to - from).Days > 89)
        {
            throw new ArgumentException(
                "Choose a reporting period of up to 90 days.");
        }

        int days = (to - from).Days + 1;

        // Read the existing restaurant opening hours.
        var setting = await _db.RestaurantSettings
            .AsNoTracking()
            .OrderBy(s => s.SettingID)
            .FirstOrDefaultAsync();

        TimeSpan opening = setting?.OpeningTime
            ?? TimeSpan.FromHours(12);

        TimeSpan closing = setting?.ClosingTime
            ?? TimeSpan.FromHours(22);

        if (closing <= opening)
        {
            throw new InvalidOperationException(
                "Invalid restaurant opening hours.");
        }

        // Use the current configured table inventory.
        var tables = await _db.RestaurantTables
            .AsNoTracking()
            .Select(t => new
            {
                t.TableID,
                Area = t.DiningArea.AreaType
            })
            .ToListAsync();

        // Confirmed, seated and completed bookings represent
        // scheduled table use.
        // Exclude pending, cancelled, expired and no-show bookings.
        var reservationData = await _db.Reservations
            .AsNoTracking()
            .Where(r =>
                r.ReservationDate >= from &&
                r.ReservationDate < to.AddDays(1) &&
                (r.ReservationStatus == "Confirmed" ||
                 r.ReservationStatus == "Seated" ||
                 r.ReservationStatus == "Completed"))
            .Select(r => new
            {
                r.TableID,
                r.ReservationDate,
                r.StartTime,
                r.EndTime,
                r.PartySize
            })
            .ToListAsync();

        var tableIds = tables
            .Select(t => t.TableID)
            .ToHashSet();

        var bookings = reservationData
            .Where(r => tableIds.Contains(r.TableID))
            .Select(r => new BookingSlot(
                r.TableID,
                r.ReservationDate.Date,
                r.StartTime,
                r.EndTime,
                r.PartySize))
            .ToList();

        decimal hoursPerDay =
            (decimal)(closing - opening).TotalHours;

        var areaResults = new List<AreaUtilisation>();

        foreach (var area in tables
            .GroupBy(t => t.Area)
            .OrderBy(g => g.Key))
        {
            var areaTableIds = area
                .Select(t => t.TableID)
                .ToHashSet();

            var areaBookings = bookings
                .Where(b => areaTableIds.Contains(b.TableID))
                .ToList();

            decimal availableHours =
                area.Count() * days * hoursPerDay;

            decimal bookedHours = CalculateBookedHours(
                areaBookings, opening, closing);

            decimal percentage = availableHours == 0m
                ? 0m
                : Math.Round(
                    bookedHours / availableHours * 100m, 1);

            areaResults.Add(new AreaUtilisation(
                area.Key,
                area.Count(),
                areaBookings.Count,
                areaBookings.Sum(b => b.Guests),
                bookedHours,
                availableHours,
                percentage));
        }

        // Count reservation arrivals by hour.
        var arrivalHours = new List<HourlyArrival>();

        int firstHour = opening.Hours;
        int lastHourExclusive =
            (int)Math.Ceiling(closing.TotalHours);

        for (int hour = firstHour;
             hour < lastHourExclusive;
             hour++)
        {
            int count = bookings.Count(b =>
                b.Start.Hours == hour &&
                b.Start >= opening &&
                b.Start < closing);

            arrivalHours.Add(new HourlyArrival(hour, count));
        }

        decimal totalBooked = areaResults
            .Sum(a => a.BookedHours);

        decimal totalAvailable = areaResults
            .Sum(a => a.AvailableHours);

        decimal overallPercentage = totalAvailable == 0m
            ? 0m
            : Math.Round(
                totalBooked / totalAvailable * 100m, 1);

        return new OccupancyReport(
            from,
            to,
            opening,
            closing,
            bookings.Count,
            bookings.Sum(b => b.Guests),
            totalBooked,
            totalAvailable,
            overallPercentage,
            areaResults,
            arrivalHours);
    }

    // Count reserved hours without double-counting
    // overlapping intervals for the same table and date.
    private static decimal CalculateBookedHours(
        List<BookingSlot> bookings,
        TimeSpan opening,
        TimeSpan closing)
    {
        long totalTicks = 0;

        foreach (var group in bookings.GroupBy(
            b => new { b.Date, b.TableID }))
        {
            var intervals = group
                .Select(b => new
                {
                    Start = b.Start > opening
                        ? b.Start : opening,
                    End = b.End < closing
                        ? b.End : closing
                })
                .Where(x => x.End > x.Start)
                .OrderBy(x => x.Start)
                .ToList();

            if (intervals.Count == 0)
            {
                continue;
            }

            TimeSpan currentStart = intervals[0].Start;
            TimeSpan currentEnd = intervals[0].End;

            foreach (var interval in intervals.Skip(1))
            {
                if (interval.Start > currentEnd)
                {
                    totalTicks +=
                        (currentEnd - currentStart).Ticks;

                    currentStart = interval.Start;
                    currentEnd = interval.End;
                }
                else if (interval.End > currentEnd)
                {
                    currentEnd = interval.End;
                }
            }

            totalTicks +=
                (currentEnd - currentStart).Ticks;
        }

        return Math.Round(
            totalTicks / (decimal)TimeSpan.TicksPerHour,
            2);
    }
}
