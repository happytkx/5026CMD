
namespace RROMS.Services;

// Represents one food or beverage item included in billing
public sealed record OrderBillingLine(
    string ItemName,
    int Quantity,
    decimal UnitPrice);

// Represents the orders belonging to one reservation
public sealed record OrderBillingSnapshot(
    int ReservationID,
    bool IsReadyForBilling,
    IReadOnlyList<OrderBillingLine> Lines,
    decimal? FoodSubtotal = null,
    decimal? TaxAmount = null,
    decimal? OrderTotal = null);

// Provides order information to Sprint 4 Billing.
// Sprint 3 provides these totals from its saved dine-in order records.
public interface ReservationOrderBillingSource
{
    Task<OrderBillingSnapshot?> GetSnapshotAsync(
        int reservationId,
        CancellationToken cancellationToken = default);
}
