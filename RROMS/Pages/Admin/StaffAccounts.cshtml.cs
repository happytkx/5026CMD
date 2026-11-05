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

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }
    [BindProperty(Name = "p", SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public const int PageSize = 25;
    public string ActiveTab => string.Equals(Tab, "customers", StringComparison.OrdinalIgnoreCase) ? "customers" : "staff";
    public List<StaffAccount> Staff { get; private set; } = [];
    public List<Customer> Customers { get; private set; } = [];
    public int ActiveCount { get; private set; }
    public int InactiveCount { get; private set; }
    public int ActiveCustomerCount { get; private set; }
    public int InactiveCustomerCount { get; private set; }
    public int FilteredCount { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(FilteredCount / (double)PageSize));

    public async Task OnGetAsync()
    {
        Search = Search?.Trim();
        var search = Search;
        if (ActiveTab == "customers")
        {
            ActiveCustomerCount = await db.Customers.CountAsync(c => c.IsActive);
            InactiveCustomerCount = await db.Customers.CountAsync(c => !c.IsActive);
            var query = db.Customers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => c.CustomerName.Contains(search) || c.Email.Contains(search));
            FilteredCount = await query.CountAsync();
            PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
            Customers = await query.OrderByDescending(c => c.IsActive).ThenBy(c => c.CustomerName).ThenBy(c => c.CustomerID)
                .Skip((PageNumber - 1) * PageSize).Take(PageSize).ToListAsync();
        }
        else
        {
            ActiveCount = await db.StaffAccounts.CountAsync(s => s.IsActive);
            InactiveCount = await db.StaffAccounts.CountAsync(s => !s.IsActive);
            var query = db.StaffAccounts.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => s.StaffName.Contains(search) || s.Email.Contains(search));
            FilteredCount = await query.CountAsync();
            PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
            Staff = await query.OrderByDescending(s => s.IsActive).ThenBy(s => s.StaffName).ThenBy(s => s.StaffAccountID)
                .Skip((PageNumber - 1) * PageSize).Take(PageSize).ToListAsync();
        }
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

    public async Task<IActionResult> OnPostBulkDeactivateStaffAsync(List<int>? selectedIds, string? search, int p = 1)
    {
        var ids = (selectedIds ?? []).Distinct().ToList();
        if (ids.Count is 0 or > 100)
            return Fail("Select between 1 and 100 staff accounts to deactivate.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var staff = await db.StaffAccounts.Where(s => ids.Contains(s.StaffAccountID) && s.IsActive).ToListAsync();
        foreach (var account in staff)
        {
            account.IsActive = false;
            account.SessionVersion++;
            account.UpdatedByAdministratorID = AdministratorId;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["StaffAccountsSuccess"] = $"{staff.Count} staff account(s) deactivated.";
        return RedirectToPage(new { tab = "staff", search, p });
    }

    public async Task<IActionResult> OnPostBulkDeactivateCustomersAsync(List<int>? selectedIds, string? search, int p = 1)
    {
        var ids = (selectedIds ?? []).Distinct().ToList();
        if (ids.Count is 0 or > 100)
            return Fail("Select between 1 and 100 customer accounts to deactivate.", "customers");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var customers = await db.Customers.Where(c => ids.Contains(c.CustomerID) && c.IsActive).ToListAsync();
        foreach (var account in customers)
        {
            account.IsActive = false;
            account.SessionVersion++;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["StaffAccountsSuccess"] = $"{customers.Count} customer account(s) deactivated.";
        return RedirectToPage(new { tab = "customers", search, p });
    }
}
