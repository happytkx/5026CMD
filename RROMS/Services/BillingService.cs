
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Services;

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
        // Check that a reservation was provided
        ArgumentNullException.ThrowIfNull(reservation);

        // Billing is only available after check-in
        if (reservation.ReservationStatus != "Seated")
        {
            throw new InvalidOperationException(
                "The customer must be seated before billing.");
        }

        // Ensure the deposit was paid
        if (reservation.DepositStatus != "Paid")
        {
            throw new InvalidOperationException(
                "The reservation deposit has not been paid.");
        }

        // Calculate the bill using reservation data
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

    // Save a new draft or update an existing draft bill
    public async Task<Bill> SaveDraftAsync(
        int reservationId,
        decimal orderTotal)
    {
        // Reject invalid reservation IDs and amounts
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

        // Retrieve the reservation from the database
        Reservation reservation = await _db.Reservations
            .SingleOrDefaultAsync(r => r.ReservationID == reservationId)
            ?? throw new InvalidOperationException(
                "Reservation not found.");

        // Check whether a bill already exists
        Bill? existingBill = await _db.Bills
            .SingleOrDefaultAsync(b => b.ReservationID == reservationId);

        // Prevent modifying bills that are no longer drafts
        if (existingBill != null && existingBill.BillStatus != "Draft")
        {
            throw new InvalidOperationException(
                "Only draft bills can be updated.");
        }

        // Calculate a fresh draft bill
        Bill draft = CreateDraft(reservation, orderTotal);

        // Save a new bill if none exists
        if (existingBill == null)
        {
            _db.Bills.Add(draft);

            await _db.SaveChangesAsync();

            return draft;
        }

        // Update the existing draft using the latest order total
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
}
