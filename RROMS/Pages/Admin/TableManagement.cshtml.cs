
using System.Data;
using System.Globalization;
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
public class TableManagementModel : PageModel
{
    private readonly ApplicationDbContext _db;

    private readonly string[] _supportedAreas =
    {
        "Indoor", "Outdoor", "VIP Room"
    };

    private const string TableIdPattern = @"^(T|VIP)(0[1-9]|1[0-9]|2[0-5])$";

    public TableManagementModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<DiningArea> Areas { get; private set; } = new();
    public List<RestaurantTable> Tables { get; private set; } = new();
    public Dictionary<string, int> UpcomingBookings { get; private set; } = new();

    public int VipRoomCount =>
        Tables.Count(t => t.DiningArea.AreaType == "VIP Room");

    public int AvailableCount =>
        Tables.Count(t => t.Status == "Available");

    [BindProperty(SupportsGet = true)]
    public DateTime? CheckDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CheckTime { get; set; }

    public bool HasAvailabilityResult { get; private set; }
    public string? AvailabilityError { get; private set; }

    public List<AvailabilityEntry> AvailabilityResults { get; private set; } = new();

    public int BookableCount =>
        AvailabilityResults.Count(t => t.IsBookable);

    public class AvailabilityEntry
    {
        public string TableId { get; set; }
        public string Area { get; set; }
        public int Capacity { get; set; }
        public string State { get; set; }

        public bool IsBookable => State == "Available";

        public AvailabilityEntry(string tableId, string area, int capacity, string state)
        {
            TableId = tableId;
            Area = area;
            Capacity = capacity;
            State = state;
        }
    }

    public async Task OnGetAsync()
    {
        await LoadTablesAsync();
        await LoadUpcomingBookingsAsync();

        if (CheckDate.HasValue || !string.IsNullOrWhiteSpace(CheckTime))
        {
            await CheckSlotAvailabilityAsync();
        }
    }

    private async Task LoadTablesAsync()
    {
        Areas = await _db.DiningAreas
            .AsNoTracking()
            .Where(a => _supportedAreas.Contains(a.AreaType))
            .OrderBy(a => a.DiningAreaID)
            .ToListAsync();

        Tables = await _db.RestaurantTables
            .AsNoTracking()
            .Include(t => t.DiningArea)
            .OrderBy(t => t.DiningAreaID)
            .ThenBy(t => t.TableID)
            .ToListAsync();
    }

    private async Task LoadUpcomingBookingsAsync()
    {
        DateTime now = ReservationPolicy.Now;

        var reservations = await _db.Reservations
            .AsNoTracking()
            .Where(r => r.ReservationDate >= now.Date &&
                (r.ReservationStatus == "Pending" ||
                 r.ReservationStatus == "Confirmed" ||
                 r.ReservationStatus == "Seated"))
            .Select(r => new
            {
                r.TableID,
                r.ReservationDate,
                r.EndTime,
                r.ReservationStatus,
                r.HoldExpiresAt
            })
            .ToListAsync();

        var activeBookings = reservations.Where(r =>
            r.ReservationDate.Add(r.EndTime) > now &&
            (r.ReservationStatus != "Pending" ||
             r.HoldExpiresAt > now));

        UpcomingBookings = activeBookings
            .GroupBy(r => r.TableID)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task CheckSlotAvailabilityAsync()
    {
        TimeSpan arrivalTime;

        bool validTime = TimeSpan.TryParseExact(
            CheckTime,
            @"hh\:mm",
            CultureInfo.InvariantCulture,
            out arrivalTime);

        if (!CheckDate.HasValue || !validTime ||
            arrivalTime < TimeSpan.Zero ||
            arrivalTime >= TimeSpan.FromDays(1) ||
            arrivalTime.Ticks % TimeSpan.FromMinutes(30).Ticks != 0)
        {
            AvailabilityError =
                "Choose a date and an arrival time in 30-minute increments.";
            return;
        }

        DateTime date = CheckDate.Value.Date;
        DateTime start = date.Add(arrivalTime);
        DateTime now = ReservationPolicy.Now;

        if (start <= now)
        {
            AvailabilityError =
                "Choose a future date and time (Malaysia time).";
            return;
        }

        var settings = await _db.RestaurantSettings
            .AsNoTracking()
            .OrderBy(s => s.SettingID)
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            AvailabilityError =
                "Restaurant opening hours have not been configured.";
            return;
        }

        if (arrivalTime < settings.OpeningTime ||
            arrivalTime >= settings.ClosingTime)
        {
            AvailabilityError =
                $"Choose an arrival time between " +
                $"{settings.OpeningTime.ToString(@"hh\:mm")} and " +
                $"{settings.ClosingTime.ToString(@"hh\:mm")}.";
            return;
        }

        var reservations = await _db.Reservations
            .AsNoTracking()
            .Where(r => r.ReservationDate == date &&
                (r.ReservationStatus == "Confirmed" ||
                 r.ReservationStatus == "Seated" ||
                 (r.ReservationStatus == "Pending" &&
                  r.HoldExpiresAt > now)))
            .Select(r => new
            {
                r.TableID,
                r.StartTime,
                r.EndTime,
                r.ReservationStatus
            })
            .ToListAsync();

        foreach (var table in Tables)
        {
            string area = table.DiningArea.AreaType;

            if (!_supportedAreas.Contains(area))
            {
                continue;
            }

            DateTime end = ReservationPolicy.End(
                start, area, settings.ClosingTime);

            bool withinHours =
                end > start &&
                end.Date == date &&
                end.TimeOfDay <= settings.ClosingTime;

            var overlappingBooking = reservations.FirstOrDefault(r =>
                r.TableID == table.TableID &&
                ReservationPolicy.Overlaps(
                    start,
                    end,
                    date.Add(r.StartTime),
                    date.Add(r.EndTime)));

            string state = "Available";

            if (table.Status != "Available")
            {
                state = "Disabled";
            }
            else if (!withinHours)
            {
                state = "Too late for dining";
            }
            else if (overlappingBooking != null)
            {
                if (overlappingBooking.ReservationStatus == "Pending")
                {
                    state = "Payment hold";
                }
                else if (overlappingBooking.ReservationStatus == "Seated")
                {
                    state = "Occupied";
                }
                else
                {
                    state = "Reserved";
                }
            }
            else if (!ReservationPolicy.MeetsAdvance(area, start, now))
            {
                state = "Advance notice required";
            }

            AvailabilityResults.Add(new AvailabilityEntry(
                table.TableID,
                area,
                table.Capacity,
                state));
        }

        HasAvailabilityResult = true;
    }

