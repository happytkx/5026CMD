
namespace RROMS.Services;

// Stores the result of a billing calculation.
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
    // Maximum amount supported by SQL decimal(18,2).
    private const decimal MaxMoney =
        9_999_999_999_999_999.99m;

    // Validate currency amounts before calculation.
    private static void ValidateMoney(
        decimal amount,
        string parameterName)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Amount cannot be negative.");
        }

        if (amount > MaxMoney)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Amount exceeds the supported limit.");
        }

        if (amount != decimal.Round(amount, 2))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Amount cannot exceed two decimal places.");
        }
    }

    public static BillingCalculation Calculate(
        decimal totalBill,
        decimal minSpend,
        decimal deposit,
        bool depositPaid)
    {
        ValidateMoney(totalBill, nameof(totalBill));
        ValidateMoney(minSpend, nameof(minSpend));
        ValidateMoney(deposit, nameof(deposit));

        // Determine whether minimum spending is met.
        decimal shortfall = Math.Max(
            0m, minSpend - totalBill);

        // Deduct only the successfully paid deposit.
        decimal depositApplied = depositPaid
            ? Math.Min(deposit, totalBill)
            : 0m;

        // Calculate the remaining checkout amount.
        decimal balanceToPay =
            totalBill - depositApplied;

        return new BillingCalculation(
            totalBill,
            depositApplied,
            shortfall,
            balanceToPay);
    }
}
