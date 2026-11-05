using System.Text.RegularExpressions;
namespace RROMS.Services;

public static class SimulatedDeposit
{
    public static void Validate(string method, string? tngAccountName, string? name, string? number, string? expiry, string? cvv, string? reference)
    {
        switch (method)
        {
            case "TNG":
                var accountName = tngAccountName?.Trim();
                if (string.IsNullOrWhiteSpace(accountName) || accountName.Length < 2 || accountName.Length > 100 ||
                    !Regex.IsMatch(accountName, @"\p{L}"))
                    throw new InvalidOperationException("Enter the full name on your Touch 'n Go account (2 to 100 characters).");
                break;
            case "Card":
                if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
                    throw new InvalidOperationException("Enter the cardholder name (up to 100 characters).");
                // Format validation only: this is a simulation, not bank/card verification.
                var digits = (number ?? "").Replace(" ", "").Replace("-", "");
                if (number?.Length > 32 || !Regex.IsMatch(digits, @"^[0-9]{13,19}$"))
                    throw new InvalidOperationException("Enter a card number containing 13 to 19 digits. Spaces and hyphens are allowed.");
                var match = Regex.Match(expiry?.Trim() ?? "", @"^(0[1-9]|1[0-2])/([0-9]{2})$");
                var now = ReservationPolicy.Now;
                if (!match.Success || new DateTime(2000 + int.Parse(match.Groups[2].Value), int.Parse(match.Groups[1].Value), 1) < new DateTime(now.Year, now.Month, 1))
                    throw new InvalidOperationException("Enter a current or future expiry date in MM/YY format.");
                if (!Regex.IsMatch(cvv ?? "", @"^[0-9]{3,4}$"))
                    throw new InvalidOperationException("Enter a 3 or 4 digit security code.");
                break;
            case "Bank Transfer":
                if (string.IsNullOrWhiteSpace(reference) || reference.Length > 40 || !Regex.IsMatch(reference, @"^[A-Za-z0-9-]+$"))
                    throw new InvalidOperationException("Enter a bank transaction reference of 1 to 40 letters, digits or hyphens.");
                break;
            default:
                throw new InvalidOperationException("Select a payment method and enter its required details. If the method selector is missing, reload the current payment page.");
        }
    }
}
