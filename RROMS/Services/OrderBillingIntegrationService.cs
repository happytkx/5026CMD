
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
                "All active dishes must be served before preparing the final bill.");
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

        // Sprint 3 stores tax per order. Sum those recorded amounts
        // rather than charging tax again on the combined subtotal.
        if (snapshot.FoodSubtotal is not decimal foodSubtotal ||
            snapshot.TaxAmount is not decimal taxAmount ||
            snapshot.OrderTotal is not decimal recordedTotal)
        {
            throw new InvalidOperationException(
                "The dining orders do not contain complete billing totals.");
        }

        if (foodSubtotal != orderTotal)
        {
            throw new InvalidOperationException(
                "The saved order subtotal does not match its line items.");
        }

        if (foodSubtotal < 0m || taxAmount < 0m ||
            recordedTotal != checked(foodSubtotal + taxAmount))
        {
            throw new InvalidOperationException(
                "The saved order subtotal, tax and total do not agree.");
        }

        // Minimum spending uses the food subtotal; the amount to pay
        // includes the existing tax and deducts the paid deposit.
        return await _billingService.SaveDraftAsync(
            reservationId,
            foodSubtotal,
            taxAmount);
    }
}
