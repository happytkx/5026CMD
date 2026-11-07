// Shared reservation rules. Configured values are read from RestaurantSettings;
// optional parameters preserve compatibility with older callers and tests.
using RROMS.Models;

namespace RROMS.Services;

public static class ReservationPolicy
{
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));

    public static bool MeetsAdvance(string area, DateTime start, DateTime now, RestaurantSetting? settings = null) =>
        area is "Indoor" or "Outdoor" or "VIP Room" &&
        start - now >= TimeSpan.FromHours(area == "VIP Room" ? settings?.VipAdvanceHours ?? 24 : settings?.StandardAdvanceHours ?? 2);

    public static DateTime PaymentDeadline(string area, DateTime start, DateTime created, RestaurantSetting? settings = null) =>
        new[] { created.AddMinutes(settings?.PaymentHoldMinutes ?? 10),
            start.AddHours(-(area == "VIP Room" ? settings?.VipAdvanceHours ?? 24 : settings?.StandardAdvanceHours ?? 2)) }.Min();

    // Guest choice remains limited by the actual room sizes and existing tables.
    public static string[] Areas(int size) => size switch
    {
        >= 1 and <= 5 => ["Indoor", "Outdoor"],
        >= 6 and <= 8 => ["Indoor", "Outdoor", "VIP Room"],
        >= 9 and <= 12 => ["VIP Room"],
        _ => []
    };

    public static bool SuitableCapacity(int size, string area, int capacity) =>
        Areas(size).Contains(area) && capacity >= size && (area == "VIP Room" ||
        capacity >= ((size + 1) / 2) * 2 && capacity <= (size switch
        {
            <= 2 => 4,
            <= 4 => 6,
            _ => 8
        }));

    public static decimal MinimumSpend(int size, string area, RestaurantSetting? settings = null) =>
        area == "VIP Room" ? (size <= 8 ? settings?.VipEightSeatMinimumSpend ?? 2400m :
            settings?.VipTwelveSeatMinimumSpend ?? 3600m) : size * (settings?.StandardMinimumSpendPerGuest ?? 50m);

    public static DateTime End(DateTime start, string area, TimeSpan closing, RestaurantSetting? settings = null) =>
        area == "VIP Room" ? start.Date.Add(closing) : start.AddMinutes(settings?.StandardDiningMinutes ?? 120);

    // Every area shares the Indoor/Outdoor arrival cutoff, while VIP occupancy still ends at closing.
    // Existing reschedules use their originally agreed dining duration when supplied.
    public static bool FitsSharedArrivalWindow(DateTime start, RestaurantSetting hours, RestaurantSetting? bookingTerms = null) =>
        start.TimeOfDay >= hours.OpeningTime &&
        start.AddMinutes(bookingTerms?.StandardDiningMinutes ?? hours.StandardDiningMinutes) <= start.Date.Add(hours.ClosingTime);

    public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) =>
        start < otherEnd && end > otherStart;

    // The original start time is never reset after modification.
    public static bool CanModify(DateTime originalStart, int count, DateTime now,
        int cutoffHours = 168, int maximumReschedules = 1) =>
        count < maximumReschedules && originalStart - now > TimeSpan.FromHours(cutoffHours);

    // Arrival grace is measured from the reserved start, not the notification time.
    public static bool IsExpired(DateTime start, bool late, DateTime now,
        int graceMinutes = 10, int extendedGraceMinutes = 30) =>
        now >= start.AddMinutes(late ? extendedGraceMinutes : graceMinutes);
}
