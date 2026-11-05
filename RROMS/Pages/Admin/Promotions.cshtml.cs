using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class PromotionsModel(ApplicationDbContext db) : PageModel
{
    public List<MenuItem> Dishes { get; private set; } = [];
    public DateTime Today => ReservationPolicy.Now.Date;
    public int ActiveCount => Dishes.Count(i => MenuPricing.IsPromotionActive(i, Today));
    public int ScheduledCount => Dishes.Count(i => MenuPricing.CampaignStatus(i, Today) == "Scheduled");

    private int? AdministratorId =>
        int.TryParse(User.FindFirst("AdministratorID")?.Value, out var id) ? id : null;

    public async Task OnGetAsync()
    {
        Dishes = await db.MenuItems.AsNoTracking()
            .OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync();
    }

    private IActionResult Result(string? error, string? message = null)
    {
        if (error != null) TempData["PromotionError"] = error;
        if (message != null) TempData["PromotionSuccess"] = message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveAsync(
        int menuItemId, decimal discountPercent, DateTime promotionStartDate, DateTime promotionEndDate)
    {
        if (discountPercent <= 0m || discountPercent > 50m ||
            decimal.Round(discountPercent, 2) != discountPercent)
            return Result("Enter a discount greater than 0% and no more than 50%, with at most two decimal places.");

        var start = promotionStartDate.Date;
        var end = promotionEndDate.Date;
        if (start < new DateTime(2000, 1, 1) || end < start ||
            end < Today || (end - start).TotalDays > 365)
            return Result("Choose valid start/end dates. The end must not be in the past and the promotion can last at most one year.");

        var item = await db.MenuItems.FindAsync(menuItemId);
        if (item == null) return NotFound();

        item.DiscountPercent = discountPercent;
        item.PromotionStartDate = start;
        item.PromotionEndDate = end;
        item.UpdatedByAdministratorID = AdministratorId;
        await db.SaveChangesAsync();

        return Result(null, $"Promotion saved for {item.Name}: {discountPercent:0.##}% off, {start:dd MMM yyyy} to {end:dd MMM yyyy}.");
    }

    public async Task<IActionResult> OnPostStopAsync(int menuItemId)
    {
        var item = await db.MenuItems.FindAsync(menuItemId);
        if (item == null) return NotFound();

        item.DiscountPercent = null;
        item.PromotionStartDate = null;
        item.PromotionEndDate = null;
        item.UpdatedByAdministratorID = AdministratorId;
        await db.SaveChangesAsync();

        return Result(null, $"Promotion removed from {item.Name}. Existing orders keep their original recorded prices.");
    }
}
