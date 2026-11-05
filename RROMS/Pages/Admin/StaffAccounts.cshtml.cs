using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class StaffAccountsModel(ApplicationDbContext db, IPasswordHasher<StaffAccount> hasher) : PageModel
{
    public List<StaffAccount> Staff { get; private set; } = [];
    public int ActiveCount => Staff.Count(s => s.IsActive);
    public int InactiveCount => Staff.Count - ActiveCount;

    public async Task OnGetAsync() => await LoadAsync();

    private Task LoadAsync() => LoadStaffAsync();

    private async Task LoadStaffAsync()
    {
        Staff = await db.StaffAccounts.AsNoTracking()
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.StaffName).ToListAsync();
    }

    private int? AdministratorId =>
        int.TryParse(User.FindFirst("AdministratorID")?.Value, out var id) ? id : null;

    private IActionResult Fail(string message)
    {
        TempData["StaffAccountsError"] = message;
        return RedirectToPage();
    }

    private IActionResult Done(string message)
    {
        TempData["StaffAccountsSuccess"] = message;
        return RedirectToPage();
    }

    private static bool ValidPerson(string? name, string? email)
        => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100 &&
           !string.IsNullOrWhiteSpace(email) && email.Trim().Length <= 100 &&
           new EmailAddressAttribute().IsValid(email.Trim());

    private static bool ValidPassword(string password) => password.Length is >= 10 and <= 128;

    private async Task<bool> EmailAlreadyUsed(string normalized, int? excludeStaffId = null)
    {
        return await db.StaffAccounts.AnyAsync(s =>
                   s.NormalizedEmail == normalized && (!excludeStaffId.HasValue || s.StaffAccountID != excludeStaffId.Value))
               || await db.Administrators.AnyAsync(a => a.NormalizedEmail == normalized)
               || await db.Customers.AnyAsync(c => c.NormalizedEmail == normalized || c.Email.ToUpper() == normalized);
    }

    public async Task<IActionResult> OnPostCreateAsync(string? staffName, string? email, string? password)
    {
        if (!ValidPerson(staffName, email))
            return Fail("Enter a staff name and a valid email address (maximum 100 characters each).");
        if (password is null || !ValidPassword(password))
            return Fail("A temporary password must contain 10–128 characters.");

        var cleanEmail = email!.Trim();
        var normalized = CustomerAccountService.Normalize(cleanEmail);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await EmailAlreadyUsed(normalized))
            return Fail("This email already belongs to a customer, administrator or staff member.");

        var staff = new StaffAccount
        {
            StaffName = staffName!.Trim(),
            Email = cleanEmail,
            NormalizedEmail = normalized,
            IsActive = true,
            CreatedByAdministratorID = AdministratorId,
            UpdatedByAdministratorID = AdministratorId
        };
        staff.PasswordHash = hasher.HashPassword(staff, password);
        db.StaffAccounts.Add(staff);
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Fail("The staff account could not be created. Check for a duplicate email and try again.");
        }

        return Done($"Staff account for {staff.StaffName} created. Share the temporary password privately.");
    }

    public async Task<IActionResult> OnPostUpdateAsync(int staffAccountId, string? staffName,
        string? email, string? newPassword)
    {
        if (!ValidPerson(staffName, email))
            return Fail("Enter a staff name and a valid email address (maximum 100 characters each).");
        if (!string.IsNullOrEmpty(newPassword) && !ValidPassword(newPassword))
            return Fail("A replacement password must contain 10–128 characters. Leave it blank to keep the current password.");

        var cleanEmail = email!.Trim();
        var normalized = CustomerAccountService.Normalize(cleanEmail);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var staff = await db.StaffAccounts.FindAsync(staffAccountId);
        if (staff is null) return NotFound();
        if (await EmailAlreadyUsed(normalized, staffAccountId))
            return Fail("This email is already used by another account.");

        var credentialsChanged = staff.NormalizedEmail != normalized || !string.IsNullOrEmpty(newPassword);
        staff.StaffName = staffName!.Trim();
        staff.Email = cleanEmail;
        staff.NormalizedEmail = normalized;
        staff.UpdatedByAdministratorID = AdministratorId;
        if (!string.IsNullOrEmpty(newPassword))
        {
            staff.PasswordHash = hasher.HashPassword(staff, newPassword);
            staff.FailedLoginCount = 0;
            staff.LockedUntilUtc = null;
        }
        if (credentialsChanged)
            staff.SessionVersion++; // Immediately invalidate old sessions on password/email changes.

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Fail("Changes could not be saved. Refresh and try again.");
        }
        return Done($"Staff account for {staff.StaffName} updated.");
    }

    public async Task<IActionResult> OnPostSetActiveAsync(int staffAccountId, bool isActive)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var staff = await db.StaffAccounts.FindAsync(staffAccountId);
        if (staff is null) return NotFound();

        if (staff.IsActive == isActive)
            return Done($"Staff account for {staff.StaffName} is already {(isActive ? "active" : "deactivated")}.");

        staff.IsActive = isActive;
        staff.SessionVersion++; // Revokes any signed-in session; cookie validation checks this.
        staff.UpdatedByAdministratorID = AdministratorId;
        if (isActive)
        {
            staff.FailedLoginCount = 0;
            staff.LockedUntilUtc = null;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Done($"Staff account for {staff.StaffName} {(isActive ? "reactivated" : "deactivated")}.");
    }
}
