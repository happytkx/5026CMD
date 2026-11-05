using RROMS.Models;

namespace RROMS.Services;

/// <summary>
/// Single source of truth for time-limited menu discounts. Historic order lines
/// keep their unit-price snapshot, so changing a campaign never rewrites sales.
/// </summary>
public static class MenuPricing
{
    public static bool IsPromotionActive(MenuItem item, DateTime malaysiaNow)
    {
        var day = malaysiaNow.Date;
        return item.DiscountPercent is > 0m and <= 50m &&
               item.PromotionStartDate.HasValue &&
               item.PromotionEndDate.HasValue &&
               item.PromotionStartDate.Value.Date <= day &&
               item.PromotionEndDate.Value.Date >= day;
    }

    public static decimal CurrentPrice(MenuItem item, DateTime malaysiaNow)
    {
        return IsPromotionActive(item, malaysiaNow) ? DiscountedPrice(item) : item.Price;
    }

    public static decimal DiscountedPrice(MenuItem item) =>
        item.DiscountPercent is > 0m and <= 50m
            ? decimal.Round(item.Price * (100m - item.DiscountPercent.Value) / 100m,
                2, MidpointRounding.AwayFromZero)
            : item.Price;

    public static string CampaignStatus(MenuItem item, DateTime malaysiaNow)
    {
        if (!item.DiscountPercent.HasValue || !item.PromotionStartDate.HasValue ||
            !item.PromotionEndDate.HasValue)
            return "No promotion";
        if (IsPromotionActive(item, malaysiaNow))
            return "Active";
        if (malaysiaNow.Date < item.PromotionStartDate.Value.Date)
            return "Scheduled";
        return "Expired";
    }
}
