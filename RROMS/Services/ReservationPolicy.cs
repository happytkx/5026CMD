
using RROMS.Models;

namespace RROMS.Services;

// Shared reservation rules for dining areas, capacity,
// minimum spending, booking deadlines and arrival times.
public static class ReservationPolicy
{
    // Restaurant operates in Malaysia (UTC+8).
    public static DateTime Now =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(
                "Asia/Kuala_Lumpur"));

    // Indoor/Outdoor: book at least 2 hours in advance.
    // VIP Room: book at least 24 hours in advance.
    public static bool MeetsAdvance(
        string area,
        DateTime start,
        DateTime now) =>
        area is "Indoor" or "Outdoor" or "VIP Room" &&
        start - now >= TimeSpan.FromHours(
            area == "VIP Room" ? 24 : 2);

    // Payment must be completed before the earlier deadline.
    public static DateTime PaymentDeadline(
        string area,
        DateTime start,
        DateTime created) =>
        new[]
        {
            created.AddMinutes(10),
            start.AddHours(
                area == "VIP Room" ? -24 : -2)
        }.Min();

    // Determine eligible dining areas based on party size.
    public static string[] Areas(int size) => size switch
    {
        >= 1 and <= 5 =>
            ["Indoor", "Outdoor"],

        >= 6 and <= 8 =>
            ["Indoor", "Outdoor", "VIP Room"],

        >= 9 and <= 12 =>
            ["VIP Room"],

        _ => []
    };

    // Check whether a table can accommodate the party.
    public static bool SuitableCapacity(
        int size,
        string area,
        int capacity) =>
        Areas(size).Contains(area) &&
        capacity >= size &&
        (
            area == "VIP Room" ||
            (
                capacity >= ((size + 1) / 2) * 2 &&
                capacity <= (size switch
                {
                    <= 2 => 4,
                    <= 4 => 6,
                    _ => 8
                })
            )
        );

    // Existing minimum-spending calculation.
    // Retained temporarily for compatibility with
    // existing services while US09 is being integrated.
    public static decimal MinimumSpend(
        int size,
        string area) =>
        area == "VIP Room"
            ? (size <= 8 ? 2400m : 3600m)
            : size * 50m;

    // US09: Administrator-configurable minimum spending.
    public static decimal MinimumSpend(
        int size,
        string area,
        RestaurantSetting settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (size < settings.MinimumPartySize ||
            size > settings.MaximumPartySize ||
            !Areas(size).Contains(area))
        {
            throw new ArgumentException(
                "Invalid party size or dining area.");
        }

        if (area == "VIP Room")
        {
            return size <= 8
                ? settings.VipMinimumSpendSmallGroup
                : settings.VipMinimumSpendLargeGroup;
        }

        return size * settings.StandardMinimumSpendPerGuest;
    }

    // Indoor/Outdoor: 2-hour dining duration.
    // VIP: until the restaurant closes.
    public static DateTime End(
        DateTime start,
        string area,
        TimeSpan closing) =>
        area == "VIP Room"
            ? start.Date.Add(closing)
            : start.AddHours(2);

    // Determine whether two reservations overlap.
    public static bool Overlaps(
        DateTime start,
        DateTime end,
        DateTime otherStart,
        DateTime otherEnd) =>
        start < otherEnd && end > otherStart;

    // A confirmed reservation may be modified once,
    // more than 168 hours before its original start.
    public static bool CanModify(
        DateTime originalStart,
        int count,
        DateTime now) =>
        count == 0 &&
        originalStart - now > TimeSpan.FromHours(168);

    // Standard grace period: 10 minutes.
    // Reported late arrival: 30 minutes.
    public static bool IsExpired(
        DateTime start,
        bool late,
        DateTime now) =>
        now >= start.AddMinutes(late ? 30 : 10);
}
