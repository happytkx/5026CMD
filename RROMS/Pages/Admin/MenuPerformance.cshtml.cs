using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class MenuPerformanceModel(ApplicationDbContext db) : PageModel
{
    public record DishRow(string Name, int Quantity, decimal Sales);

    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; } = ReservationPolicy.Now.Date.AddDays(-29);

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; } = ReservationPolicy.Now.Date;

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SortBy { get; set; } = "MostOrdered";

    public List<string> Categories { get; private set; } = [];
    public List<DishRow> Dishes { get; private set; } = [];
    public int TotalQuantity => Dishes.Sum(d => d.Quantity);
    public decimal TotalSales => Dishes.Sum(d => d.Sales);
    public int NeverOrdered => Dishes.Count(d => d.Quantity == 0);
    public int MaximumQuantity => Dishes.Count == 0 ? 0 : Dishes.Max(d => d.Quantity);
    public bool HasValidPeriod => StartDate.Date <= EndDate.Date && EndDate.Date < DateTime.MaxValue.Date;

    private async Task LoadAsync()
    {
        var namedCategories = await db.MenuCategories.AsNoTracking().Select(c => c.Name).ToListAsync();
        var usedCategories = await db.MenuItems.AsNoTracking().Select(i => i.Category).Distinct().ToListAsync();
        Categories = namedCategories.Concat(usedCategories)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Keep the displayed selections and the report data in sync. Razor's
        // select tag helpers prefer ModelState attempted values over corrected values.
        var selectedCategory = Categories.FirstOrDefault(name =>
            string.Equals(name, Category, StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault();
        if (!string.Equals(Category, selectedCategory, StringComparison.Ordinal))
        {
            Category = selectedCategory;
            ModelState.Remove(nameof(Category));
        }

        if (SortBy is not ("MostOrdered" or "LeastOrdered"))
        {
            SortBy = "MostOrdered";
            ModelState.Remove(nameof(SortBy));
        }

        if (!HasValidPeriod)
        {
            ModelState.AddModelError("", "Please choose a valid reporting period.");
            return;
        }

        if (Category is null) return;

        var menu = await db.MenuItems.AsNoTracking()
            .Where(i => i.Category == Category)
            .Select(i => new { i.MenuItemID, i.Name })
            .ToListAsync();

        var itemIds = menu.Select(i => i.MenuItemID).ToList();
        if (itemIds.Count == 0) return;

        var endExclusive = EndDate.Date.AddDays(1);
        var totals = await db.OrderLines.AsNoTracking()
            .Where(line => itemIds.Contains(line.MenuItemID) &&
                !line.IsCancelled &&
                line.Order.Status != "Cancelled" &&
                line.Order.CreatedAt >= StartDate.Date &&
                line.Order.CreatedAt < endExclusive)
            .GroupBy(line => line.MenuItemID)
            .Select(group => new
            {
                MenuItemID = group.Key,
                Quantity = group.Sum(line => line.Quantity),
                Sales = group.Sum(line => line.LineTotal)
            })
            .ToListAsync();

        var totalsById = totals.ToDictionary(x => x.MenuItemID);
        var data = menu.Select(item =>
        {
            totalsById.TryGetValue(item.MenuItemID, out var count);
            return new DishRow(item.Name, count?.Quantity ?? 0, count?.Sales ?? 0m);
        });

        Dishes = SortBy == "LeastOrdered"
            ? data.OrderBy(d => d.Quantity).ThenBy(d => d.Sales).ThenBy(d => d.Name).ToList()
            : data.OrderByDescending(d => d.Quantity).ThenByDescending(d => d.Sales).ThenBy(d => d.Name).ToList();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnGetExportAsync()
    {
        await LoadAsync();
        if (!HasValidPeriod) return BadRequest("Choose a valid reporting period.");

        static string Escape(string value)
        {
            if (value.TrimStart() is { Length: > 0 } text && "=+-@".Contains(text[0]))
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        var csv = new StringBuilder();
        void Row(params string[] fields) => csv.AppendLine(string.Join(",", fields.Select(Escape)));

        Row("Céleste Menu Performance Report");
        Row("Category", Category ?? "No categories");
        Row("Start date", StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Row("End date", EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Row("Sort", SortBy ?? "MostOrdered");
        Row();
        Row("Dish", "Quantity ordered", "Sales (RM, pre-tax)");
        foreach (var dish in Dishes)
            Row(dish.Name, dish.Quantity.ToString(CultureInfo.InvariantCulture),
                dish.Sales.ToString("0.00", CultureInfo.InvariantCulture));

        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(content, "text/csv; charset=utf-8",
            $"celeste-menu-performance-{StartDate:yyyyMMdd}-{EndDate:yyyyMMdd}.csv");
    }
}
