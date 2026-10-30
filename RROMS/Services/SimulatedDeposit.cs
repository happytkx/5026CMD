namespace RROMS.Services;

public static class SimulatedDeposit
{
    public static void Validate(string method, string? phone, string? name, string? number, string? expiry, string? cvv, string? reference)
    {
        bool valid = method switch
        {
            "TNG" => System.Text.RegularExpressions.Regex.IsMatch(phone ?? "", @"^(?:\+?60|0)1[0-9]{8,9}$"),
            "Card" => !string.IsNullOrWhiteSpace(name) && name.Length <= 100 && number == "4242424242424242" && expiry == "12/30" && cvv == "123",
            "Bank Transfer" => !string.IsNullOrWhiteSpace(reference) && reference.Length <= 40 && System.Text.RegularExpressions.Regex.IsMatch(reference, @"^[A-Za-z0-9-]+$"),
            _ => false
        };
        if (!valid) throw new InvalidOperationException("Enter the required simulated payment details. Card payments accept only demo card 4242424242424242, expiry 12/30, CVV 123. Bank references must be 1–40 letters, digits or hyphens.");
    }
}
