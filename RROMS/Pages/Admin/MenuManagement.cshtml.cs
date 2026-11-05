
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
public class MenuManagementModel : PageModel
{
    private readonly ApplicationDbContext db;
    private readonly IWebHostEnvironment host;

    public MenuManagementModel(ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        db = context;
        host = environment;
    }

    public List<MenuCategory> Categories { get; private set; } = new();
    public List<MenuItem> AllItems { get; private set; } = new();
    public List<MenuItem> Items { get; private set; } = new();

    public List<string> DietaryOptions { get; private set; } = new();
    public List<string> AllergenOptions { get; private set; } = new();
    public List<string> ProteinOptions { get; private set; } = new();
    public List<MenuOption> MenuOptions { get; private set; } = new();

    public record TrendPoint(DateTime Date, int Units, decimal Sales);
    public record DishMetrics(int Units, decimal Sales, List<TrendPoint> Trend);
    public record CategoryMetrics(string Name, int Dishes, int Units, decimal Sales);

    public Dictionary<int, DishMetrics> PerformanceById { get; private set; } = new();
    public List<CategoryMetrics> CategoryPerformance { get; private set; } = new();

    public int AvailableDishCount
    {
        get { return Items.Count(i => i.IsAvailable); }
    }

    public int TotalUnits
    {
        get { return Items.Sum(i => MetricsFor(i).Units); }
    }

    public decimal TotalSales
    {
        get { return Items.Sum(i => MetricsFor(i).Sales); }
    }

    public int NoOrderAvailableCount
    {
        get
        {
            return Items.Count(i => i.IsAvailable && MetricsFor(i).Units == 0);
        }
    }

