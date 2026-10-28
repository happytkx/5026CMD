// Digital menu: filters available dishes by name, category, dietary tag, protein and spice level, and adds dishes to the signed-in customer's basket.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages;

public class MenuModel(ApplicationDbContext db) : PageModel
{
    public List<MenuItem> Items { get; set; } = [];
    public List<string> Categories { get; set; } = [];
    [BindProperty(SupportsGet = true)]
    public string? Q
    {
        get; set;
    }
    [BindProperty(SupportsGet = true)]
    public string? Category
    {
        get; set;
    }
    [BindProperty(SupportsGet = true)]
    public string? Dietary
    {
        get; set;
    }
    [BindProperty(SupportsGet = true)]
    public string? Protein
    {
        get; set;
    }
    [BindProperty(SupportsGet = true)]
    public int? Spice
    {
        get; set;
    }

    public async Task OnGetAsync()
    {
        var query = db.MenuItems.AsNoTracking().Where(i => i.IsAvailable);
        if (!string.IsNullOrWhiteSpace(Q))
            query = query.Where(i => i.Name.Contains(Q) || i.Description.Contains(Q));
        if (!string.IsNullOrWhiteSpace(Category))
            query = query.Where(i => i.Category == Category);
        if (!string.IsNullOrWhiteSpace(Dietary))
            query = query.Where(i => i.DietaryTags.Contains(Dietary));
        if (!string.IsNullOrWhiteSpace(Protein))
            query = query.Where(i => i.ProteinType == Protein);
        if (Spice.HasValue)
            query = query.Where(i => i.SpiceLevel <= Spice.Value);
        Items = await query.OrderByDescending(i => i.IsPopular).ThenBy(i => i.Category).ThenBy(i => i.Name).ToListAsync();
        Categories = await db.MenuItems.Where(i => i.IsAvailable).Select(i => i.Category).Distinct().OrderBy(c => c).ToListAsync();
    }

    public async Task<IActionResult> OnPostAddAsync(int itemId)
    {
        // Adding to the basket requires a signed-in account.
        if (User.Identity?.IsAuthenticated != true)
            return Challenge();
        var item = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(i => i.MenuItemID == itemId && i.IsAvailable);
        if (item is null)
        {
            TempData["Error"] = "This dish is currently unavailable.";
            return RedirectToPage();
        }
        var basket = Basket.Read(HttpContext.Session);
        basket[itemId] = Math.Min(Basket.MaxQuantity, basket.GetValueOrDefault(itemId) + 1);
        Basket.Write(HttpContext.Session, basket);
        TempData["Success"] = $"{item.Name} added to your order.";
        return RedirectToPage(new { q = Q, category = Category, dietary = Dietary, protein = Protein, spice = Spice });
    }
}
