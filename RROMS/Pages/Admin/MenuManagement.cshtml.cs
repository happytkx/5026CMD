using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

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

    public record TrendPoint(DateTime Date, int Units, decimal Sales);
    public record DishMetrics(int Units, decimal Sales, List<TrendPoint> Trend);
    public record CategoryMetrics(string Name, int Dishes, int Units, decimal Sales);

    public Dictionary<int, DishMetrics> PerformanceById { get; private set; } = [];
    public List<CategoryMetrics> CategoryPerformance { get; private set; } = [];
    public int AvailableDishCount => Items.Count(i => i.IsAvailable);
    public int TotalUnits => Items.Sum(i => MetricsFor(i).Units);
    public decimal TotalSales => Items.Sum(i => MetricsFor(i).Sales);
    public int NoOrderAvailableCount => Items.Count(i => i.IsAvailable && MetricsFor(i).Units == 0);
    public string TrendInterval { get; private set; } = "day";
    public DishMetrics MetricsFor(MenuItem item) => PerformanceById.TryGetValue(item.MenuItemID, out var metric)
        ? metric : new DishMetrics(0, 0m, []);

    [BindProperty(SupportsGet = true)] public DateTime StartDate { get; set; } = ReservationPolicy.Now.Date.AddDays(-29);
    [BindProperty(SupportsGet = true)] public DateTime EndDate { get; set; } = ReservationPolicy.Now.Date;
    [BindProperty(SupportsGet = true)] public string SortBy { get; set; } = "MostOrdered";
    [BindProperty(SupportsGet = true)] public int? FocusItemId { get; set; }
    public bool HasValidPeriod => StartDate.Date <= EndDate.Date && EndDate.Date != DateTime.MaxValue.Date;

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

        if (SortBy is not ("Name" or "MostOrdered" or "LeastOrdered" or "HighestSales"))
        {
            SortBy = "MostOrdered";
            ModelState.Remove(nameof(SortBy));
        }

        Items = filtered.ToList();
        if (!HasValidPeriod)
        {
            ModelState.AddModelError("", "Please choose a valid reporting period.");
            return;
        }

        await LoadPerformanceAsync();
        Items = SortBy switch
        {
            "LeastOrdered" => Items.OrderBy(i => MetricsFor(i).Units).ThenBy(i => i.Name).ToList(),
            "HighestSales" => Items.OrderByDescending(i => MetricsFor(i).Sales).ThenBy(i => i.Name).ToList(),
            "Name" => Items.OrderBy(i => i.Name).ToList(),
            _ => Items.OrderByDescending(i => MetricsFor(i).Units).ThenBy(i => i.Name).ToList()
        };

        CategoryPerformance = Items.GroupBy(i => i.Category)
            .Select(g => new CategoryMetrics(g.Key, g.Count(),
                g.Sum(i => MetricsFor(i).Units), g.Sum(i => MetricsFor(i).Sales)))
            .OrderByDescending(g => g.Units).ThenBy(g => g.Name).ToList();
    }

    private async Task LoadPerformanceAsync()
    {
        var days = (EndDate.Date - StartDate.Date).Days + 1;
        TrendInterval = days <= 45 ? "day" : days <= 365 ? "week" : "month";
        var bucketDates = new List<DateTime>();
        if (TrendInterval == "day")
        {
            for (var date = StartDate.Date; date <= EndDate.Date; date = date.AddDays(1))
                bucketDates.Add(date);
        }
        else if (TrendInterval == "week")
        {
            for (var date = StartDate.Date; date <= EndDate.Date; date = date.AddDays(7))
                bucketDates.Add(date);
        }
        else
        {
            for (var date = new DateTime(StartDate.Year, StartDate.Month, 1);
                date <= EndDate.Date; date = date.AddMonths(1))
                bucketDates.Add(date);
        }

        var allItemIds = AllItems.Select(i => i.MenuItemID).ToList();
        if (allItemIds.Count == 0) return;

        // Use original order-line values and the date the order was placed.
        // Cancelled orders and cancelled order lines do not count as sales.
        var lines = await db.OrderLines.AsNoTracking()
            .Where(l => allItemIds.Contains(l.MenuItemID) && !l.IsCancelled &&
                l.Order.Status != "Cancelled" &&
                l.Order.CreatedAt >= StartDate.Date &&
                l.Order.CreatedAt < EndDate.Date.AddDays(1))
            .Select(l => new { l.MenuItemID, l.Quantity, l.LineTotal, l.Order.CreatedAt })
            .ToListAsync();

        DateTime BucketFor(DateTime date) => TrendInterval switch
        {
            "week" => StartDate.Date.AddDays(((date.Date - StartDate.Date).Days / 7) * 7),
            "month" => new DateTime(date.Year, date.Month, 1),
            _ => date.Date
        };

        foreach (var item in AllItems)
        {
            var itemLines = lines.Where(l => l.MenuItemID == item.MenuItemID).ToList();
            var perBucket = itemLines.GroupBy(l => BucketFor(l.CreatedAt))
                .ToDictionary(g => g.Key, g => (Units: g.Sum(l => l.Quantity), Sales: g.Sum(l => l.LineTotal)));
            var trend = bucketDates.Select(date => perBucket.TryGetValue(date, out var amount)
                ? new TrendPoint(date, amount.Units, amount.Sales)
                : new TrendPoint(date, 0, 0m)).ToList();
            PerformanceById[item.MenuItemID] = new DishMetrics(
                itemLines.Sum(l => l.Quantity), itemLines.Sum(l => l.LineTotal), trend);
        }
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        await OnGetAsync();
        if (!HasValidPeriod) return BadRequest("Choose a valid reporting period.");

        static string Escape(string value)
        {
            var trimmed = value.TrimStart();
            if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder();
        void Row(params string[] fields) => csv.AppendLine(string.Join(",", fields.Select(Escape)));
        Row("Celeste Menu Management & Performance Report");
        Row("Start date", StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Row("End date", EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Row("Search", string.IsNullOrWhiteSpace(Search) ? "All dishes" : Search.Trim());
        Row("Category filter", FilterCategory ?? "All");
        Row("Availability filter", FilterAvailability ?? "All");
        Row("Sort", SortBy);
        Row();
        Row("Dish", "Category", "Current price (RM)", "Availability", "Units sold", "Sales before tax (RM)");
        foreach (var item in Items)
        {
            var metrics = MetricsFor(item);
            Row(item.Name, item.Category, item.Price.ToString("0.00", CultureInfo.InvariantCulture),
                item.IsAvailable ? "Available" : "Unavailable",
                metrics.Units.ToString(CultureInfo.InvariantCulture),
                metrics.Sales.ToString("0.00", CultureInfo.InvariantCulture));
        }
        Row();
        Row("Notes", "Sales use original order-line prices and exclude cancelled orders and lines.");
        Row("Notes", "Current menu prices and availability may differ from their historical values.");
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(content, "text/csv; charset=utf-8",
            $"celeste-menu-performance-{StartDate:yyyyMMdd}-{EndDate:yyyyMMdd}.csv");
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
        if (FilterCategory == previousName) FilterCategory = name;
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
        return Success($"Dish {item.Name} {(Dish.MenuItemID == 0 ? "created" : "updated")}. Changes appear on the customer menu.", item.MenuItemID);
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
        return Success($"{item.Name} is now {(item.IsAvailable ? "available" : "unavailable")}.", item.MenuItemID);
    }

    private static bool ValidCategoryName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 120;

    private IActionResult ReturnToWorkspace(int? focusId = null) => RedirectToPage(new
    {
        Search,
        FilterCategory,
        FilterAvailability,
        SortBy,
        StartDate = StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        EndDate = EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        FocusItemId = focusId
    });

    private IActionResult Error(string message)
    {
        TempData["Error"] = message;
        return ReturnToWorkspace(Dish.MenuItemID > 0 ? Dish.MenuItemID : null);
    }

    private IActionResult Success(string message, int? focusId = null)
    {
        TempData["Success"] = message;
        return ReturnToWorkspace(focusId);
    }
}
