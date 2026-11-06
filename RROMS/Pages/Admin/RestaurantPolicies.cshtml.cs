using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class RestaurantPoliciesModel(ApplicationDbContext db) : PageModel
{
    public RestaurantSetting Current { get; private set; } = new();
    public bool IsEditing { get; private set; }

    [BindProperty] public PolicyForm Form { get; set; } = new();

    private int? AdministratorId =>
        int.TryParse(User.FindFirst("AdministratorID")?.Value, out var id) ? id : null;

    public sealed class PolicyForm
    {
        [Range(typeof(decimal), "1", "10000")] public decimal DepositPerGuest { get; set; } = 50m;
        [Range(typeof(decimal), "0", "100000")] public decimal StandardMinimumSpendPerGuest { get; set; } = 50m;
        [Range(typeof(decimal), "0", "100000")] public decimal VipEightSeatMinimumSpend { get; set; } = 2400m;
        [Range(typeof(decimal), "0", "100000")] public decimal VipTwelveSeatMinimumSpend { get; set; } = 3600m;
        [Range(1, 720)] public int RescheduleCutoffHours { get; set; } = 168;
        [Range(1, 4)] public int MaximumReschedules { get; set; } = 1;
        [Range(1, 120)] public int ArrivalGraceMinutes { get; set; } = 10;
        [Range(2, 240)] public int ExtendedArrivalGraceMinutes { get; set; } = 30;
        [Range(30, 600)] public int StandardDiningMinutes { get; set; } = 120;
        [Range(1, 72)] public int StandardAdvanceHours { get; set; } = 2;
        [Range(1, 240)] public int VipAdvanceHours { get; set; } = 24;
        [Range(1, 60)] public int PaymentHoldMinutes { get; set; } = 10;
        [StringLength(1000)] public string? GeneralGuidelines { get; set; }

        public static PolicyForm From(RestaurantSetting s) => new()
        {
            DepositPerGuest = s.DepositPerGuest,
            StandardMinimumSpendPerGuest = s.StandardMinimumSpendPerGuest,
            VipEightSeatMinimumSpend = s.VipEightSeatMinimumSpend,
            VipTwelveSeatMinimumSpend = s.VipTwelveSeatMinimumSpend,
            RescheduleCutoffHours = s.RescheduleCutoffHours,
            MaximumReschedules = s.MaximumReschedules,
            ArrivalGraceMinutes = s.ArrivalGraceMinutes,
            ExtendedArrivalGraceMinutes = s.ExtendedArrivalGraceMinutes,
            StandardDiningMinutes = s.StandardDiningMinutes,
            StandardAdvanceHours = s.StandardAdvanceHours,
            VipAdvanceHours = s.VipAdvanceHours,
            PaymentHoldMinutes = s.PaymentHoldMinutes,
            GeneralGuidelines = s.GeneralGuidelines
        };
    }

    public async Task OnGetAsync(bool edit = false)
    {
        Current = await db.RestaurantSettings.AsNoTracking()
            .OrderBy(s => s.SettingID).FirstOrDefaultAsync() ?? new RestaurantSetting();
        Form = PolicyForm.From(Current);
        IsEditing = edit;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        IsEditing = true;
        Current = await db.RestaurantSettings.AsNoTracking()
            .OrderBy(s => s.SettingID).FirstOrDefaultAsync() ?? new RestaurantSetting();

        if (Form.ExtendedArrivalGraceMinutes <= Form.ArrivalGraceMinutes)
            ModelState.AddModelError(nameof(Form.ExtendedArrivalGraceMinutes),
                "The notified-arrival grace must be longer than the normal grace period.");
        if (Form.PaymentHoldMinutes > Form.StandardAdvanceHours * 60 ||
            Form.PaymentHoldMinutes > Form.VipAdvanceHours * 60)
            ModelState.AddModelError(nameof(Form.PaymentHoldMinutes),
                "The payment hold must fit within both advance-booking windows.");
        foreach (var value in new[] { Form.DepositPerGuest, Form.StandardMinimumSpendPerGuest,
            Form.VipEightSeatMinimumSpend, Form.VipTwelveSeatMinimumSpend })
        {
            if (value != decimal.Round(value, 2))
            {
                ModelState.AddModelError("", "Money amounts must have at most two decimal places.");
                break;
            }
        }
        if (Form.StandardDiningMinutes > (Current.ClosingTime - Current.OpeningTime).TotalMinutes)
            ModelState.AddModelError(nameof(Form.StandardDiningMinutes),
                "The standard dining duration must fit within the restaurant's opening hours.");

        if (!ModelState.IsValid) return Page();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var setting = await db.RestaurantSettings.OrderBy(s => s.SettingID).FirstOrDefaultAsync();
        if (setting is null)
        {
            setting = new RestaurantSetting { CreatedByAdministratorID = AdministratorId };
            db.RestaurantSettings.Add(setting);
        }
        // Re-check the duration against the latest opening hours inside the transaction.
        if (Form.StandardDiningMinutes > (setting.ClosingTime - setting.OpeningTime).TotalMinutes)
        {
            ModelState.AddModelError(nameof(Form.StandardDiningMinutes),
                "The opening hours changed. Reduce the dining duration or update Restaurant Settings.");
            Current = setting;
            return Page();
        }

        setting.DepositPerGuest = Form.DepositPerGuest;
        setting.StandardMinimumSpendPerGuest = Form.StandardMinimumSpendPerGuest;
        setting.VipEightSeatMinimumSpend = Form.VipEightSeatMinimumSpend;
        setting.VipTwelveSeatMinimumSpend = Form.VipTwelveSeatMinimumSpend;
        setting.RescheduleCutoffHours = Form.RescheduleCutoffHours;
        setting.MaximumReschedules = Form.MaximumReschedules;
        setting.ArrivalGraceMinutes = Form.ArrivalGraceMinutes;
        setting.ExtendedArrivalGraceMinutes = Form.ExtendedArrivalGraceMinutes;
        setting.StandardDiningMinutes = Form.StandardDiningMinutes;
        setting.StandardAdvanceHours = Form.StandardAdvanceHours;
        setting.VipAdvanceHours = Form.VipAdvanceHours;
        setting.PaymentHoldMinutes = Form.PaymentHoldMinutes;
        setting.GeneralGuidelines = string.IsNullOrWhiteSpace(Form.GeneralGuidelines)
            ? null : Form.GeneralGuidelines.Trim();
        setting.PoliciesUpdatedAt = ReservationPolicy.Now;
        setting.UpdatedByAdministratorID = AdministratorId;

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Policies could not be saved. Refresh the page and try again.");
            return Page();
        }

        TempData["PoliciesSuccess"] = "Restaurant policies saved. New reservations and published policy wording now use these values; existing bookings retain their agreed amounts and arrival terms.";
        return RedirectToPage();
    }
}
