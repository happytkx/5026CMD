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
public class StaffAccountsModel(ApplicationDbContext db, IPasswordHasher<StaffAccount> hasher,
    IPasswordHasher<Customer> customerHasher) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    public string ActiveTab => string.Equals(Tab, "customers", StringComparison.OrdinalIgnoreCase) ? "customers" : "staff";
    public List<StaffAccount> Staff { get; private set; } = [];
    public List<Customer> Customers { get; private set; } = [];
    public int ActiveCount => Staff.Count(s => s.IsActive);
    public int InactiveCount => Staff.Count - ActiveCount;
    public int ActiveCustomerCount => Customers.Count(c => c.IsActive);
    public int InactiveCustomerCount => Customers.Count - ActiveCustomerCount;

    public async Task OnGetAsync()
    {
        if (ActiveTab == "customers")
            Customers = await db.Customers.AsNoTracking()
                .OrderByDescending(c => c.IsActive).ThenBy(c => c.CustomerName).ToListAsync();
        else
            await LoadStaffAsync();
    }

    private async Task LoadStaffAsync()
    {
        Staff = await db.StaffAccounts.AsNoTracking()
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.StaffName).ToListAsync();
    }

    private int? AdministratorId =>
        int.TryParse(User.FindFirst("AdministratorID")?.Value, out var id) ? id : null;

    private IActionResult Fail(string message, string tab = "staff")
    {
        TempData["StaffAccountsError"] = message;
        return RedirectToPage(new { tab });
    }

    private IActionResult Done(string message, string tab = "staff")
    {
        TempData["StaffAccountsSuccess"] = message;
        return RedirectToPage(new { tab });
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

    private async Task<bool> CustomerEmailAlreadyUsed(string normalized, int? excludeCustomerId = null)
        => await db.Customers.AnyAsync(c =>
                (c.NormalizedEmail == normalized || c.Email.ToUpper() == normalized) &&
                (!excludeCustomerId.HasValue || c.CustomerID != excludeCustomerId.Value))
            || await db.Administrators.AnyAsync(a => a.NormalizedEmail == normalized)
            || await db.StaffAccounts.AnyAsync(a => a.NormalizedEmail == normalized);

    public async Task<IActionResult> OnPostCreateCustomerAsync(string? customerName, string? email,
        string? phoneNo, string? password)
    {
        if (!ValidPerson(customerName, email))
            return Fail("Enter a customer name and a valid email (maximum 100 characters).", "customers");
        if (phoneNo?.Trim().Length > 20)
            return Fail("Phone number must be 20 characters or fewer.", "customers");
        if (password is null || !ValidPassword(password))
            return Fail("Temporary password must contain 10–128 characters.", "customers");

        var cleanEmail = email!.Trim();
        var normalized = CustomerAccountService.Normalize(cleanEmail);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await CustomerEmailAlreadyUsed(normalized))
            return Fail("This email is already used by another account.", "customers");

        var customer = new Customer
        {
            CustomerName = customerName!.Trim(),
            Email = cleanEmail,
            NormalizedEmail = normalized,
            PhoneNo = phoneNo?.Trim() ?? string.Empty,
            Role = Customer.CustomerRole,
            CreatedAt = ReservationPolicy.Now,
            IsActive = true,
            EmailConfirmed = true // An administrator explicitly provisions this account.
        };
        customer.PasswordHash = customerHasher.HashPassword(customer, password);
        db.Customers.Add(customer);
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Fail("Customer account could not be created. Check the email and try again.", "customers");
        }
        return Done($"Customer account for {customer.CustomerName} created. Share the temporary password privately.", "customers");
    }

    public async Task<IActionResult> OnPostUpdateCustomerAsync(int customerId, string? customerName,
        string? email, string? phoneNo, string? address, string? newPassword)
    {
        if (!ValidPerson(customerName, email))
            return Fail("Enter a customer name and a valid email (maximum 100 characters).", "customers");
        if (phoneNo?.Trim().Length > 20 || address?.Trim().Length > 250)
            return Fail("Phone must be 20 characters or fewer and address 250 characters or fewer.", "customers");
        if (!string.IsNullOrEmpty(newPassword) && !ValidPassword(newPassword))
            return Fail("Replacement password must contain 10–128 characters.", "customers");

        var cleanEmail = email!.Trim();
        var normalized = CustomerAccountService.Normalize(cleanEmail);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return NotFound();
        if (await CustomerEmailAlreadyUsed(normalized, customerId))
            return Fail("This email is already used by another account.", "customers");

        var credentialsChanged = customer.NormalizedEmail != normalized ||
            !string.Equals(customer.Email, cleanEmail, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(newPassword);

        customer.CustomerName = customerName!.Trim();
        customer.Email = cleanEmail;
        customer.NormalizedEmail = normalized;
        customer.PhoneNo = phoneNo?.Trim() ?? string.Empty;
        customer.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

        if (!string.IsNullOrEmpty(newPassword))
        {
            customer.PasswordHash = customerHasher.HashPassword(customer, newPassword);
            customer.EmailConfirmed = true; // Admin-provisioned password allows signing in.
            customer.FailedLoginCount = 0;
            customer.LockedUntilUtc = null;
            customer.LockoutEnd = null;
        }

        if (credentialsChanged)
            customer.SessionVersion++;

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Fail("Customer changes could not be saved. Refresh and try again.", "customers");
        }
        return Done($"Customer account for {customer.CustomerName} updated.", "customers");
    }

    public async Task<IActionResult> OnPostSetCustomerActiveAsync(int customerId, bool isActive)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return NotFound();

        if (customer.IsActive == isActive)
            return Done($"Customer account for {customer.CustomerName} is already {(isActive ? "active" : "deactivated")}.", "customers");

        customer.IsActive = isActive;
        customer.SessionVersion++; // Existing customer sign-in cookies are revoked.
        if (isActive)
        {
            customer.FailedLoginCount = 0;
            customer.LockedUntilUtc = null;
            customer.LockoutEnd = null;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Done($"Customer account for {customer.CustomerName} {(isActive ? "reactivated" : "deactivated")}.", "customers");
    }

}
