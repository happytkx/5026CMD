// Finds suitable tables in the chosen area, ordered by smallest sufficient capacity, excluding overlapping active bookings and unexpired payment holds.
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Services;

public class TableAllocationService(ApplicationDbContext context)
{
    public async Task<List<RestaurantTable>> Available(int size, string area, DateTime start,
        DateTime end, int? excluding = null)
    {
        if (!ReservationPolicy.Areas(size).Contains(area))
            return [];
        var tables = await context.RestaurantTables.Include(t => t.DiningArea)
            .Where(t => t.DiningArea.AreaType == area && t.Capacity >= size && t.Status == "Available")
            .OrderBy(t => t.Capacity).ThenBy(t => t.TableID).ToListAsync();
        var bookings = await context.Reservations.Where(r => r.ReservationID != excluding &&
            r.ReservationDate == start.Date &&
            (r.ReservationStatus == "Confirmed" || r.ReservationStatus == "Seated" ||
             (r.ReservationStatus == "Pending" && r.HoldExpiresAt > ReservationPolicy.Now)))
            .ToListAsync();
        return tables.Where(t => !bookings.Any(r => r.TableID == t.TableID &&
            ReservationPolicy.Overlaps(start, end, r.ReservationDate.Add(r.StartTime),
                r.ReservationDate.Add(r.EndTime)))).ToList();
    }
}
