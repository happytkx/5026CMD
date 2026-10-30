
namespace RROMS.Services;

// Stores the result of a billing calculation.
public record BillingCalculation(
    decimal FoodSubtotal,
    decimal TaxAmount,
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
        decimal foodSubtotal,
        decimal taxAmount,
        decimal minSpend,
        decimal deposit,
        bool depositPaid)
    {
        ValidateMoney(foodSubtotal, nameof(foodSubtotal));
        ValidateMoney(taxAmount, nameof(taxAmount));
        ValidateMoney(minSpend, nameof(minSpend));
        ValidateMoney(deposit, nameof(deposit));

        // Reuse the tax amounts already recorded by the ordering module.
        decimal totalBill = checked(foodSubtotal + taxAmount);
        ValidateMoney(totalBill, nameof(totalBill));

        // Minimum spending excludes tax and the advance deposit.
        decimal shortfall = Math.Max(0m, minSpend - foodSubtotal);

        // A successfully paid deposit is applied once, never as a discount.
        decimal depositApplied = depositPaid
            ? Math.Min(deposit, totalBill)
            : 0m;

        return new BillingCalculation(
            foodSubtotal,
            taxAmount,
            totalBill,
            depositApplied,
            shortfall,
            totalBill - depositApplied);
    }

    // Keep older zero-tax callers compatible. New order billing must use
    // the overload with the saved subtotal and tax.
    public static BillingCalculation Calculate(
        decimal foodSubtotal,
        decimal minSpend,
        decimal deposit,
        bool depositPaid)
        => Calculate(foodSubtotal, 0m, minSpend, deposit, depositPaid);
}
