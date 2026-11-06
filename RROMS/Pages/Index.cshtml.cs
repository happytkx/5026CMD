// Public landing page data: guest-favourite dishes.
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<MenuItem> Popular { get; set; } = [];
    public RestaurantSetting Policy { get; private set; } = new();
    public List<Announcement> Announcements { get; set; } = [];
    public List<Announcement> PopupAnnouncements { get; set; } = [];
    public List<Announcement> FeaturedPromotions { get; set; } = [];
    public DateTime Today => RROMS.Services.ReservationPolicy.Now.Date;

    public async Task OnGetAsync()
    {
        Policy = await db.RestaurantSettings.AsNoTracking().OrderBy(s => s.SettingID).FirstOrDefaultAsync() ?? new RestaurantSetting();
        Popular = await db.MenuItems.AsNoTracking().Where(i => i.IsAvailable && i.IsPopular).OrderBy(i => i.Name).Take(4).ToListAsync();

        var now = ReservationPolicy.Now;
        var visible = await db.Announcements.AsNoTracking()
            .Where(a => a.IsActive && (a.StartsAt == null || a.StartsAt <= now) && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.CreatedAt).ThenByDescending(a => a.AnnouncementID)
            .ToListAsync();

        // Menu discounts also need to reach the homepage. A promotion saved for
        // one dish, or a bulk promotion without a custom announcement, should
        // not require a second publishing step to appear in offers and popups.
        var discountedDishes = await db.MenuItems.AsNoTracking()
            .Where(i => i.IsAvailable && i.DiscountPercent > 0m && i.DiscountPercent <= 50m &&
                i.PromotionStartDate != null && i.PromotionStartDate <= Today &&
                i.PromotionEndDate != null && i.PromotionEndDate >= Today)
            .OrderBy(i => i.Name)
            .ToListAsync();

        var automaticOffers = new List<Announcement>();
        var campaigns = discountedDishes.GroupBy(i => new
        {
            Percent = i.DiscountPercent!.Value,
            Start = i.PromotionStartDate!.Value.Date,
            End = i.PromotionEndDate!.Value.Date
        });

        foreach (var campaign in campaigns)
        {
            var dishes = campaign.OrderBy(i => i.Name).ToList();
            var start = campaign.Key.Start;
            var end = campaign.Key.End;
            var percentage = campaign.Key.Percent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            // A custom announcement from "Also publish" already represents
            // this offer. Keep that copy instead of showing a duplicate.
            var hasCustomAnnouncement = visible.Any(a =>
                a.IsPromotional && a.StartsAt?.Date == start && a.ExpiresAt?.Date == end.AddDays(1) &&
                (a.Title.Contains(percentage + "%", StringComparison.OrdinalIgnoreCase) ||
                 a.Message.Contains(percentage + "%", StringComparison.OrdinalIgnoreCase) ||
                 dishes.Any(d => a.Message.Contains(d.Name, StringComparison.OrdinalIgnoreCase))));
            if (hasCustomAnnouncement) continue;

            var names = string.Join(", ", dishes.Take(4).Select(i => i.Name));
            if (dishes.Count > 4) names += $" and {dishes.Count - 4} more";

            // Stable, non-database ID: the browser shows each campaign popup
            // once, while a changed campaign has a fresh identity.
            var campaignKey = $"{percentage}:{start:yyyyMMdd}:{end:yyyyMMdd}:{string.Join(",", dishes.Select(i => i.MenuItemID).OrderBy(id => id))}";
            var digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(campaignKey));
            var campaignId = -1 - (BitConverter.ToInt32(digest, 0) & int.MaxValue);

            automaticOffers.Add(new Announcement
            {
                AnnouncementID = campaignId,
                Title = $"Enjoy {percentage}% off at Céleste",
                Message = $"Save {percentage}% on {names} from {start:dd MMM} to {end:dd MMM yyyy}. Explore our menu while the offer lasts.",
                IsActive = true,
                IsPromotional = true,
                IsFeatured = true,
                ShowInPopup = true,
                DurationMode = "custom",
                StartsAt = start,
                ExpiresAt = end.AddDays(1),
                CreatedAt = start
            });
        }

        // Promotional offers stay above the dining spaces; ordinary news
        // stays in the announcements section below.
        FeaturedPromotions = visible.Where(a => a.IsPromotional).Concat(automaticOffers)
            .OrderByDescending(a => a.StartsAt ?? a.CreatedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AnnouncementID)
            .ToList();
        PopupAnnouncements = visible.Where(a => a.ShowInPopup).Concat(automaticOffers)
            .OrderByDescending(a => a.StartsAt ?? a.CreatedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ToList();
        Announcements = visible.Where(a => !a.IsPromotional).ToList();
    }
}
