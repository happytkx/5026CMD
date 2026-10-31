using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class MenuManagementModel(ApplicationDbContext db, IWebHostEnvironment host) : PageModel
{
    public List<MenuCategory> Categories { get; private set; } = [];
    public List<MenuItem> AllItems { get; private set; } = [];
    public List<MenuItem> Items { get; private set; } = [];
    public List<string> DietaryOptions { get; private set; } = [];
    public List<string> AllergenOptions { get; private set; } = [];
    public List<string> ProteinOptions { get; private set; } = [];
    public List<MenuOption> MenuOptions { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterCategory { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterAvailability { get; set; }

    [BindProperty] public IFormFile? DishImage { get; set; }
    [BindProperty] public string[] DietarySelections { get; set; } = [];
    [BindProperty] public string[] AllergenSelections { get; set; } = [];

    [BindProperty]
    public MenuItemInput Dish { get; set; } = new();

    public class MenuItemInput
    {
        public int MenuItemID { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ProteinType { get; set; }
        public string? DietaryTags { get; set; }
        public string? Allergens { get; set; }
        public bool IsPopular { get; set; }
        public bool IsAvailable { get; set; }
    }

    public async Task OnGetAsync()
    {
        Categories = await db.MenuCategories.AsNoTracking()
            .OrderBy(c => c.Name).ToListAsync();
        AllItems = await db.MenuItems.AsNoTracking()
            .OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync();

        MenuOptions = await db.MenuOptions.AsNoTracking()
            .OrderBy(o => o.Kind).ThenBy(o => o.Name).ToListAsync();
        DietaryOptions = Options(MenuOptions.Where(o => o.Kind == "Dietary").Select(o => o.Name),
            AllItems.Select(i => i.DietaryTags));
        AllergenOptions = Options(MenuOptions.Where(o => o.Kind == "Allergen").Select(o => o.Name),
            AllItems.Select(i => i.Allergens));
        ProteinOptions = MenuOptions.Where(o => o.Kind == "Protein").Select(o => o.Name)
            .Concat(AllItems.Select(i => i.ProteinType))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();

        IEnumerable<MenuItem> filtered = AllItems;
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            filtered = filtered.Where(i =>
                i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(FilterCategory))
            filtered = filtered.Where(i => i.Category == FilterCategory);
        if (FilterAvailability == "Available")
            filtered = filtered.Where(i => i.IsAvailable);
        else if (FilterAvailability == "Unavailable")
            filtered = filtered.Where(i => !i.IsAvailable);

        Items = filtered.ToList();
    }

    private static List<string> Options(IEnumerable<string> configured, IEnumerable<string> existing) =>
        configured.Concat(existing.SelectMany(SplitTags))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IEnumerable<string> SplitTags(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string JoinTags(IEnumerable<string>? values) =>
        string.Join(", ", (values ?? []).Select(v => v.Trim())
            .Where(v => v.Length > 0 && v.Length <= 60 && !v.Contains(','))
            .Distinct(StringComparer.OrdinalIgnoreCase));

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

    private static bool ValidOptionName(string? kind, string? name) =>
        (kind is "Protein" or "Dietary" or "Allergen") &&
        !string.IsNullOrWhiteSpace(name) &&
        name.Length <= (kind == "Protein" ? 40 : 60) &&
        !name.Contains(',');

    public int OptionUsageCount(MenuOption option) => option.Kind switch
    {
        "Protein" => AllItems.Count(i => string.Equals(i.ProteinType, option.Name, StringComparison.OrdinalIgnoreCase)),
        "Dietary" => AllItems.Count(i => SplitTags(i.DietaryTags)
            .Any(tag => string.Equals(tag, option.Name, StringComparison.OrdinalIgnoreCase))),
        "Allergen" => AllItems.Count(i => SplitTags(i.Allergens)
            .Any(tag => string.Equals(tag, option.Name, StringComparison.OrdinalIgnoreCase))),
        _ => 0
    };

    public async Task<IActionResult> OnPostCreateOptionAsync(string? kind, string? name)
    {
        name = name?.Trim();
        if (!ValidOptionName(kind, name))
            return Error("Enter a valid option name (up to 40 characters for proteins or 60 for tags).");

        if (await db.MenuOptions.AnyAsync(o => o.Kind == kind && o.Name == name))
            return Error("That option already exists.");

        db.MenuOptions.Add(new MenuOption { Kind = kind!, Name = name! });
        await db.SaveChangesAsync();
        return Success($"{name} added to the {kind!.ToLowerInvariant()} choices.");
    }

    public async Task<IActionResult> OnPostRenameOptionAsync(int optionId, string? name)
    {
        name = name?.Trim();
        var option = await db.MenuOptions.FindAsync(optionId);
        if (option is null) return NotFound();
        if (!ValidOptionName(option.Kind, name))
            return Error("Enter a valid name for this option.");

        if (await db.MenuOptions.AnyAsync(o => o.MenuOptionID != optionId && o.Kind == option.Kind && o.Name == name))
            return Error("That option already exists.");

        var oldName = option.Name;
        var items = await db.MenuItems.ToListAsync();
        foreach (var item in items)
        {
            if (option.Kind == "Protein")
            {
                if (string.Equals(item.ProteinType, oldName, StringComparison.OrdinalIgnoreCase))
                    item.ProteinType = name!;
            }
            else if (option.Kind == "Dietary")
            {
                item.DietaryTags = JoinTags(SplitTags(item.DietaryTags)
                    .Select(tag => string.Equals(tag, oldName, StringComparison.OrdinalIgnoreCase) ? name! : tag));
                if (item.DietaryTags.Length > 250)
                    return Error("The new tag name would exceed a dish's dietary-tag limit.");
            }
            else
            {
                item.Allergens = JoinTags(SplitTags(item.Allergens)
                    .Select(tag => string.Equals(tag, oldName, StringComparison.OrdinalIgnoreCase) ? name! : tag));
                if (item.Allergens.Length > 300)
                    return Error("The new allergen name would exceed a dish's allergen limit.");
            }
        }

        option.Name = name!;
        await db.SaveChangesAsync();
        return Success($"{oldName} renamed to {name}. Any dishes using it were updated.");
    }

    public async Task<IActionResult> OnPostDeleteOptionAsync(int optionId)
    {
        var option = await db.MenuOptions.FindAsync(optionId);
        if (option is null) return NotFound();
        var items = await db.MenuItems.AsNoTracking().ToListAsync();
        bool inUse = option.Kind switch
        {
            "Protein" => items.Any(i => string.Equals(i.ProteinType, option.Name, StringComparison.OrdinalIgnoreCase)),
            "Dietary" => items.Any(i => SplitTags(i.DietaryTags)
                .Any(tag => string.Equals(tag, option.Name, StringComparison.OrdinalIgnoreCase))),
            "Allergen" => items.Any(i => SplitTags(i.Allergens)
                .Any(tag => string.Equals(tag, option.Name, StringComparison.OrdinalIgnoreCase))),
            _ => true
        };
        if (inUse) return Error("This option is used by a dish. Change those dishes before deleting it.");

        db.MenuOptions.Remove(option);
        await db.SaveChangesAsync();
        return Success($"{option.Name} removed from the list of choices.");
    }

    public async Task<IActionResult> OnPostSaveItemAsync()
    {
        var name = Dish.Name?.Trim();
        var description = Dish.Description?.Trim() ?? "";
        var category = Dish.Category?.Trim();
        var protein = Dish.ProteinType?.Trim() ?? "Vegetarian";
        var dietary = JoinTags(DietarySelections);
        var allergens = JoinTags(AllergenSelections);

        if (!ModelState.IsValid || Dish.MenuItemID < 0 ||
            string.IsNullOrWhiteSpace(name) || name.Length > 100 ||
            string.IsNullOrWhiteSpace(category) || category.Length > 120 ||
            description.Length > 600 || Dish.Price <= 0 || Dish.Price > 100000m ||
            decimal.Round(Dish.Price, 2) != Dish.Price ||
            protein.Length is < 1 or > 40 || dietary.Length > 250 ||
            allergens.Length > 300)
            return Error("Check the dish name, category, price (maximum two decimals), and selected tags.");

        if (DishImage != null && !await ValidImageAsync(DishImage))
            return Error("Choose a valid JPG, PNG or WebP image, up to 5 MB.");

        if (!await db.MenuCategories.AnyAsync(c => c.Name == category))
            return Error("Choose an existing category.");

        var configuredOptions = await db.MenuOptions.AsNoTracking()
            .Select(o => new { o.Kind, o.Name }).ToListAsync();
        bool HasOption(string kind, string selected) => configuredOptions.Any(o =>
            o.Kind == kind && string.Equals(o.Name, selected.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!HasOption("Protein", protein) ||
            DietarySelections.Any(tag => !HasOption("Dietary", tag)) ||
            AllergenSelections.Any(tag => !HasOption("Allergen", tag)))
            return Error("Choose only configured protein, dietary and allergen options.");

        if (await db.MenuItems.AnyAsync(i => i.MenuItemID != Dish.MenuItemID && i.Name == name))
            return Error("A dish with that name already exists.");

        MenuItem item;
        if (Dish.MenuItemID == 0)
        {
            item = new MenuItem { SpiceLevel = 0 };
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
        item.ProteinType = protein;
        item.DietaryTags = dietary;
        item.Allergens = allergens;
        item.IsPopular = Dish.IsPopular;
        item.IsAvailable = Dish.IsAvailable;

        if (DishImage is { Length: > 0 })
        {
            try
            {
                item.ImageUrl = await SaveImageAsync(DishImage);
            }
            catch (IOException)
            {
                return Error("The image could not be saved. Please try uploading it again.");
            }
            catch (UnauthorizedAccessException)
            {
                return Error("The image upload folder is not writable.");
            }
        }

        await db.SaveChangesAsync();
        return Success($"Dish {item.Name} {(Dish.MenuItemID == 0 ? "created" : "updated")}. Changes appear on the customer menu.");
    }

    private static async Task<bool> ValidImageAsync(IFormFile file)
    {
        if (file.Length < 12 || file.Length > 5 * 1024 * 1024)
            return false;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return false;

        var magic = new byte[12];
        try
        {
            await using var input = file.OpenReadStream();
            await input.ReadExactlyAsync(magic);
        }
        catch (IOException)
        {
            return false;
        }

        var jpeg = magic[0] == 0xFF && magic[1] == 0xD8 && magic[2] == 0xFF;
        var png = magic.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var webp = System.Text.Encoding.ASCII.GetString(magic, 0, 4) == "RIFF" &&
            System.Text.Encoding.ASCII.GetString(magic, 8, 4) == "WEBP";

        return extension switch
        {
            ".jpg" or ".jpeg" => jpeg,
            ".png" => png,
            ".webp" => webp,
            _ => false
        };
    }

    private async Task<string> SaveImageAsync(IFormFile file)
    {
        var folder = Path.Combine(host.WebRootPath ?? Path.Combine(host.ContentRootPath, "wwwroot"),
            "images", "menu", "uploads");
        Directory.CreateDirectory(folder);

        var filename = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        var fullPath = Path.Combine(folder, filename);
        await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
        await file.CopyToAsync(output);
        return $"/images/menu/uploads/{filename}";
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
