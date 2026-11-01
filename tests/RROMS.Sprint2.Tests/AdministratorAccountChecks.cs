using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

internal static class AdministratorAccountChecks
{
    public static async Task Run(DbContextOptions<ApplicationDbContext> options, Action<bool, string> check)
    {
        await using var db = new ApplicationDbContext(options);
        var passwords = new PasswordHasher<Administrator>();
        var admin = new Administrator { Name = "Auth check", Email = "auth-admin@example.test", NormalizedEmail = "AUTH-ADMIN@EXAMPLE.TEST" };
        admin.PasswordHash = passwords.HashPassword(admin, "Original-password-2026!");
        db.Administrators.Add(admin); await db.SaveChangesAsync();
        var sender = new FakeEmailSender();
        var config = new ConfigurationBuilder().Build();
        var service = new AdministratorAccountService(db, passwords, new TestEnvironment(), config, sender, NullLogger<AdministratorAccountService>.Instance);
        check(await service.Authenticate(admin.Email, "Original-password-2026!") != null, "Independent administrator authenticates with hashed password");
        for (var i = 0; i < 5; i++) await service.Authenticate(admin.Email, "incorrect");
        check(admin.LockedUntilUtc > DateTime.UtcNow && await service.Authenticate(admin.Email, "Original-password-2026!") == null, "Administrator lockout blocks correct password after repeated failures");
        admin.LockedUntilUtc = null; await db.SaveChangesAsync();
        check(await service.SendPasswordReset(admin.Email, CancellationToken.None), "Administrator recovery sends reset link");
        var body = sender.Bodies.Last();
        var token = System.Text.RegularExpressions.Regex.Match(body, "token=([A-F0-9]+)").Groups[1].Value;
        check(body.Contains("AccountType=Administrator") && await service.CanResetPassword(admin.AdministratorID, token), "Administrator reset link has separate account scope");
        check(!await service.CanResetPassword(admin.AdministratorID, "bad-token"), "Administrator reset rejects malformed tokens");
        check(await service.ResetPassword(admin.AdministratorID, token, "New-password-2026!"), "Administrator can reset password");
        check(!await service.ResetPassword(admin.AdministratorID, token, "Other-password-2026!"), "Administrator reset token is single-use");
        db.ChangeTracker.Clear();
        check(await service.Authenticate(admin.Email, "Original-password-2026!") == null && await service.Authenticate(admin.Email, "New-password-2026!") != null, "Administrator reset replaces previous password");
        var updated = await db.Administrators.SingleAsync(a => a.AdministratorID == admin.AdministratorID);
        check(updated.SessionVersion == 1, "Administrator reset invalidates previous sessions");
        updated.IsActive = false; await db.SaveChangesAsync();
        check(await service.Authenticate(admin.Email, "New-password-2026!") == null, "Disabled administrator cannot sign in");
        var customers = new CustomerAccountService(db, new PasswordHasher<Customer>(), sender, config, NullLogger<CustomerAccountService>.Instance);
        try { await customers.Register("Impersonator", admin.Email, "0123456789", "New-password-2026!"); check(false, "Internal email rejected by customer signup"); }
        catch (InvalidOperationException) { check(true, "Internal email rejected by customer signup"); }
        db.Customers.Add(new Customer { CustomerName = "Invalid internal identity", Email = "invalid-role@example.test", Role = Customer.AdminRole });
        try { await db.SaveChangesAsync(); check(false, "Customer table rejects administrator role"); }
        catch (DbUpdateException) { check(true, "Customer table rejects administrator role"); }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "RROMS";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
