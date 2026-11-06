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

    // One campaign may cover several dishes, while each dish keeps its own price history.
    public async Task<IActionResult> OnPostApplyBulkAsync(
        List<int>? menuItemIds, decimal discountPercent, DateTime promotionStartDate,
        DateTime promotionEndDate, bool publishAnnouncement, string? announcementTitle,
        string? announcementMessage, bool featureAnnouncement)
    {
        var ids = (menuItemIds ?? []).Distinct().ToList();
        if (ids.Count == 0 || ids.Count > 100)
            return Result("Choose between 1 and 100 dishes for this promotion.");
        if (discountPercent <= 0m || discountPercent > 50m ||
            decimal.Round(discountPercent, 2) != discountPercent)
            return Result("Enter a discount greater than 0% and no more than 50%, with at most two decimal places.");

        var start = promotionStartDate.Date;
        var end = promotionEndDate.Date;
        if (start < new DateTime(2000, 1, 1) || end < start ||
            end < Today || (end - start).TotalDays > 365)
            return Result("Choose valid promotion dates; the end cannot be in the past and campaigns last at most one year.");
        if (publishAnnouncement && (announcementTitle?.Trim().Length > 120 ||
            announcementMessage?.Trim().Length > 1000))
            return Result("Announcement titles must be at most 120 characters and messages at most 1,000 characters.");

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var dishes = await db.MenuItems.Where(i => ids.Contains(i.MenuItemID)).ToListAsync();
        if (dishes.Count != ids.Count)
            return Result("One of the selected dishes no longer exists. Refresh and try again.");

        foreach (var dish in dishes)
        {
            dish.DiscountPercent = discountPercent;
            dish.PromotionStartDate = start;
            dish.PromotionEndDate = end;
            dish.UpdatedByAdministratorID = AdministratorId;
        }

        if (publishAnnouncement)
        {
            var names = string.Join(", ", dishes.OrderBy(i => i.Name).Take(4).Select(i => i.Name));
            if (dishes.Count > 4) names += $" and {dishes.Count - 4} more";
            var title = string.IsNullOrWhiteSpace(announcementTitle)
                ? "A limited-time offer from Céleste" : announcementTitle.Trim();
            var message = string.IsNullOrWhiteSpace(announcementMessage)
                ? $"Enjoy {discountPercent:0.##}% off {names} from {start:dd MMM} to {end:dd MMM yyyy}. Explore our menu while the offer lasts."
                : announcementMessage.Trim();

            if (featureAnnouncement)
            {
                var previouslyFeatured = await db.Announcements.Where(a => a.IsFeatured).ToListAsync();
                foreach (var announcement in previouslyFeatured) announcement.IsFeatured = false;
            }

            db.Announcements.Add(new Announcement
            {
                Title = title,
                Message = message,
                DurationMode = "custom",
                IsActive = true,
                IsPromotional = true,
                ShowInPopup = true,
                IsFeatured = featureAnnouncement,
                StartsAt = start,
                ExpiresAt = end.AddDays(1), // Inclusive final promotion day.
                CreatedAt = ReservationPolicy.Now,
                CreatedByAdministratorID = AdministratorId,
                UpdatedByAdministratorID = AdministratorId
            });
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result(null, $"Promotion applied to {dishes.Count} dishes from {start:dd MMM yyyy} through {end:dd MMM yyyy}." +
            (publishAnnouncement ? " A promotional announcement was published." : ""));
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
