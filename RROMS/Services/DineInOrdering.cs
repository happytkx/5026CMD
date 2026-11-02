// A shared database rule for customer basket additions and seated-reservation checkout.
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Services;

public static class DineInOrdering
{
    public static IQueryable<Reservation> EligibleReservations(ApplicationDbContext db, int customerId)
    {
        // A previous day's Seated record and an already paid bill must not reopen ordering.
        var today = ReservationPolicy.Now.Date;
        return db.Reservations.AsNoTracking()
            .Where(r => r.CustomerID == customerId &&
                        r.ReservationStatus == "Seated" &&
                        r.ReservationDate == today &&
                        !db.Bills.Any(b => b.ReservationID == r.ReservationID && b.BillStatus == "Paid"));
    }
}
