// Digital menu: filters available dishes by name, category, dietary tag and protein, and adds dishes to the signed-in customer's basket.
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
    public bool CanOrder { get; private set; }
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

    public async Task OnGetAsync()
    {
        CanOrder = User.IsInRole(Customer.CustomerRole) &&
            await DineInOrdering.EligibleReservations(db, ReservationIdentity.CustomerId(User)).AnyAsync();
        var query = db.MenuItems.AsNoTracking().Where(i => i.IsAvailable);
        if (!string.IsNullOrWhiteSpace(Q))
            query = query.Where(i => i.Name.Contains(Q) || i.Description.Contains(Q));
        if (!string.IsNullOrWhiteSpace(Category))
            query = query.Where(i => i.Category == Category);
        if (!string.IsNullOrWhiteSpace(Dietary))
            query = query.Where(i => i.DietaryTags.Contains(Dietary));
        if (!string.IsNullOrWhiteSpace(Protein))
            query = query.Where(i => i.ProteinType == Protein);
        Items = await query.OrderByDescending(i => i.IsPopular).ThenBy(i => i.Category).ThenBy(i => i.Name).ToListAsync();
        Categories = await db.MenuItems.Where(i => i.IsAvailable).Select(i => i.Category).Distinct().OrderBy(c => c).ToListAsync();
    }

    public async Task<IActionResult> OnPostAddAsync(int itemId)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Challenge();
        // Internal personnel can browse the menu but cannot order using their staff role.
        if (!User.IsInRole(Customer.CustomerRole))
            return RedirectToPage("/Account/Denied", new { ReturnUrl = Url.Page("/Menu") });
        // Re-check on POST so a crafted request cannot bypass the Seated requirement.
        if (!await DineInOrdering.EligibleReservations(db, ReservationIdentity.CustomerId(User)).AnyAsync())
        {
            TempData["Error"] = "Dishes can be added only after staff check you in for today's reservation and mark you Seated, while the bill is still open.";
            return RedirectToPage(new { q = Q, category = Category, dietary = Dietary, protein = Protein });
        }
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
        return RedirectToPage(new { q = Q, category = Category, dietary = Dietary, protein = Protein });
    }
}
