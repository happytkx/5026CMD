// Standalone Sprint 4 calculation checks. Does not change Happy's
// separate, locally edited Sprint 4 integration-test project.
using RROMS.Services;

static void AssertEqual(decimal expected, decimal actual, string name)
{
    if (expected != actual)
        throw new Exception($"{name}: expected {expected}, got {actual}.");
}

static void RejectInvalid(Action operation, string name)
{
    try
    {
        operation();
    }
    catch (ArgumentOutOfRangeException)
    {
        return;
    }
    throw new Exception($"{name}: invalid input was accepted.");
}

var ordinary = BillingCalculator.Calculate(
    foodSubtotal: 300m, taxAmount: 18m, minSpend: 100m,
    deposit: 100m, depositPaid: true);
AssertEqual(300m, ordinary.FoodSubtotal, "Food subtotal");
AssertEqual(18m, ordinary.TaxAmount, "Recorded tax");
AssertEqual(318m, ordinary.TotalBill, "Grand total");
AssertEqual(100m, ordinary.DepositApplied, "Deposit applied");
AssertEqual(218m, ordinary.BalanceToPay, "Onsite payment");
AssertEqual(0m, ordinary.MinShortFall, "Minimum spending met");

var below = BillingCalculator.Calculate(95m, 5.70m, 100m, 50m, true);
AssertEqual(5m, below.MinShortFall, "Minimum spending excludes tax");

var exact = BillingCalculator.Calculate(100m, 6m, 100m, 50m, true);
AssertEqual(0m, exact.MinShortFall, "Exactly meeting minimum spending");

var unpaidDeposit = BillingCalculator.Calculate(100m, 6m, 50m, 50m, false);
AssertEqual(0m, unpaidDeposit.DepositApplied, "Unpaid deposit not deducted");
AssertEqual(106m, unpaidDeposit.BalanceToPay, "Unpaid deposit balance");

var cappedDeposit = BillingCalculator.Calculate(80m, 4.80m, 0m, 100m, true);
AssertEqual(84.80m, cappedDeposit.DepositApplied, "Deposit capped at total");
AssertEqual(0m, cappedDeposit.BalanceToPay, "Never a negative balance");

// Orders have individually rounded taxes; aggregate their saved values.
var recordedTaxes = new[] { 0.01m, 0.02m, 0.06m };
var combined = BillingCalculator.Calculate(
    1.50m, recordedTaxes.Sum(), 1m, 0m, true);
AssertEqual(0.09m, combined.TaxAmount, "Sum of saved order taxes");
AssertEqual(1.59m, combined.TotalBill, "Multiple-order grand total");

RejectInvalid(() => BillingCalculator.Calculate(-1m, 0m, 0m, 0m, true),
    "Negative subtotal");
RejectInvalid(() => BillingCalculator.Calculate(1m, -0.01m, 0m, 0m, true),
    "Negative tax");
RejectInvalid(() => BillingCalculator.Calculate(1.001m, 0m, 0m, 0m, true),
    "More than two decimal places");

Console.WriteLine("All Sprint 4 billing calculation checks passed.");
