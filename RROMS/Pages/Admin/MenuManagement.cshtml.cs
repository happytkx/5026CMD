using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class MenuManagementModel(ApplicationDbContext db) : PageModel
{
    public List<MenuCategory> Categories { get; private set; } = [];
    public List<MenuItem> Items { get; private set; } = [];

    [BindProperty]
    public MenuItemInput Dish { get; set; } = new();

    public class MenuItemInput
    {
        public int MenuItemID { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string? ProteinType { get; set; }
        public string? DietaryTags { get; set; }
        public string? Allergens { get; set; }
        public int SpiceLevel { get; set; }
        public bool IsPopular { get; set; }
        public bool IsAvailable { get; set; }
    }

    public async Task OnGetAsync()
    {
        Categories = await db.MenuCategories.AsNoTracking()
            .OrderBy(c => c.Name).ToListAsync();
        Items = await db.MenuItems.AsNoTracking()
            .OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateCategoryAsync(string? name)
    {
        name = name?.Trim();
        if (!ValidCategoryName(name))
            return Error("Category name must be between 1 and 120 characters.");

        if (await db.MenuCategories.AnyAsync(c => c.Name == name))
            return Error("That category already exists.");

        db.MenuCategories.Add(new MenuCategory { Name = name! });
        await db.SaveChangesAsync();
        return Success($"Category {name} created.");
    }

    public async Task<IActionResult> OnPostRenameCategoryAsync(int categoryId, string? name)
    {
        name = name?.Trim();
        if (!ValidCategoryName(name))
            return Error("Category name must be between 1 and 120 characters.");

        var category = await db.MenuCategories.FindAsync(categoryId);
        if (category is null) return NotFound();
        if (await db.MenuCategories.AnyAsync(c => c.MenuCategoryID != categoryId && c.Name == name))
            return Error("That category already exists.");

        var previousName = category.Name;
        category.Name = name!;
        foreach (var item in await db.MenuItems.Where(i => i.Category == previousName).ToListAsync())
            item.Category = name!;
        await db.SaveChangesAsync();
        return Success($"Category renamed to {name}. Its dishes were moved automatically.");
    }

    public async Task<IActionResult> OnPostDeleteCategoryAsync(int categoryId)
    {
        var category = await db.MenuCategories.FindAsync(categoryId);
        if (category is null) return NotFound();
        if (await db.MenuItems.AnyAsync(i => i.Category == category.Name))
            return Error("Move or remove the dishes in this category before deleting it.");

        db.MenuCategories.Remove(category);
        await db.SaveChangesAsync();
        return Success("Empty category deleted.");
    }

    public async Task<IActionResult> OnPostSaveItemAsync()
    {
        var name = Dish.Name?.Trim();
        var description = Dish.Description?.Trim() ?? "";
        var category = Dish.Category?.Trim();
        var image = Dish.ImageUrl?.Trim();
        var protein = Dish.ProteinType?.Trim() ?? "Vegetarian";
        var dietary = Dish.DietaryTags?.Trim() ?? "";
        var allergens = Dish.Allergens?.Trim() ?? "";

        if (!ModelState.IsValid || Dish.MenuItemID < 0 ||
            string.IsNullOrWhiteSpace(name) || name.Length > 100 ||
            string.IsNullOrWhiteSpace(category) || category.Length > 120 ||
            description.Length > 600 || Dish.Price <= 0 || Dish.Price > 100000m ||
            decimal.Round(Dish.Price, 2) != Dish.Price ||
            protein.Length is < 1 or > 40 || dietary.Length > 250 ||
            allergens.Length > 300 || Dish.SpiceLevel is < 0 or > 5 ||
            (image?.Length ?? 0) > 400)
            return Error("Check the dish name, category, price (maximum two decimals), and field lengths.");

        if (!string.IsNullOrEmpty(image) &&
            !(image.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
              (Uri.TryCreate(image, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)))
            return Error("Use an HTTPS image URL or a local path starting with /images/.");

        if (!await db.MenuCategories.AnyAsync(c => c.Name == category))
            return Error("Choose an existing category. Create the category first if needed.");

        if (await db.MenuItems.AnyAsync(i => i.MenuItemID != Dish.MenuItemID && i.Name == name))
            return Error("A dish with that name already exists.");

        MenuItem item;
        if (Dish.MenuItemID == 0)
        {
            item = new MenuItem();
            db.MenuItems.Add(item);
        }
        else
        {
            var existing = await db.MenuItems.FindAsync(Dish.MenuItemID);
            if (existing is null) return NotFound();
            item = existing;
        }

        item.Name = name!;
        item.Description = description;
        item.Category = category!;
        item.Price = Dish.Price;
        item.ImageUrl = string.IsNullOrEmpty(image) ? null : image;
        item.ProteinType = protein;
        item.DietaryTags = dietary;
        item.Allergens = allergens;
        item.SpiceLevel = Dish.SpiceLevel;
        item.IsPopular = Dish.IsPopular;
        item.IsAvailable = Dish.IsAvailable;
        await db.SaveChangesAsync();
        return Success($"Dish {item.Name} {(Dish.MenuItemID == 0 ? "created" : "updated")}. Changes appear on the customer menu.");
    }

    public async Task<IActionResult> OnPostToggleAvailabilityAsync(int itemId)
    {
        var item = await db.MenuItems.FindAsync(itemId);
        if (item is null) return NotFound();
        item.IsAvailable = !item.IsAvailable;
        await db.SaveChangesAsync();
        return Success($"{item.Name} is now {(item.IsAvailable ? "available" : "unavailable")}.");
    }

    private static bool ValidCategoryName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 120;

    private IActionResult Error(string message)
    {
        TempData["Error"] = message;
        return RedirectToPage();
    }

    private IActionResult Success(string message)
    {
        TempData["Success"] = message;
        return RedirectToPage();
    }
}
