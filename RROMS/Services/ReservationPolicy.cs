// Shared coursework rules: allowed areas by party size, minimum spend, dining duration, modification deadline and arrival grace periods. Times use Malaysia UTC+8.
namespace RROMS.Services;

public static class ReservationPolicy
{
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));
    // Guests choose an area; they never choose a specific table. Invalid party sizes have no eligible areas.
    public static string[] Areas(int size) => size switch
    {
        >= 1 and <= 5 => ["Indoor", "Outdoor"],
        >= 6 and <= 8 => ["Indoor", "Outdoor", "VIP Room"],
        >= 9 and <= 12 => ["VIP Room"],
        _ => []
    };
    // Minimum dining spend is separate from the RM50-per-person reservation deposit.
    public static decimal MinimumSpend(int size, string area) =>
        area == "VIP Room" ? (size <= 8 ? 2400m : 3600m) : size * 50m;
    // Ordinary tables have two hours; VIP rooms remain reserved until that day's closing time.
    public static DateTime End(DateTime start, string area, TimeSpan closing) =>
        area == "VIP Room" ? start.Date.Add(closing) : start.AddHours(2);
    // A booking ending exactly when another begins is allowed, so the boundaries use strict comparisons.
    public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) =>
        start < otherEnd && end > otherStart;
    // Always compare against the ORIGINAL booking time, even after a reschedule. Exactly 168 hours is allowed.
    public static bool CanModify(DateTime originalStart, int count, DateTime now) =>
        count == 0 && originalStart - now >= TimeSpan.FromHours(168);
    // Both grace periods start at the booked arrival time, not at the late-notification time.
    public static bool IsExpired(DateTime start, bool late, DateTime now) =>
        now >= start.AddMinutes(late ? 30 : 10);
}
