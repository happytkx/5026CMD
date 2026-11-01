using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
namespace RROMS.Services;
public class AdministratorAccountService(ApplicationDbContext db, IPasswordHasher<Administrator> passwords,
    IWebHostEnvironment environment, IConfiguration config, IEmailSender sender, ILogger<AdministratorAccountService> logger)
{
    private static string Normalize(string email) => CustomerAccountService.Normalize(email);
    private static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public async Task<Administrator?> Authenticate(string email, string password)
    {
        var normalized = CustomerAccountService.Normalize(email);
        var account = await db.Administrators.SingleOrDefaultAsync(s => s.NormalizedEmail == normalized);
        if (account == null) return null;
        var valid = passwords.VerifyHashedPassword(account, account.PasswordHash, password);
        if (!account.IsActive || (account.IsDemo && (!environment.IsDevelopment() || !config.GetValue<bool>("DemoData:Enabled"))) ||
            account.LockedUntilUtc > DateTime.UtcNow) return null;
        if (valid == PasswordVerificationResult.Failed)
        {
            account.FailedLoginCount++;
            if (account.FailedLoginCount >= 5) { account.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15); account.FailedLoginCount = 0; }
            await db.SaveChangesAsync(); return null;
        }
        account.FailedLoginCount = 0; account.LockedUntilUtc = null;
        if (valid == PasswordVerificationResult.SuccessRehashNeeded) account.PasswordHash = passwords.HashPassword(account, password);
        await db.SaveChangesAsync(); return account;
    }
    public async Task<bool> SendPasswordReset(string email, CancellationToken cancellationToken)
    {
        var normalized = Normalize(email);
        var administrator = await db.Administrators.SingleOrDefaultAsync(c => c.NormalizedEmail == normalized || c.Email.ToUpper() == normalized);
        if (administrator?.PasswordHash == null || administrator.IsDemo || !administrator.IsActive ||
            administrator.PasswordResetSentUtc > DateTime.UtcNow.AddMinutes(-1)) return false;
        var origin = config["Accounts:PublicBaseUrl"] ?? "https://localhost:7096";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https")
            throw new InvalidOperationException("The password reset address must use HTTPS.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        administrator.PasswordResetTokenHash = TokenHash(token);
        administrator.PasswordResetExpiresUtc = DateTime.UtcNow.AddMinutes(30);
        administrator.PasswordResetSentUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var link = new Uri(baseUri, $"/Account/ResetPassword?AccountType=Administrator&id={administrator.AdministratorID}&token={token}").AbsoluteUri;
        try
        {
            await sender.SendAsync(administrator.Email, "Reset your Céleste password",
                $"Hello {administrator.Name},\n\nUse this link to choose a new password:\n{link}\n\nThis link expires in 30 minutes and can be used once. If you did not request a password reset, ignore this email; your password has not changed.",
                $"reset-{Guid.NewGuid():N}@celeste.local", cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            administrator.PasswordResetSentUtc = null;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Password reset email could not be sent ({ErrorType}).", ex.GetType().Name);
            return false;
        }
    }

    public async Task<bool> CanResetPassword(int id, string? token)
    {
        if (token == null || token.Length != 64 || !token.All(Uri.IsHexDigit)) return false;
        var hash = TokenHash(token); var now = DateTime.UtcNow;
        return await db.Administrators.AsNoTracking().AnyAsync(c => c.AdministratorID == id && c.IsActive &&
            c.PasswordHash != null && c.PasswordResetTokenHash == hash && c.PasswordResetExpiresUtc > now);
    }

    public async Task<bool> ResetPassword(int id, string? token, string password)
    {
        if (password.Length is < 10 or > 128 || !await CanResetPassword(id, token)) return false;
        var hash = TokenHash(token!); var now = DateTime.UtcNow;
        var administrator = await db.Administrators.AsNoTracking().SingleAsync(c => c.AdministratorID == id);
        var passwordHash = passwords.HashPassword(administrator, password);
        // The predicate and token consumption are atomic: racing requests cannot reuse a link.
        return await db.Administrators.Where(c => c.AdministratorID == id && c.IsActive &&
            c.PasswordResetTokenHash == hash && c.PasswordResetExpiresUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.PasswordHash, passwordHash)
                .SetProperty(c => c.PasswordResetTokenHash, (string?)null)
                .SetProperty(c => c.PasswordResetExpiresUtc, (DateTime?)null)
                .SetProperty(c => c.PasswordResetSentUtc, (DateTime?)null)
                .SetProperty(c => c.FailedLoginCount, 0)
                .SetProperty(c => c.LockedUntilUtc, (DateTime?)null)

                .SetProperty(c => c.SessionVersion, c => c.SessionVersion + 1)) == 1;
    }
}
