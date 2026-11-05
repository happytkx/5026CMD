
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class OperatingHoursModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public OperatingHoursModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public TimeSpan OpeningTime { get; set; }

    [BindProperty]
    public TimeSpan ClosingTime { get; set; }

    public TimeSpan CurrentOpeningTime { get; set; }
    public TimeSpan CurrentClosingTime { get; set; }

    public bool SettingExists { get; set; }

    public async Task OnGetAsync()
    {
        var setting = await _db.RestaurantSettings
            .OrderBy(s => s.SettingID)
            .FirstOrDefaultAsync();

        if (setting != null)
        {
            SettingExists = true;

            CurrentOpeningTime = setting.OpeningTime;
            CurrentClosingTime = setting.ClosingTime;

            OpeningTime = setting.OpeningTime;
            ClosingTime = setting.ClosingTime;
        }
        else
        {
            OpeningTime = TimeSpan.FromHours(12);
            ClosingTime = TimeSpan.FromHours(22);
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please enter valid operating hours.";
            return RedirectToPage();
        }

        if (OpeningTime < TimeSpan.Zero ||
            ClosingTime > TimeSpan.FromDays(1) ||
            OpeningTime >= ClosingTime)
        {
            TempData["Error"] =
                "Closing time must be later than opening time.";
            return RedirectToPage();
        }

        if (OpeningTime.Ticks % TimeSpan.FromMinutes(30).Ticks != 0 ||
            ClosingTime.Ticks % TimeSpan.FromMinutes(30).Ticks != 0)
        {
            TempData["Error"] =
                "Please choose times in 30-minute increments.";
            return RedirectToPage();
        }

        DateTime now = ReservationPolicy.Now;

        // Check existing reservations before changing operating hours.
        var reservations = await _db.Reservations
            .AsNoTracking()
            .Where(r => r.ReservationDate >= now.Date &&
                (r.ReservationStatus == "Confirmed" ||
                 r.ReservationStatus == "Seated" ||
                 (r.ReservationStatus == "Pending" &&
                  r.HoldExpiresAt > now)))
            .Select(r => new
            {
                r.ReservationDate,
                r.StartTime,
                r.EndTime
            })
            .ToListAsync();

        foreach (var reservation in reservations)
        {
            DateTime reservationStart =
                reservation.ReservationDate.Add(reservation.StartTime);

            DateTime reservationEnd =
                reservation.ReservationDate.Add(reservation.EndTime);

            if (reservationEnd <= now)
            {
                continue;
            }

            if (reservation.StartTime < OpeningTime ||
                reservation.EndTime > ClosingTime)
            {
                TempData["Error"] =
                    "Operating hours cannot be changed because " +
                    "an active or upcoming reservation falls " +
                    "outside the proposed hours.";

                return RedirectToPage();
            }
        }

        var setting = await _db.RestaurantSettings
            .OrderBy(s => s.SettingID)
            .FirstOrDefaultAsync();

        if (setting == null)
        {
            setting = new RestaurantSetting();

            _db.RestaurantSettings.Add(setting);
        }

        setting.OpeningTime = OpeningTime;
        setting.ClosingTime = ClosingTime;

        await _db.SaveChangesAsync();

        TempData["Success"] =
            "Restaurant operating hours updated successfully.";

        return RedirectToPage();
    }
}
