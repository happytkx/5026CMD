using RROMS.Models;

namespace RROMS.Services;

public class BillingService
{
    public Bill CreateDraft(
        Reservation reservation,
        decimal totalBill)

    {
        // Check that a reservation was provided
        ArgumentNullException.ThrowIfNull(reservation);

        // Billing is only available after check-in
        if (reservation.ReservationStatus != "Seated")
        {
            throw new InvalidOperationException(
                "The customer must be seated before billing.");
        }

        // The reservation deposit must have been paid
        if (reservation.DepositStatus != "Paid")
        {
            throw new InvalidOperationException(
                "The reservation deposit has not been paid.");
        }

        // Calculate the bill using existing reservation data
        BillingCalculation calculation =
            BillingCalculator.Calculate(
                totalBill,
                reservation.MinimumSpend,
                reservation.DepositAmount,
                true);

        // Create a draft bill
        Bill bill = new Bill
        {
            ReservationID = reservation.ReservationID,
            TotalBill = calculation.TotalBill,
            MinSpend = reservation.MinimumSpend,
            DepositApplied = calculation.DepositApplied,
            BalanceToPay = calculation.BalanceToPay,
            BillStatus = "Draft"
        };

        return bill;

    }
}