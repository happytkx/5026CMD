
using System.Data;
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

        Reservation? reservation = await _db.Reservations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.ReservationID == reservationId);

        if (reservation == null)
        {
            return new BillSettlementCheck(
                false, "Reservation not found.", 0m, 0m);
        }

        Bill? bill = await GetBillAsync(reservationId);

        if (bill == null)
        {
            return new BillSettlementCheck(
                false, "No saved bill found.", 0m, 0m);
        }

        if (bill.BillStatus != "Draft")
        {
            return new BillSettlementCheck(
                false, "The bill is not an unpaid draft.",
                0m, bill.BalanceToPay);
        }

        if (reservation.ReservationStatus != "Seated")
        {
            return new BillSettlementCheck(
                false, "The customer must be seated.",
                0m, bill.BalanceToPay);
        }

        if (reservation.DepositStatus != "Paid")
        {
            return new BillSettlementCheck(
                false, "The reservation deposit is not paid.",
                0m, bill.BalanceToPay);
        }

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

        if (bill.TotalBill < 0m ||
            bill.MinSpend < 0m ||
            bill.DepositApplied < 0m ||
            bill.BalanceToPay < 0m)
        {
            return new BillSettlementCheck(
                false, "The saved bill contains invalid amounts.",
                0m, 0m);
        }

        BillingCalculation calculation =
            BillingCalculator.Calculate(
                bill.TotalBill,
                reservation.MinimumSpend,
                reservation.DepositAmount,
                true);

        if (bill.MinSpend != reservation.MinimumSpend ||
            bill.DepositApplied != calculation.DepositApplied ||
            bill.BalanceToPay != calculation.BalanceToPay)
        {
            return new BillSettlementCheck(
                false, "The bill amounts need to be recalculated.",
                calculation.MinShortFall,
                calculation.BalanceToPay);
        }

        if (!calculation.MeetMinSpend)
        {
            return new BillSettlementCheck(
                false,
                "Minimum spending has not been met.",
                calculation.MinShortFall,
                calculation.BalanceToPay);
        }

        return new BillSettlementCheck(
            true,
            "The bill is ready for final payment.",
            0m,
            calculation.BalanceToPay);
    }

    // Record a confirmed final payment and mark the bill as Paid
    public async Task<Bill> RecordFinalPaymentAsync(
        int reservationId,
        string paymentMethod,
        string? paymentReference = null)
    {
        if (reservationId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reservationId));
        }

        // Only supported offline payment methods are accepted
        if (paymentMethod is not ("Cash" or "Card" or "EWallet"))
        {
            throw new ArgumentException(
                "Choose Cash, Card or EWallet.",
                nameof(paymentMethod));
        }

        // Use the provided receipt reference or create an internal one
        string reference = string.IsNullOrWhiteSpace(paymentReference)
            ? "FINAL-" + Guid.NewGuid().ToString("N")
            : paymentReference.Trim();

        if (reference.Length > 40)
        {
            throw new ArgumentException(
                "Payment reference cannot exceed 40 characters.",
                nameof(paymentReference));
        }

        // Keep validation, payment recording and bill update together
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        // Recheck the actual database state before settlement
        BillSettlementCheck check =
            await CheckSettlementAsync(reservationId);

        if (!check.CanSettle)
        {
            throw new InvalidOperationException(check.Message);
        }

        // Load the bill for updating
        Bill bill = await _db.Bills
            .SingleAsync(b => b.ReservationID == reservationId);

        // Record only a positive remaining payment
        if (bill.BalanceToPay > 0m)
        {
            Payment payment = new Payment
            {
                ReservationID = reservationId,
                PaymentType = "DiningBill",
                PaymentMethod = paymentMethod,
                PaymentReference = reference,
                Amount = bill.BalanceToPay,
                PaymentStatus = "Paid",
                PaymentDate = ReservationPolicy.Now
            };

            _db.Payments.Add(payment);
        }

        // The entire bill is now settled
        bill.BillStatus = "Paid";

        // Save both changes within the same transaction
        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return bill;
    }
}