    public string TrendInterval { get; private set; } = "day";
    public int TrendUnitsAxisStep { get; private set; } = 1;
    public decimal TrendSalesAxisStep { get; private set; } = 1m;

    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; } =
        ReservationPolicy.Now.Date.AddDays(-29);

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; } =
        ReservationPolicy.Now.Date;

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "MostOrdered";

    [BindProperty(SupportsGet = true)]
    public int? FocusItemId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterCategory { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterAvailability { get; set; }

    [BindProperty]
    public IFormFile? DishImage { get; set; }

    [BindProperty]
    public string[] DietarySelections { get; set; } = Array.Empty<string>();

    [BindProperty]
    public string[] AllergenSelections { get; set; } = Array.Empty<string>();

    [BindProperty]
    public MenuItemInput Dish { get; set; } = new MenuItemInput();

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

    // Load menu categories, dishes, options and reports.
    public async Task OnGetAsync()
    {
        Categories = await db.MenuCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        AllItems = await db.MenuItems
            .AsNoTracking()
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ToListAsync();

        MenuOptions = await db.MenuOptions
            .AsNoTracking()
            .OrderBy(o => o.Kind)
            .ThenBy(o => o.Name)
            .ToListAsync();

        DietaryOptions = Options(
            MenuOptions.Where(o => o.Kind == "Dietary").Select(o => o.Name),
            AllItems.Select(i => i.DietaryTags));

        AllergenOptions = Options(
            MenuOptions.Where(o => o.Kind == "Allergen").Select(o => o.Name),
            AllItems.Select(i => i.Allergens));

        ProteinOptions = MenuOptions
            .Where(o => o.Kind == "Protein")
            .Select(o => o.Name)
            .Concat(AllItems.Select(i => i.ProteinType))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        IEnumerable<MenuItem> filtered = AllItems;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            string term = Search.Trim();

            filtered = filtered.Where(i =>
                i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(FilterCategory))
        {
            filtered = filtered.Where(i => i.Category == FilterCategory);
        }

        if (FilterAvailability == "Available")
        {
            filtered = filtered.Where(i => i.IsAvailable);
        }
        else if (FilterAvailability == "Unavailable")
        {
            filtered = filtered.Where(i => !i.IsAvailable);
        }

        if (SortBy != "Name" &&
            SortBy != "MostOrdered" &&
            SortBy != "LeastOrdered" &&
            SortBy != "HighestSales")
        {
            SortBy = "MostOrdered";
        }

        ModelState.Remove(nameof(SortBy));

        Items = filtered.ToList();

        if (!HasValidPeriod)
        {
            ModelState.AddModelError("", "Please choose a valid reporting period.");
            return;
        }

        await LoadPerformanceAsync();

        if (SortBy == "LeastOrdered")
        {
            Items = Items.OrderBy(i => MetricsFor(i).Units)
                .ThenBy(i => i.Name).ToList();
        }
        else if (SortBy == "HighestSales")
        {
            Items = Items.OrderByDescending(i => MetricsFor(i).Sales)
                .ThenBy(i => i.Name).ToList();
        }
        else if (SortBy == "Name")
        {
            Items = Items.OrderBy(i => i.Name).ToList();
        }
        else
        {
            Items = Items.OrderByDescending(i => MetricsFor(i).Units)
                .ThenBy(i => i.Name).ToList();
        }

        CategoryPerformance = Items.GroupBy(i => i.Category)
            .Select(g => new CategoryMetrics(
                g.Key,
                g.Count(),
                g.Sum(i => MetricsFor(i).Units),
                g.Sum(i => MetricsFor(i).Sales)))
            .OrderByDescending(g => g.Units)
            .ThenBy(g => g.Name)
            .ToList();
    }

    public bool HasValidPeriod
    {
        get
        {
            return StartDate.Date <= EndDate.Date &&
                EndDate.Date != DateTime.MaxValue.Date;
        }
    }

    private static decimal NiceAxisStep(decimal peak, bool wholeUnits = false)
    {
        if (peak <= 0m)
        {
            return 1m;
        }

        double rawStep = (double)peak / 5d;
        double magnitude = Math.Pow(10d, Math.Floor(Math.Log10(rawStep)));
        double scaled = rawStep / magnitude;

        double factor;

        if (scaled <= 1d)
        {
            factor = 1d;
        }
        else if (scaled <= 2d)
        {
            factor = 2d;
        }
        else if (scaled <= 5d)
        {
            factor = 5d;
        }
        else
        {
            factor = 10d;
        }

        decimal step = (decimal)(factor * magnitude);

        if (wholeUnits)
        {
            return Math.Max(1m, decimal.Ceiling(step));
        }

        return step;
    }

    public DishMetrics MetricsFor(MenuItem item)
    {
        if (PerformanceById.TryGetValue(item.MenuItemID, out var metric))
        {
            return metric;
        }

        return new DishMetrics(0, 0m, new List<TrendPoint>());
    }

    // Calculate menu sales performance.
    private async Task LoadPerformanceAsync()
    {
        int days = (EndDate.Date - StartDate.Date).Days + 1;

        if (days <= 45)
        {
            TrendInterval = "day";
        }
        else if (days <= 365)
        {
            TrendInterval = "week";
        }
        else
        {
            TrendInterval = "month";
        }

        List<DateTime> bucketDates = new List<DateTime>();

        if (TrendInterval == "day")
        {
            for (DateTime date = StartDate.Date;
                date <= EndDate.Date;
                date = date.AddDays(1))
            {
                bucketDates.Add(date);
            }
        }
        else if (TrendInterval == "week")
        {
            for (DateTime date = StartDate.Date;
                date <= EndDate.Date;
                date = date.AddDays(7))
            {
                bucketDates.Add(date);
            }
        }
        else
        {
            for (DateTime date = new DateTime(StartDate.Year, StartDate.Month, 1);
                date <= EndDate.Date;
                date = date.AddMonths(1))
            {
                bucketDates.Add(date);
            }
        }

        List<int> allItemIds = AllItems
            .Select(i => i.MenuItemID)
            .ToList();

        if (allItemIds.Count == 0)
        {
            return;
        }

        var lines = await db.OrderLines
            .AsNoTracking()
            .Where(l => allItemIds.Contains(l.MenuItemID) &&
                !l.IsCancelled &&
                l.Order.Status != "Cancelled" &&
                l.Order.CreatedAt >= StartDate.Date &&
                l.Order.CreatedAt < EndDate.Date.AddDays(1))
            .Select(l => new
            {
                l.MenuItemID,
                l.Quantity,
                l.LineTotal,
                l.Order.CreatedAt
            })
            .ToListAsync();

        DateTime BucketFor(DateTime date)
        {
            if (TrendInterval == "week")
            {
                int offset = (date.Date - StartDate.Date).Days / 7;
                return StartDate.Date.AddDays(offset * 7);
            }

            if (TrendInterval == "month")
            {
                return new DateTime(date.Year, date.Month, 1);
            }

            return date.Date;
        }

        foreach (MenuItem item in AllItems)
        {
            var itemLines = lines
                .Where(l => l.MenuItemID == item.MenuItemID)
                .ToList();

            var perBucket = itemLines
                .GroupBy(l => BucketFor(l.CreatedAt))
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Units: g.Sum(l => l.Quantity),
                        Sales: g.Sum(l => l.LineTotal)
                    ));

            List<TrendPoint> trend = new List<TrendPoint>();

            foreach (DateTime date in bucketDates)
            {
                if (perBucket.TryGetValue(date, out var amount))
                {
                    trend.Add(new TrendPoint(
                        date, amount.Units, amount.Sales));
                }
                else
                {
                    trend.Add(new TrendPoint(date, 0, 0m));
                }
            }

            int units = itemLines.Sum(l => l.Quantity);
            decimal sales = itemLines.Sum(l => l.LineTotal);

            PerformanceById[item.MenuItemID] =
                new DishMetrics(units, sales, trend);
        }

        var allPoints = PerformanceById.Values
            .SelectMany(metrics => metrics.Trend)
            .ToList();

        TrendUnitsAxisStep = (int)NiceAxisStep(
            allPoints.Max(point => point.Units), true);

        TrendSalesAxisStep = NiceAxisStep(
            allPoints.Max(point => point.Sales));
    }

    // Export menu performance to CSV.
    public async Task<IActionResult> OnGetExportAsync()
    {
        await OnGetAsync();

        if (!HasValidPeriod)
        {
            return BadRequest("Choose a valid reporting period.");
        }

        static string Escape(string value)
        {
            string trimmed = value.TrimStart();

            if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0]))
            {
                value = "'" + value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        StringBuilder csv = new StringBuilder();

        void Row(params string[] fields)
        {
            csv.AppendLine(string.Join(",", fields.Select(Escape)));
        }

        Row("Celeste Menu Management & Performance Report");
        Row("Start date", StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Row("End date", EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Row("Search", string.IsNullOrWhiteSpace(Search) ? "All dishes" : Search.Trim());
        Row("Category filter", FilterCategory ?? "All");
        Row("Availability filter", FilterAvailability ?? "All");
        Row("Sort", SortBy);
        Row();

        Row("Dish", "Category", "Current price (RM)",
            "Availability", "Units sold", "Sales before tax (RM)");

        foreach (MenuItem item in Items)
        {
            DishMetrics metrics = MetricsFor(item);

            Row(
                item.Name,
                item.Category,
                item.Price.ToString("0.00", CultureInfo.InvariantCulture),
                item.IsAvailable ? "Available" : "Unavailable",
                metrics.Units.ToString(CultureInfo.InvariantCulture),
                metrics.Sales.ToString("0.00", CultureInfo.InvariantCulture)
            );
        }

        Row();
        Row("Notes", "Sales use original order-line prices and exclude cancelled orders and lines.");
        Row("Notes", "Current menu prices and availability may differ from their historical values.");

        byte[] content = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        return File(
            content,
            "text/csv; charset=utf-8",
            $"celeste-menu-performance-{StartDate:yyyyMMdd}-{EndDate:yyyyMMdd}.csv"
        );
    }

    // Convert dietary and allergen tags into lists.
    private static IEnumerable<string> SplitTags(string? value)
    {
        return (value ?? "").Split(
            ',',
            StringSplitOptions.TrimEntries |
            StringSplitOptions.RemoveEmptyEntries
        );
    }

    private static string JoinTags(IEnumerable<string>? values)
    {
        return string.Join(", ",
            (values ?? Array.Empty<string>())
                .Select(v => v.Trim())
                .Where(v => v.Length > 0 &&
                    v.Length <= 60 &&
                    !v.Contains(','))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> Options(
        IEnumerable<string> configured,
        IEnumerable<string> existing)
    {
        return configured
            .Concat(existing.SelectMany(SplitTags))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // US08: Add a menu category.
    public async Task<IActionResult> OnPostCreateCategoryAsync(string? name)
    {
        string categoryName = name?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(categoryName) ||
            categoryName.Length > 120)
        {
            return Error("Category name must be between 1 and 120 characters.");
        }

        bool exists = await db.MenuCategories
            .AnyAsync(c => c.Name == categoryName);

        if (exists)
        {
            return Error("That category already exists.");
        }

        MenuCategory category = new MenuCategory();
        category.Name = categoryName;

        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();

        return Success($"Category {categoryName} created.");
    }

    // US08: Rename an existing category.
    public async Task<IActionResult> OnPostRenameCategoryAsync(
        int categoryId, string? name)
    {
        string newName = name?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(newName) ||
            newName.Length > 120)
        {
            return Error("Category name must be between 1 and 120 characters.");
        }

        MenuCategory? category = await db.MenuCategories
            .FirstOrDefaultAsync(c => c.MenuCategoryID == categoryId);

        if (category == null)
        {
            return NotFound();
        }

        bool duplicate = await db.MenuCategories
            .AnyAsync(c => c.MenuCategoryID != categoryId &&
                c.Name == newName);

        if (duplicate)
        {
            return Error("That category already exists.");
        }

        string oldName = category.Name;

        List<MenuItem> dishes = await db.MenuItems
            .Where(i => i.MenuCategoryID == categoryId)
            .ToListAsync();

        category.Name = newName;

        foreach (MenuItem dish in dishes)
        {
            dish.Category = newName;
        }

        await db.SaveChangesAsync();

        if (FilterCategory == oldName)
        {
            FilterCategory = newName;
        }

        return Success(
            $"Category renamed to {newName}. Its dishes were moved automatically.");
    }

    // US08: Delete a category only when it contains no dishes.
    public async Task<IActionResult> OnPostDeleteCategoryAsync(int categoryId)
    {
        MenuCategory? category = await db.MenuCategories
            .FirstOrDefaultAsync(c => c.MenuCategoryID == categoryId);

        if (category == null)
        {
            return NotFound();
        }

        bool hasDishes = await db.MenuItems
            .AnyAsync(i => i.MenuCategoryID == categoryId);

        if (hasDishes)
        {
            return Error(
                "Move or remove the dishes in this category before deleting it.");
        }

        db.MenuCategories.Remove(category);
        await db.SaveChangesAsync();

        return Success("Empty category deleted.");
    }

    // Validate configurable menu options.
    private static bool ValidOptionName(string? kind, string? name)
    {
        if (kind != "Protein" &&
            kind != "Dietary" &&
            kind != "Allergen")
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        int maximumLength = kind == "Protein" ? 40 : 60;

        if (name.Length > maximumLength || name.Contains(','))
        {
            return false;
        }

        return true;
    }

    public int OptionUsageCount(MenuOption option)
    {
        if (option.Kind == "Protein")
        {
            return AllItems.Count(i =>
                string.Equals(i.ProteinType, option.Name,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (option.Kind == "Dietary")
        {
            return AllItems.Count(i =>
                SplitTags(i.DietaryTags).Any(tag =>
                    string.Equals(tag, option.Name,
                        StringComparison.OrdinalIgnoreCase)));
        }

        if (option.Kind == "Allergen")
        {
            return AllItems.Count(i =>
                SplitTags(i.Allergens).Any(tag =>
                    string.Equals(tag, option.Name,
                        StringComparison.OrdinalIgnoreCase)));
        }

        return 0;
    }

    // Add protein, dietary or allergen options.
    public async Task<IActionResult> OnPostCreateOptionAsync(
        string? kind, string? name)
    {
        name = name?.Trim();

        if (!ValidOptionName(kind, name))
        {
            return Error(
                "Enter a valid option name (up to 40 characters for proteins or 60 for tags).");
        }

        bool exists = await db.MenuOptions
            .AnyAsync(o => o.Kind == kind && o.Name == name);

        if (exists)
        {
            return Error("That option already exists.");
        }

        MenuOption option = new MenuOption();
        option.Kind = kind!;
        option.Name = name!;

        db.MenuOptions.Add(option);
        await db.SaveChangesAsync();

        return Success(
            $"{name} added to the {kind!.ToLowerInvariant()} choices.");
    }

    // Rename an option and update dishes using that option.
    public async Task<IActionResult> OnPostRenameOptionAsync(
        int optionId, string? name)
    {
        name = name?.Trim();

        MenuOption? option = await db.MenuOptions
            .FirstOrDefaultAsync(o => o.MenuOptionID == optionId);

        if (option == null)
        {
            return NotFound();
        }

        if (!ValidOptionName(option.Kind, name))
        {
            return Error("Enter a valid name for this option.");
        }

        bool duplicate = await db.MenuOptions.AnyAsync(o =>
            o.MenuOptionID != optionId &&
            o.Kind == option.Kind &&
            o.Name == name);

        if (duplicate)
        {
            return Error("That option already exists.");
        }

        string oldName = option.Name;

        List<MenuItem> items = await db.MenuItems.ToListAsync();

        foreach (MenuItem item in items)
        {
            if (option.Kind == "Protein")
            {
                if (string.Equals(item.ProteinType, oldName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    item.ProteinType = name!;
                }
            }
            else if (option.Kind == "Dietary")
            {
                item.DietaryTags = JoinTags(
                    SplitTags(item.DietaryTags)
                        .Select(tag =>
                            string.Equals(tag, oldName,
                                StringComparison.OrdinalIgnoreCase)
                                ? name!
                                : tag));

                if (item.DietaryTags.Length > 250)
                {
                    return Error(
                        "The new tag name would exceed a dish's dietary-tag limit.");
                }
            }
            else if (option.Kind == "Allergen")
            {
                item.Allergens = JoinTags(
                    SplitTags(item.Allergens)
                        .Select(tag =>
                            string.Equals(tag, oldName,
                                StringComparison.OrdinalIgnoreCase)
                                ? name!
                                : tag));

                if (item.Allergens.Length > 300)
                {
                    return Error(
                        "The new allergen name would exceed a dish's allergen limit.");
                }
            }
        }

        option.Name = name!;
        await db.SaveChangesAsync();

        return Success(
            $"{oldName} renamed to {name}. Any dishes using it were updated.");
    }

    // Delete unused options.
    public async Task<IActionResult> OnPostDeleteOptionAsync(int optionId)
    {
        MenuOption? option = await db.MenuOptions
            .FirstOrDefaultAsync(o => o.MenuOptionID == optionId);

        if (option == null)
        {
            return NotFound();
        }

        List<MenuItem> items = await db.MenuItems
            .AsNoTracking()
            .ToListAsync();

        bool inUse = false;

        if (option.Kind == "Protein")
        {
            inUse = items.Any(i =>
                string.Equals(i.ProteinType, option.Name,
                    StringComparison.OrdinalIgnoreCase));
        }
        else if (option.Kind == "Dietary")
        {
            inUse = items.Any(i =>
                SplitTags(i.DietaryTags).Any(tag =>
                    string.Equals(tag, option.Name,
                        StringComparison.OrdinalIgnoreCase)));
        }
        else if (option.Kind == "Allergen")
        {
            inUse = items.Any(i =>
                SplitTags(i.Allergens).Any(tag =>
                    string.Equals(tag, option.Name,
                        StringComparison.OrdinalIgnoreCase)));
        }
        else
        {
            inUse = true;
        }

        if (inUse)
        {
            return Error(
                "This option is used by a dish. Change those dishes before deleting it.");
        }

        db.MenuOptions.Remove(option);
        await db.SaveChangesAsync();

        return Success(
            $"{option.Name} removed from the list of choices.");
    }

    // US28 and US29: Create or update a menu item.
    public async Task<IActionResult> OnPostSaveItemAsync()
    {
        string? name = Dish.Name?.Trim();
        string description = Dish.Description?.Trim() ?? "";
        string? category = Dish.Category?.Trim();
        string protein = Dish.ProteinType?.Trim() ?? "Vegetarian";

        string dietary = JoinTags(DietarySelections);
        string allergens = JoinTags(AllergenSelections);

        if (!ModelState.IsValid ||
            Dish.MenuItemID < 0 ||
            string.IsNullOrWhiteSpace(name) ||
            name.Length > 100 ||
            string.IsNullOrWhiteSpace(category) ||
            category.Length > 120 ||
            description.Length > 600 ||
            Dish.Price <= 0 ||
            Dish.Price > 100000m ||
            decimal.Round(Dish.Price, 2) != Dish.Price ||
            protein.Length < 1 ||
            protein.Length > 40 ||
            dietary.Length > 250 ||
            allergens.Length > 300)
        {
            return Error(
                "Check the dish name, category, price (maximum two decimals), and selected tags.");
        }

        if (DishImage != null)
        {
            bool validImage = await ValidImageAsync(DishImage);

            if (!validImage)
            {
                return Error(
                    "Choose a valid JPG, PNG or WebP image, up to 5 MB.");
            }
        }

        bool categoryExists = await db.MenuCategories
            .AnyAsync(c => c.Name == category);

        if (!categoryExists)
        {
            return Error("Choose an existing category.");
        }

        var configuredOptions = await db.MenuOptions
            .AsNoTracking()
            .Select(o => new { o.Kind, o.Name })
            .ToListAsync();

        bool HasOption(string kind, string selected)
        {
            return configuredOptions.Any(o =>
                o.Kind == kind &&
                string.Equals(o.Name, selected.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!HasOption("Protein", protein) ||
            DietarySelections.Any(tag => !HasOption("Dietary", tag)) ||
            AllergenSelections.Any(tag => !HasOption("Allergen", tag)))
        {
            return Error(
                "Choose only configured protein, dietary and allergen options.");
        }

        bool duplicate = await db.MenuItems.AnyAsync(i =>
            i.MenuItemID != Dish.MenuItemID &&
            i.Name == name);

        if (duplicate)
        {
            return Error("A dish with that name already exists.");
        }

        MenuItem item;

        if (Dish.MenuItemID == 0)
        {
            item = new MenuItem();
            item.SpiceLevel = 0;

            db.MenuItems.Add(item);
        }
        else
        {
            MenuItem? existing = await db.MenuItems
                .FirstOrDefaultAsync(i =>
                    i.MenuItemID == Dish.MenuItemID);

            if (existing == null)
            {
                return NotFound();
            }

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

        if (DishImage != null && DishImage.Length > 0)
        {
            try
            {
                item.ImageUrl = await SaveImageAsync(DishImage);
            }
            catch (IOException)
            {
                return Error(
                    "The image could not be saved. Please try uploading it again.");
            }
            catch (UnauthorizedAccessException)
            {
                return Error("The image upload folder is not writable.");
            }
        }

        await db.SaveChangesAsync();

        string action = Dish.MenuItemID == 0 ? "created" : "updated";

        return Success(
            $"Dish {item.Name} {action}. Changes appear on the customer menu.",
            item.MenuItemID);
    }

    // Check image file extension, size and format.
    private static async Task<bool> ValidImageAsync(IFormFile file)
    {
        if (file.Length < 12 ||
            file.Length > 5 * 1024 * 1024)
        {
            return false;
        }

        string extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        if (extension != ".jpg" &&
            extension != ".jpeg" &&
            extension != ".png" &&
            extension != ".webp")
        {
            return false;
        }

        byte[] magic = new byte[12];

        try
        {
            await using var input = file.OpenReadStream();
            await input.ReadExactlyAsync(magic);
        }
        catch (IOException)
        {
            return false;
        }

        bool jpeg = magic[0] == 0xFF &&
            magic[1] == 0xD8 &&
            magic[2] == 0xFF;

        bool png = magic.AsSpan(0, 8)
            .SequenceEqual(new byte[]
            {
                137, 80, 78, 71, 13, 10, 26, 10
            });

        bool webp =
            Encoding.ASCII.GetString(magic, 0, 4) == "RIFF" &&
            Encoding.ASCII.GetString(magic, 8, 4) == "WEBP";

        if (extension == ".jpg" || extension == ".jpeg")
        {
            return jpeg;
        }

        if (extension == ".png")
        {
            return png;
        }

        if (extension == ".webp")
        {
            return webp;
        }

        return false;
    }

    // Save the uploaded menu image.
    private async Task<string> SaveImageAsync(IFormFile file)
    {
        string webRoot = host.WebRootPath ??
            Path.Combine(host.ContentRootPath, "wwwroot");

        string folder = Path.Combine(
            webRoot, "images", "menu", "uploads");

        Directory.CreateDirectory(folder);

        string filename =
            Guid.NewGuid().ToString("N") +
            Path.GetExtension(file.FileName).ToLowerInvariant();

        string fullPath = Path.Combine(folder, filename);

        await using var output = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write);

        await file.CopyToAsync(output);

        return "/images/menu/uploads/" + filename;
    }

    // US28: Enable or disable a menu item.
    public async Task<IActionResult> OnPostToggleAvailabilityAsync(int itemId)
    {
        MenuItem? item = await db.MenuItems
            .FirstOrDefaultAsync(i => i.MenuItemID == itemId);

        if (item == null)
        {
            return NotFound();
        }

        item.IsAvailable = !item.IsAvailable;

        await db.SaveChangesAsync();

        string availability = item.IsAvailable
            ? "available"
            : "unavailable";

        return Success(
            $"{item.Name} is now {availability}.",
            item.MenuItemID);
    }

    // Delete menu items that are not linked to orders.
    public async Task<IActionResult> OnPostDeleteItemAsync(int itemId)
    {
        MenuItem? item = await db.MenuItems
            .FirstOrDefaultAsync(i => i.MenuItemID == itemId);

        if (item == null)
        {
            return NotFound();
        }

        bool hasOrders = await db.OrderLines
            .AnyAsync(line => line.MenuItemID == itemId);

        if (hasOrders)
        {
            return Error(
                "This dish is linked to orders and cannot be deleted. Use Disable to stop customers ordering it while preserving order history.");
        }

        db.MenuItems.Remove(item);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Microsoft.Data.SqlClient.SqlException
            { Number: 547 })
        {
            return Error(
                "This dish is now linked to another record and cannot be deleted. Use Disable instead.");
        }

        return Success($"Dish {item.Name} deleted.");
    }

    // Return to the Menu Management page with current filters.
    private IActionResult ReturnToWorkspace(int? focusId = null)
    {
        return RedirectToPage(new
        {
            Search,
            FilterCategory,
            FilterAvailability,
            SortBy,
            StartDate = StartDate.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = EndDate.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture),
            FocusItemId = focusId
        });
    }

    private IActionResult Error(string message)
    {
        TempData["Error"] = message;

        int? focusId = null;

        if (Dish.MenuItemID > 0)
        {
            focusId = Dish.MenuItemID;
        }

        return ReturnToWorkspace(focusId);
    }

    private IActionResult Success(string message, int? focusId = null)
    {
        TempData["Success"] = message;
        return ReturnToWorkspace(focusId);
    }
}
