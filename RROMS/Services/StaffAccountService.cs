using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
namespace RROMS.Services;
public class StaffAccountService(ApplicationDbContext db, IPasswordHasher<StaffAccount> passwords,
    IWebHostEnvironment environment, IConfiguration config)
{
    public async Task<StaffAccount?> Authenticate(string email, string password)
    {
        var normalized = CustomerAccountService.Normalize(email);
        var account = await db.StaffAccounts.SingleOrDefaultAsync(s => s.NormalizedEmail == normalized);
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
}
