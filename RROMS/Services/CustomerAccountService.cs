// Customer-only credentials and single-use email confirmation; never grants staff/admin roles.
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Services;

public class CustomerAccountService(ApplicationDbContext db, IPasswordHasher<Customer> passwords,
    IEmailSender sender, IConfiguration configuration, ILogger<CustomerAccountService> logger,
    IWebHostEnvironment? environment = null)
{
    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
    private static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<Customer> Register(string name, string email, string phone, string password)
    {
        var normalized = Normalize(email);
        // Do not allow public signup to take over a legacy customer and its reservations.
        if (await db.Customers.AnyAsync(c => c.NormalizedEmail == normalized || c.Email.ToUpper() == normalized)
            || await db.Administrators.AnyAsync(a => a.NormalizedEmail == normalized)
            || await db.StaffAccounts.AnyAsync(a => a.NormalizedEmail == normalized))
            throw new InvalidOperationException("This email is already registered. Please sign in or contact the restaurant for help.");
        var customer = new Customer { CustomerName = name.Trim(), Email = email.Trim(),
            PhoneNo = phone.Trim(), NormalizedEmail = normalized };
        customer.PasswordHash = passwords.HashPassword(customer, password);
        db.Customers.Add(customer);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new InvalidOperationException("Unable to create this account. Please try signing in or use another email."); }
        return customer;
    }

    public async Task<bool> SendVerification(Customer customer, CancellationToken cancellationToken)
    {
        if (customer.EmailConfirmed || customer.PasswordHash == null ||
            customer.VerificationSentUtc > DateTime.UtcNow.AddMinutes(-1)) return false;
        // Use a configured origin rather than trusting the incoming Host header.
        var origin = configuration["Accounts:PublicBaseUrl"] ?? "https://localhost:7096";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https")
            throw new InvalidOperationException("The account confirmation address must use HTTPS.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        customer.VerificationTokenHash = TokenHash(token);
        customer.VerificationExpiresUtc = DateTime.UtcNow.AddHours(24);
        customer.VerificationSentUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var link = new Uri(baseUri, $"/Account/ConfirmEmail?id={customer.CustomerID}&token={token}").AbsoluteUri;
        try
        {
            await sender.SendAsync(customer.Email, "Confirm your Céleste account",
                $"Hello {customer.CustomerName},\n\nConfirm your email address to sign in to Céleste:\n{link}\n\nThis link expires in 24 hours. If you did not register, you can ignore this email.",
                $"account-{Guid.NewGuid():N}@celeste.local", cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Keep credentials/tokens and recipient details out of logs; allow an immediate retry.
            customer.VerificationSentUtc = null;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Account verification email could not be sent ({ErrorType}).", ex.GetType().Name);
            return false;
        }
    }

    public async Task<bool> Confirm(int id, string token)
    {
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return false;
        var hash = TokenHash(token);
        var now = DateTime.UtcNow;
        return await db.Customers.Where(c => c.CustomerID == id && !c.EmailConfirmed &&
            c.VerificationTokenHash == hash && c.VerificationExpiresUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EmailConfirmed, true)
                .SetProperty(c => c.VerificationTokenHash, (string?)null)
                .SetProperty(c => c.VerificationExpiresUtc, (DateTime?)null)) == 1;
    }

    public async Task<Customer?> Authenticate(string email, string password)
    {
        var normalized = Normalize(email);
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.NormalizedEmail == normalized || c.Email.ToUpper() == normalized);
        if (customer?.PasswordHash == null)
        {
            // Perform the same password hashing work even for an unknown account.
            var dummy = new Customer();
            passwords.VerifyHashedPassword(dummy, passwords.HashPassword(dummy, "unused-random-account-value"), password);
            return null;
        }
        var result = passwords.VerifyHashedPassword(customer, customer.PasswordHash, password);
        if (customer.IsDemo && (environment?.IsDevelopment() != true || !configuration.GetValue<bool>("DemoData:Enabled"))) return null;
        if (customer.LockedUntilUtc > DateTime.UtcNow || customer.LockoutEnd > ReservationPolicy.Now) return null;
        if (result == PasswordVerificationResult.Failed)
        {
            customer.FailedLoginCount++;
            if (customer.FailedLoginCount >= 5)
            {
                customer.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
                customer.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync(); return null;
        }
        if (!customer.EmailConfirmed && customer.NormalizedEmail != null) return null;
        customer.FailedLoginCount = 0; customer.LockedUntilUtc = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            customer.PasswordHash = passwords.HashPassword(customer, password);
        await db.SaveChangesAsync(); return customer;
    }

    public async Task<bool> SendPasswordReset(string email, CancellationToken cancellationToken)
    {
        var normalized = Normalize(email);
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.NormalizedEmail == normalized || c.Email.ToUpper() == normalized);
        if (customer?.PasswordHash == null || customer.IsDemo || (!customer.EmailConfirmed && customer.NormalizedEmail != null) ||
            customer.PasswordResetSentUtc > DateTime.UtcNow.AddMinutes(-1)) return false;
        var origin = configuration["Accounts:PublicBaseUrl"] ?? "https://localhost:7096";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https")
            throw new InvalidOperationException("The password reset address must use HTTPS.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        customer.PasswordResetTokenHash = TokenHash(token);
        customer.PasswordResetExpiresUtc = DateTime.UtcNow.AddMinutes(30);
        customer.PasswordResetSentUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var link = new Uri(baseUri, $"/Account/ResetPassword?id={customer.CustomerID}&token={token}").AbsoluteUri;
        try
        {
            await sender.SendAsync(customer.Email, "Reset your Céleste password",
                $"Hello {customer.CustomerName},\n\nUse this link to choose a new password:\n{link}\n\nThis link expires in 30 minutes and can be used once. If you did not request a password reset, ignore this email; your password has not changed.",
                $"reset-{Guid.NewGuid():N}@celeste.local", cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            customer.PasswordResetSentUtc = null;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Password reset email could not be sent ({ErrorType}).", ex.GetType().Name);
            return false;
        }
    }

    public async Task<bool> CanResetPassword(int id, string? token)
    {
        if (token == null || token.Length != 64 || !token.All(Uri.IsHexDigit)) return false;
        var hash = TokenHash(token); var now = DateTime.UtcNow;
        return await db.Customers.AsNoTracking().AnyAsync(c => c.CustomerID == id && (c.EmailConfirmed || c.NormalizedEmail == null) &&
            c.PasswordHash != null && c.PasswordResetTokenHash == hash && c.PasswordResetExpiresUtc > now);
    }

    public async Task<bool> ResetPassword(int id, string? token, string password)
    {
        if (password.Length is < 10 or > 128 || !await CanResetPassword(id, token)) return false;
        var hash = TokenHash(token!); var now = DateTime.UtcNow;
        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.CustomerID == id);
        var passwordHash = passwords.HashPassword(customer, password);
        // The predicate and token consumption are atomic: racing requests cannot reuse a link.
        return await db.Customers.Where(c => c.CustomerID == id && (c.EmailConfirmed || c.NormalizedEmail == null) &&
            c.PasswordResetTokenHash == hash && c.PasswordResetExpiresUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.PasswordHash, passwordHash)
                .SetProperty(c => c.PasswordResetTokenHash, (string?)null)
                .SetProperty(c => c.PasswordResetExpiresUtc, (DateTime?)null)
                .SetProperty(c => c.PasswordResetSentUtc, (DateTime?)null)
                .SetProperty(c => c.FailedLoginCount, 0)
                .SetProperty(c => c.LockedUntilUtc, (DateTime?)null)
                .SetProperty(c => c.LockoutEnd, (DateTime?)null)
                .SetProperty(c => c.SessionVersion, c => c.SessionVersion + 1)) == 1;
    }
}
