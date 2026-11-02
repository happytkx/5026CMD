// Administrator table inventory: edit dining-area assignment, capacity and availability
// while protecting current/future reservations and the fixed two-room VIP policy.
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class TableManagementModel(ApplicationDbContext db) : PageModel
{
    private static readonly string[] SupportedAreas = ["Indoor", "Outdoor", "VIP Room"];
    private static readonly Regex TableIdFormat = new("^[A-Za-z0-9-]{1,10}$", RegexOptions.Compiled);

    public List<DiningArea> Areas { get; private set; } = [];
    public List<RestaurantTable> Tables { get; private set; } = [];
    public Dictionary<string, int> UpcomingBookings { get; private set; } = [];
    public int VipRoomCount => Tables.Count(t => t.DiningArea.AreaType == "VIP Room");
    public int AvailableCount => Tables.Count(t => t.Status == "Available");

    public async Task OnGetAsync()
    {
        Areas = await db.DiningAreas.AsNoTracking()
            .Where(a => SupportedAreas.Contains(a.AreaType))
            .OrderBy(a => a.DiningAreaID).ToListAsync();
        Tables = await db.RestaurantTables.AsNoTracking().Include(t => t.DiningArea)
            .OrderBy(t => t.DiningAreaID).ThenBy(t => t.TableID).ToListAsync();

        var now = ReservationPolicy.Now;
        var candidates = await db.Reservations.AsNoTracking()
            .Where(r => r.ReservationDate >= now.Date &&
                (r.ReservationStatus == "Pending" || r.ReservationStatus == "Confirmed" ||
                 r.ReservationStatus == "Seated"))
            .Select(r => new
            {
                r.TableID, r.ReservationDate, r.EndTime, r.ReservationStatus, r.HoldExpiresAt
            }).ToListAsync();
        UpcomingBookings = candidates
            .Where(r => r.ReservationDate.Add(r.EndTime) > now &&
                (r.ReservationStatus != "Pending" || r.HoldExpiresAt > now))
            .GroupBy(r => r.TableID)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static bool AllowedCapacity(string area, int capacity) => area switch
    {
        "Indoor" or "Outdoor" => capacity is 2 or 4 or 6 or 8,
        "VIP Room" => capacity is 8 or 12,
        _ => false
    };

    private IActionResult Error(string message)
    {
        TempData["TableManagementError"] = message;
        return RedirectToPage();
    }

    private IActionResult Success(string message)
    {
        TempData["TableManagementSuccess"] = message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddTableAsync(string? tableId, int diningAreaId, int capacity)
    {
        tableId = tableId?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(tableId) || !TableIdFormat.IsMatch(tableId))
            return Error("Enter a table ID of 1–10 letters, numbers or hyphens.");

        var area = await db.DiningAreas.FindAsync(diningAreaId);
        if (area is null || !SupportedAreas.Contains(area.AreaType))
            return Error("Select an existing dining area.");
        if (!AllowedCapacity(area.AreaType, capacity))
            return Error("Ordinary tables must seat 2, 4, 6 or 8 guests; VIP rooms must seat 8 or 12.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.RestaurantTables.AnyAsync(t => t.TableID.ToUpper() == tableId))
            return Error("That table ID already exists.");
        if (area.AreaType == "VIP Room")
        {
            var vipRooms = await db.RestaurantTables
                .Where(t => t.DiningArea.AreaType == "VIP Room").ToListAsync();
            if (vipRooms.Count >= 2)
                return Error("Céleste has exactly two VIP rooms. Edit an existing room instead.");
            if (vipRooms.Any(t => t.Capacity == capacity))
                return Error("The two VIP rooms must have distinct capacities: 8 and 12 guests.");
        }

        db.RestaurantTables.Add(new RestaurantTable
        {
            TableID = tableId,
            DiningAreaID = diningAreaId,
            Capacity = capacity,
            Status = "Available"
        });
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Error("Unable to create the table. Check that the ID is unique and retry.");
        }
        return Success($"Table {tableId} added to {area.AreaType}.");
    }

    public async Task<IActionResult> OnPostUpdateTableAsync(
        string? tableId, int diningAreaId, int capacity, string? status)
    {
        if (string.IsNullOrWhiteSpace(tableId) || status is not ("Available" or "Unavailable"))
            return Error("Select a valid table and availability status.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var table = await db.RestaurantTables.Include(t => t.DiningArea)
            .SingleOrDefaultAsync(t => t.TableID == tableId);
        if (table is null) return NotFound();

        var area = await db.DiningAreas.FindAsync(diningAreaId);
        if (area is null || !SupportedAreas.Contains(area.AreaType))
            return Error("Select an existing dining area.");
        if (!AllowedCapacity(area.AreaType, capacity))
            return Error("Ordinary tables must seat 2, 4, 6 or 8 guests; VIP rooms must seat 8 or 12.");

        var movingArea = table.DiningAreaID != diningAreaId;
        if (movingArea && await db.Reservations.AnyAsync(r => r.TableID == table.TableID))
            return Error("This table has reservation history. Its dining area cannot be changed; add a new table instead.");
        if (area.AreaType == "VIP Room" && (movingArea || table.Capacity != capacity))
        {
            var otherVipRooms = await db.RestaurantTables
                .Where(t => t.TableID != table.TableID && t.DiningArea.AreaType == "VIP Room")
                .ToListAsync();
            if (movingArea && otherVipRooms.Count >= 2)
                return Error("Céleste has exactly two VIP rooms. No additional VIP rooms can be assigned.");
            if (otherVipRooms.Any(t => t.Capacity == capacity))
                return Error("The two VIP rooms must have distinct capacities: 8 and 12 guests.");
        }

        var now = ReservationPolicy.Now;
        var bookings = await db.Reservations
            .Where(r => r.TableID == table.TableID && r.ReservationDate >= now.Date &&
                (r.ReservationStatus == "Pending" || r.ReservationStatus == "Confirmed" ||
                 r.ReservationStatus == "Seated"))
            .ToListAsync();
        var upcoming = bookings.Where(r => r.ReservationDate.Add(r.EndTime) > now &&
            (r.ReservationStatus != "Pending" || r.HoldExpiresAt > now)).ToList();

        if (status != "Available" && upcoming.Count > 0)
            return Error("This table has active or upcoming reservations. Resolve them before making it unavailable.");
        if (upcoming.Any(r => !ReservationPolicy.SuitableCapacity(r.PartySize, area.AreaType, capacity)))
            return Error("The new capacity would make an existing reservation unsuitable.");

        table.DiningAreaID = diningAreaId;
        table.Capacity = capacity;
        table.Status = status;
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Error("This table could not be updated. Refresh and retry.");
        }
        return Success($"Table {table.TableID} updated successfully.");
    }
}