    private bool IsValidCapacity(string area, int capacity)
    {
        if (area == "Indoor" || area == "Outdoor")
        {
            return capacity == 2 || capacity == 4 ||
                   capacity == 6 || capacity == 8;
        }

        if (area == "VIP Room")
        {
            return capacity == 8 || capacity == 12;
        }

        return false;
    }

    private IActionResult ShowError(string message)
    {
        TempData["TableManagementError"] = message;
        return RedirectToPage();
    }

    private IActionResult ShowSuccess(string message)
    {
        TempData["TableManagementSuccess"] = message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddTableAsync(
        string? tableId, int diningAreaId, int capacity)
    {
        tableId = tableId?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(tableId) ||
            !Regex.IsMatch(tableId, TableIdPattern))
        {
            return ShowError(
                "Table ID must be T01–T25 or VIP01–VIP25.");
        }

        var area = await _db.DiningAreas.FindAsync(diningAreaId);

        if (area == null || !_supportedAreas.Contains(area.AreaType))
        {
            return ShowError("Select an existing dining area.");
        }

        if (area.AreaType == "VIP Room" &&
            !tableId.StartsWith("VIP"))
        {
            return ShowError("VIP rooms must use a VIP table ID.");
        }

        if (area.AreaType != "VIP Room" &&
            !tableId.StartsWith("T"))
        {
            return ShowError("Regular tables must use a T table ID.");
        }

        if (!IsValidCapacity(area.AreaType, capacity))
        {
            return ShowError(
                "Ordinary tables must seat 2, 4, 6 or 8 guests; " +
                "VIP rooms must seat 8 or 12.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        bool duplicate = await _db.RestaurantTables
            .AnyAsync(t => t.TableID.ToUpper() == tableId);

        if (duplicate)
        {
            return ShowError("That table ID already exists.");
        }

        if (area.AreaType == "VIP Room")
        {
            var vipRooms = await _db.RestaurantTables
                .Where(t => t.DiningArea.AreaType == "VIP Room")
                .ToListAsync();

            if (vipRooms.Count >= 2)
            {
                return ShowError(
                    "Céleste has exactly two VIP rooms. " +
                    "Edit an existing room instead.");
            }

            if (vipRooms.Any(t => t.Capacity == capacity))
            {
                return ShowError(
                    "The two VIP rooms must have distinct " +
                    "capacities: 8 and 12 guests.");
            }
        }

        RestaurantTable newTable = new RestaurantTable
        {
            TableID = tableId,
            DiningAreaID = diningAreaId,
            Capacity = capacity,
            Status = "Available"
        };

        _db.RestaurantTables.Add(newTable);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return ShowError(
                "Unable to create the table. " +
                "Check that the ID is unique and retry.");
        }

        return ShowSuccess(
            $"Table {tableId} added to {area.AreaType}.");
    }

    public async Task<IActionResult> OnPostToggleTableAsync(string? tableId)
    {
        if (string.IsNullOrWhiteSpace(tableId))
        {
            return ShowError("Select a table to enable or disable.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var table = await _db.RestaurantTables
            .SingleOrDefaultAsync(t => t.TableID == tableId);

        if (table == null)
        {
            return NotFound();
        }

        if (table.Status == "Available")
        {
            bool hasBookings = await HasUpcomingBookingsAsync(table.TableID);

            if (hasBookings)
            {
                return ShowError(
                    $"Table {table.TableID} has active or upcoming " +
                    "reservations. Resolve them before disabling it.");
            }

            table.Status = "Unavailable";
        }
        else if (table.Status == "Unavailable")
        {
            table.Status = "Available";
        }
        else
        {
            return ShowError(
                "This table has an unsupported availability status.");
        }

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return ShowError(
                "Unable to change table availability. Refresh and retry.");
        }

        return ShowSuccess(
            $"Table {table.TableID} is now " +
            $"{table.Status.ToLowerInvariant()} for new reservations.");
    }

    private async Task<bool> HasUpcomingBookingsAsync(string tableId)
    {
        DateTime now = ReservationPolicy.Now;

        var reservations = await _db.Reservations
            .AsNoTracking()
            .Where(r => r.TableID == tableId &&
                r.ReservationDate >= now.Date &&
                (r.ReservationStatus == "Pending" ||
                 r.ReservationStatus == "Confirmed" ||
                 r.ReservationStatus == "Seated"))
            .ToListAsync();

        return reservations.Any(r =>
            r.ReservationDate.Add(r.EndTime) > now &&
            (r.ReservationStatus != "Pending" ||
             r.HoldExpiresAt > now));
    }

    public async Task<IActionResult> OnPostUpdateTableAsync(
        string? tableId, int diningAreaId, int capacity, string? status)
    {
        if (string.IsNullOrWhiteSpace(tableId) ||
            (status != "Available" && status != "Unavailable"))
        {
            return ShowError(
                "Select a valid table and availability status.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var table = await _db.RestaurantTables
            .Include(t => t.DiningArea)
            .SingleOrDefaultAsync(t => t.TableID == tableId);

        if (table == null)
        {
            return NotFound();
        }

        var area = await _db.DiningAreas.FindAsync(diningAreaId);

        if (area == null || !_supportedAreas.Contains(area.AreaType))
        {
            return ShowError("Select an existing dining area.");
        }

        if (!IsValidCapacity(area.AreaType, capacity))
        {
            return ShowError(
                "Ordinary tables must seat 2, 4, 6 or 8 guests; " +
                "VIP rooms must seat 8 or 12.");
        }

        bool changingArea = table.DiningAreaID != diningAreaId;

        if (changingArea)
        {
            bool hasHistory = await _db.Reservations
                .AnyAsync(r => r.TableID == table.TableID);

            if (hasHistory)
            {
                return ShowError(
                    "This table has reservation history. " +
                    "Its dining area cannot be changed; " +
                    "add a new table instead.");
            }
        }

        if (area.AreaType == "VIP Room" &&
            (changingArea || table.Capacity != capacity))
        {
            var otherVipRooms = await _db.RestaurantTables
                .Where(t => t.TableID != table.TableID &&
                    t.DiningArea.AreaType == "VIP Room")
                .ToListAsync();

            if (changingArea && otherVipRooms.Count >= 2)
            {
                return ShowError(
                    "Céleste has exactly two VIP rooms. " +
                    "No additional VIP rooms can be assigned.");
            }

            if (otherVipRooms.Any(t => t.Capacity == capacity))
            {
                return ShowError(
                    "The two VIP rooms must have distinct " +
                    "capacities: 8 and 12 guests.");
            }
        }

        DateTime now = ReservationPolicy.Now;

        var reservations = await _db.Reservations
            .Where(r => r.TableID == table.TableID &&
                r.ReservationDate >= now.Date &&
                (r.ReservationStatus == "Pending" ||
                 r.ReservationStatus == "Confirmed" ||
                 r.ReservationStatus == "Seated"))
            .ToListAsync();

        var upcoming = reservations.Where(r =>
            r.ReservationDate.Add(r.EndTime) > now &&
            (r.ReservationStatus != "Pending" ||
             r.HoldExpiresAt > now))
            .ToList();

        if (status == "Unavailable" && upcoming.Count > 0)
        {
            return ShowError(
                "This table has active or upcoming reservations. " +
                "Resolve them before making it unavailable.");
        }

        foreach (var reservation in upcoming)
        {
            bool suitable = ReservationPolicy.SuitableCapacity(
                reservation.PartySize,
                area.AreaType,
                capacity);

            if (!suitable)
            {
                return ShowError(
                    "The new capacity would make an existing " +
                    "reservation unsuitable.");
            }
        }

        table.DiningAreaID = diningAreaId;
        table.Capacity = capacity;
        table.Status = status;

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return ShowError(
                "This table could not be updated. Refresh and retry.");
        }

        return ShowSuccess(
            $"Table {table.TableID} updated successfully.");
    }
}
