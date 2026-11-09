using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class RestaurantSettingsModel(ApplicationDbContext db) : PageModel
{
    [BindProperty] public string Opening { get; set; } = "12:00";
    [BindProperty] public string Closing { get; set; } = "22:00";
    [BindProperty] public int IntervalMinutes { get; set; } = 30;

    private int? AdministratorId =>
        int.TryParse(User.FindFirst("AdministratorID")?.Value, out var id) ? id : null;

    public async Task OnGetAsync()
    {
        var setting = await db.RestaurantSettings.AsNoTracking()
            .OrderBy(s => s.SettingID).FirstOrDefaultAsync();
        if (setting is null) return;
        Opening = setting.OpeningTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        Closing = setting.ClosingTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        IntervalMinutes = setting.SlotIntervalMinutes;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!TimeSpan.TryParseExact(Opening, @"hh\:mm", CultureInfo.InvariantCulture, out var opening) ||
            !TimeSpan.TryParseExact(Closing, @"hh\:mm", CultureInfo.InvariantCulture, out var closing) ||
            opening < TimeSpan.Zero || closing >= TimeSpan.FromDays(1) ||
            closing - opening < TimeSpan.FromHours(2))
        {
            TempData["RestaurantSettingsError"] = "Set valid same-day opening and closing hours, with at least two hours between them.";
            return RedirectToPage();
        }
        if (IntervalMinutes is not (15 or 30 or 60))
        {
            TempData["RestaurantSettingsError"] = "Choose a reservation interval of 15, 30 or 60 minutes.";
            return RedirectToPage();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var setting = await db.RestaurantSettings.OrderBy(s => s.SettingID).FirstOrDefaultAsync();
        if (setting is null)
        {
            setting = new RestaurantSetting { OpeningTime = opening, ClosingTime = closing,
                SlotIntervalMinutes = IntervalMinutes, CreatedByAdministratorID = AdministratorId };
            db.RestaurantSettings.Add(setting);
        }
        else
        {
            // Do not invalidate customer commitments. Existing bookings retain their time
            // and VIP guests retain their promised access until the original closing time.
            var now = ReservationPolicy.Now;
            var futureBookings = await db.Reservations.AsNoTracking()
                .Where(r => r.ReservationDate >= now.Date &&
                    (r.ReservationStatus == "Pending" ||
                     r.ReservationStatus == "Confirmed" ||
                     r.ReservationStatus == "Seated"))
                .Select(r => new
                {
                    r.ReservationDate,
                    r.StartTime,
                    r.EndTime,
                    r.PreferredDiningOption,
                    r.ReservationStatus,
                    r.HoldExpiresAt
                }).ToListAsync();

            var conflicts = futureBookings.Count(r =>
                r.ReservationDate.Add(r.EndTime) > now &&
                (r.ReservationStatus != "Pending" || r.HoldExpiresAt > now) &&
                (r.StartTime < opening || r.EndTime > closing ||
                 (r.PreferredDiningOption == "VIP Room" && r.EndTime != closing)));

            if (conflicts > 0)
            {
                TempData["RestaurantSettingsError"] =
                    $"Cannot change the hours: {conflicts} active or upcoming reservation(s) would be affected. Existing reservations must be honoured.";
                return RedirectToPage();
            }

            setting.OpeningTime = opening;
            setting.ClosingTime = closing;
            setting.SlotIntervalMinutes = IntervalMinutes;
            setting.UpdatedByAdministratorID = AdministratorId;
        }

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            TempData["RestaurantSettingsError"] = "Restaurant settings could not be saved. Refresh and try again.";
            return RedirectToPage();
        }

        TempData["RestaurantSettingsSuccess"] =
            $"Opening hours updated to {opening.ToString(@"hh\:mm", CultureInfo.InvariantCulture)}–{closing.ToString(@"hh\:mm", CultureInfo.InvariantCulture)}; reservation arrival slots are now every {IntervalMinutes} minutes.";
        return RedirectToPage();
    }
}
