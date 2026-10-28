
using RROMS.Models;

namespace RROMS.Services;

public class OrderBillingIntegrationService
{
    private readonly ReservationOrderBillingSource _orderSource;
    private readonly BillingService _billingService;

    public OrderBillingIntegrationService(
        ReservationOrderBillingSource orderSource,
        BillingService billingService)
    {
        _orderSource = orderSource;
        _billingService = billingService;
    }

    // Generate or update a draft bill using
    // verified server-side order information.
    public async Task<Bill> SaveDraftFromOrdersAsync(
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reservationId));
        }

        // Obtain order information from Sprint 3.
        OrderBillingSnapshot? snapshot =
            await _orderSource.GetSnapshotAsync(
                reservationId, cancellationToken);

        if (snapshot == null)
        {
            throw new InvalidOperationException(
                "No order information is available.");
        }

        // Verify that the orders belong to this reservation.
        if (snapshot.ReservationID != reservationId)
        {
            throw new InvalidOperationException(
                "Order reservation does not match.");
        }

        // Unfinished orders cannot be used for final billing.
        if (!snapshot.IsReadyForBilling)
        {
            throw new InvalidOperationException(
                "The order is not ready for billing.");
        }

        if (snapshot.Lines == null ||
            snapshot.Lines.Count == 0)
        {
            throw new InvalidOperationException(
                "No billable order items were found.");
        }

        decimal orderTotal = 0m;

        foreach (OrderBillingLine line in snapshot.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ItemName))
            {
                throw new InvalidOperationException(
                    "An order item has no name.");
            }

            if (line.Quantity <= 0)
            {
                throw new InvalidOperationException(
                    "An order item has an invalid quantity.");
            }

            if (line.UnitPrice < 0m ||
                line.UnitPrice !=
                    decimal.Round(line.UnitPrice, 2))
            {
                throw new InvalidOperationException(
                    "An order item has an invalid price.");
            }

            // Calculate total using the recorded order prices.
            orderTotal = checked(
                orderTotal +
                line.Quantity * line.UnitPrice);
        }

        // Reuse the existing Sprint 4 billing service.
        // This calculates the deposit deduction,
        // balance to pay and minimum spending rules.
        return await _billingService.SaveDraftAsync(
            reservationId,
            orderTotal);
    }
}
