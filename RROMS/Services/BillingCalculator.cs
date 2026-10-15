namespace RROMS.Services;

// Stores the result of the billing calculation
public record BillingCalculation(
    decimal TotalBill,
    decimal DepositApplied,
    decimal MinShortFall,
    decimal BalanceToPay)
{
    public bool MeetMinSpend =>
        MinShortFall == 0m;
}

public static class BillingCalculator
{
    public static BillingCalculation Calculate(
        decimal totalBill,
        decimal minSpend,
        decimal deposit,
        bool depositPaid)

    {
        // Reject invalid amounts
        if (totalBill < 0)
            throw new ArgumentOutOfRangeException(
                nameof(totalBill));

        if (minSpend < 0)
            throw new ArgumentOutOfRangeException(
                nameof(minSpend));

        if (deposit < 0)
            throw new ArgumentOutOfRangeException(
                nameof(deposit));

        // Calculate how much more the customer must spend to meet min spend
        decimal shortfall = Math.Max(
            0m, minSpend - totalBill);

        // Only deduct successfully paid deposit
        decimal depositApplied = depositPaid
            ? Math.Min(deposit, totalBill)
            : 0m;

        // Calculate the remaining payment
        decimal balanceToPay = 
            totalBill - depositApplied;

        return new BillingCalculation(
            totalBill,
            depositApplied,
            shortfall,
            balanceToPay);
    }
}