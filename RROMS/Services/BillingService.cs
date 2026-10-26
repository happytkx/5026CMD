
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Services;

// Stores the result of checking whether a bill can be settled
public record BillSettlementCheck(
    bool CanSettle,
    string Message,
    decimal MinimumSpendShortfall,
    decimal BalanceToPay);

public class BillingService
{
    private readonly ApplicationDbContext _db;

    public BillingService(ApplicationDbContext db)
    {
        _db = db;
    }

    // Calculate and create a draft bill in memory
    public Bill CreateDraft(
        Reservation reservation,
        decimal totalBill)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        if (reservation.ReservationStatus != "Seated")
        {
            throw new InvalidOperationException(
                "The customer must be seated before billing.");
        }

        if (reservation.DepositStatus != "Paid")
        {
            throw new InvalidOperationException(
                "The reservation deposit has not been paid.");
        }

        BillingCalculation calculation =
            BillingCalculator.Calculate(
                totalBill,
                reservation.MinimumSpend,
                reservation.DepositAmount,
                true);

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

    // Save a new draft or update an existing draft bill
    public async Task<Bill> SaveDraftAsync(
        int reservationId,
        decimal orderTotal)
    {
        if (reservationId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reservationId));
        }

        if (orderTotal < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderTotal));
        }

        Reservation reservation = await _db.Reservations
            .SingleOrDefaultAsync(r => r.ReservationID == reservationId)
            ?? throw new InvalidOperationException(
                "Reservation not found.");

        Bill? existingBill = await _db.Bills
            .SingleOrDefaultAsync(b => b.ReservationID == reservationId);

        if (existingBill != null && existingBill.BillStatus != "Draft")
        {
            throw new InvalidOperationException(
                "Only draft bills can be updated.");
        }

        Bill draft = CreateDraft(reservation, orderTotal);

        if (existingBill == null)
        {
            _db.Bills.Add(draft);

            await _db.SaveChangesAsync();

            return draft;
        }

        existingBill.TotalBill = draft.TotalBill;
        existingBill.MinSpend = draft.MinSpend;
        existingBill.DepositApplied = draft.DepositApplied;
        existingBill.BalanceToPay = draft.BalanceToPay;

        await _db.SaveChangesAsync();

        return existingBill;
    }

    // Retrieve a saved bill by reservation ID
    public async Task<Bill?> GetBillAsync(int reservationId)
    {
        return await _db.Bills
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.ReservationID == reservationId);
    }

    // Check whether a saved bill is ready for final payment
    public async Task<BillSettlementCheck> CheckSettlementAsync(
        int reservationId)
    {
        if (reservationId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reservationId));
        }

        // Retrieve the reservation
        Reservation? reservation = await _db.Reservations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.ReservationID == reservationId);

        if (reservation == null)
        {
            return new BillSettlementCheck(
                false, "Reservation not found.", 0m, 0m);
        }

        // Retrieve the saved bill
        Bill? bill = await GetBillAsync(reservationId);

        if (bill == null)
        {
            return new BillSettlementCheck(
                false, "No saved bill found.", 0m, 0m);
        }

        // Check the bill status
        if (bill.BillStatus != "Draft")
        {
            return new BillSettlementCheck(
                false, "The bill is not an unpaid draft.",
                0m, bill.BalanceToPay);
        }

        // Check that the customer is seated
        if (reservation.ReservationStatus != "Seated")
        {
            return new BillSettlementCheck(
                false, "The customer must be seated.",
                0m, bill.BalanceToPay);
        }

        // Check the reservation deposit status
        if (reservation.DepositStatus != "Paid")
        {
            return new BillSettlementCheck(
                false, "The reservation deposit is not paid.",
                0m, bill.BalanceToPay);
        }

        // Verify that a successful deposit transaction exists
        bool depositRecorded = await _db.Payments
            .AsNoTracking()
            .AnyAsync(p =>
                p.ReservationID == reservationId &&
                p.PaymentType == "Deposit" &&
                p.PaymentStatus == "Paid" &&
                p.Amount == reservation.DepositAmount);

        if (!depositRecorded)
        {
            return new BillSettlementCheck(
                false, "No matching paid deposit record found.",
                0m, bill.BalanceToPay);
        }

        // Prevent settling a previously paid dining bill
        bool finalPaymentRecorded = await _db.Payments
            .AsNoTracking()
            .AnyAsync(p =>
                p.ReservationID == reservationId &&
                p.PaymentType == "DiningBill" &&
                p.PaymentStatus == "Paid");

        if (finalPaymentRecorded)
        {
            return new BillSettlementCheck(
                false, "A final payment is already recorded.",
                0m, bill.BalanceToPay);
        }

        // Reject invalid saved amounts
        if (bill.TotalBill < 0m ||
            bill.MinSpend < 0m ||
            bill.DepositApplied < 0m ||
            bill.BalanceToPay < 0m)
        {
            return new BillSettlementCheck(
                false, "The saved bill contains invalid amounts.",
                0m, 0m);
        }

        // Recalculate amounts using the reservation data
        BillingCalculation calculation =
            BillingCalculator.Calculate(
                bill.TotalBill,
                reservation.MinimumSpend,
                reservation.DepositAmount,
                true);

        // Check that the saved figures are consistent
        if (bill.MinSpend != reservation.MinimumSpend ||
            bill.DepositApplied != calculation.DepositApplied ||
            bill.BalanceToPay != calculation.BalanceToPay)
        {
            return new BillSettlementCheck(
                false, "The bill amounts need to be recalculated.",
                calculation.MinShortFall,
                calculation.BalanceToPay);
        }

        // Prevent settlement below minimum spending
        if (!calculation.MeetMinSpend)
        {
            return new BillSettlementCheck(
                false,
                "Minimum spending has not been met.",
                calculation.MinShortFall,
                calculation.BalanceToPay);
        }

        // All settlement checks passed
        return new BillSettlementCheck(
            true,
            "The bill is ready for final payment.",
            0m,
            calculation.BalanceToPay);
    }
}
