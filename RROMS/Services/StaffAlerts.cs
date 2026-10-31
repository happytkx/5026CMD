using System.Globalization;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Services;

// Queue alerts in the same DbContext transaction as the operation they describe.
public static class StaffAlerts
{
    public static void Add(ApplicationDbContext db, string title, string message, string targetUrl,
        string type = "Operations", string audience = StaffNotification.StaffAudience)
    {
        if (!targetUrl.StartsWith("/", StringComparison.Ordinal) || targetUrl.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Notification links must be local paths.", nameof(targetUrl));

        db.StaffNotifications.Add(new StaffNotification
        {
            Title = title,
            Message = message,
            Type = type,
            Audience = audience,
            TargetUrl = targetUrl,
            CreatedAt = ReservationPolicy.Now
        });
    }

    public static string ReservationUrl(DateTime date) =>
        "/Staff/Index?Date=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string OrderUrl(int reservationId) =>
        "/Staff/Orders?expanded=" + reservationId.ToString(CultureInfo.InvariantCulture);
}
