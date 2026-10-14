// Reads the logged-in customer ID from authentication claims so customer pages and operations can restrict access to the owner of a reservation.
using System.Security.Claims;
namespace RROMS.Services;

public static class ReservationIdentity
{
    public static int CustomerId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("CustomerID") ?? user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : -1;
}
