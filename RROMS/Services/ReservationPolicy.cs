// Shared coursework rules: allowed areas by party size, minimum spend, dining duration, modification deadline and arrival grace periods. Times use Malaysia UTC+8.
namespace RROMS.Services;

public static class ReservationPolicy
{
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
    public static bool MeetsAdvance(string area, DateTime start, DateTime now) =>
        area is "Indoor" or "Outdoor" or "VIP Room" &&
        start - now >= TimeSpan.FromHours(area == "VIP Room" ? 24 : 2);
    public static DateTime PaymentDeadline(string area, DateTime start, DateTime created) =>
        new[] { created.AddMinutes(10), start.AddHours(area == "VIP Room" ? -24 : -2) }.Min();
    // Guests choose an area; they never choose a specific table. Invalid party sizes have no eligible areas.
    public static string[] Areas(int size) => size switch
    {
        >= 1 and <= 5 => ["Indoor", "Outdoor"],
        >= 6 and <= 8 => ["Indoor", "Outdoor", "VIP Room"],
        >= 9 and <= 12 => ["VIP Room"],
        _ => []
    };
    // Keep larger ordinary tables for larger groups. VIP rooms use their actual room capacity.
    public static bool SuitableCapacity(int size, string area, int capacity) =>
        Areas(size).Contains(area) && capacity >= size && (area == "VIP Room" ||
        capacity >= ((size + 1) / 2) * 2 && capacity <= (size switch
        {
            <= 2 => 4,
            <= 4 => 6,
            _ => 8
        }));
    // Minimum dining spend is separate from the RM50-per-person reservation deposit.
    public static decimal MinimumSpend(int size, string area) =>
        area == "VIP Room" ? (size <= 8 ? 2400m : 3600m) : size * 50m;
    // Ordinary tables have two hours; VIP rooms remain reserved until that day's closing time.
    public static DateTime End(DateTime start, string area, TimeSpan closing) =>
        area == "VIP Room" ? start.Date.Add(closing) : start.AddHours(2);
    // A booking ending exactly when another begins is allowed, so the boundaries use strict comparisons.
    public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) =>
        start < otherEnd && end > otherStart;
    // Always compare against the ORIGINAL booking time. The final 168 hours are closed to modifications.
    public static bool CanModify(DateTime originalStart, int count, DateTime now) =>
        count == 0 && originalStart - now > TimeSpan.FromHours(168);
    // Both grace periods start at the booked arrival time, not at the late-notification time.
    public static bool IsExpired(DateTime start, bool late, DateTime now) =>
        now >= start.AddMinutes(late ? 30 : 10);
}
