// Lets a signed-in user view and update their name, phone number and address. The sign-in email is read-only.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Account;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ProfileModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public bool Edit { get; set; }

    public string Email { get; set; } = "";

    public bool IsCustomer =>
        User.IsInRole(Customer.CustomerRole);

    public bool IsAdministrator =>
        User.IsInRole(Customer.AdminRole);

    public class ProfileInput
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = "";

        [Phone]
        [StringLength(20)]
        public string? PhoneNo { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsCustomer)
        {
            var customer = await GetCustomerAsync();

            if (customer == null)
            {
                return Challenge();
            }

            Email = customer.Email;
            Input.FullName = customer.CustomerName;
            Input.PhoneNo = customer.PhoneNo;
            Input.Address = customer.Address;

            return Page();
        }

        return await LoadInternalProfileAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Edit)
        {
            return RedirectToPage();
        }

        if (IsCustomer)
        {
            return await UpdateCustomerProfileAsync();
        }

        return await UpdateInternalProfileAsync();
    }

    private async Task<Customer?> GetCustomerAsync()
    {
        var customerId = ReservationIdentity.CustomerId(User);

        return await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerID == customerId);
    }

    private int GetAccountId(string claimName)
    {
        string? value = User.FindFirst(claimName)?.Value;

        if (int.TryParse(value, out int accountId))
        {
            return accountId;
        }

        return -1;
    }

    private async Task<IActionResult> UpdateCustomerProfileAsync()
    {
        var customer = await GetCustomerAsync();

        if (customer == null)
        {
            return Challenge();
        }

        Email = customer.Email;

        if (!ModelState.IsValid)
        {
            Edit = true;
            return Page();
        }

        customer.CustomerName = Input.FullName.Trim();
        customer.PhoneNo = Input.PhoneNo?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(Input.Address))
        {
            customer.Address = null;
        }
        else
        {
            customer.Address = Input.Address.Trim();
        }

        await _context.SaveChangesAsync();

        await CustomerAuth.SignInAsync(HttpContext, customer);

        TempData["Success"] = "Your profile has been updated.";

        return RedirectToPage();
    }

    private async Task<IActionResult> LoadInternalProfileAsync()
    {
        if (IsAdministrator)
        {
            var admin = await _context.Administrators.FindAsync(
                GetAccountId("AdministratorID"));

            if (admin == null)
            {
                return Challenge();
            }

            Email = admin.Email;
            Input.FullName = admin.Name;

            return Page();
        }

        if (User.IsInRole(Customer.StaffRole))
        {
            var staff = await _context.StaffAccounts.FindAsync(
                GetAccountId("StaffAccountID"));

            if (staff == null)
            {
                return Challenge();
            }

            Email = staff.Email;
            Input.FullName = staff.StaffName;

            return Page();
        }

        return Challenge();
    }

    private async Task<IActionResult> UpdateInternalProfileAsync()
    {
        if (IsAdministrator)
        {
            var admin = await _context.Administrators.FindAsync(
                GetAccountId("AdministratorID"));

            if (admin == null)
            {
                return Challenge();
            }

            Email = admin.Email;

            if (!ModelState.IsValid)
            {
                Edit = true;
                return Page();
            }

            admin.Name = Input.FullName.Trim();

            await _context.SaveChangesAsync();
            await InternalAccountAuth.SignInAsync(HttpContext, admin);
        }
        else if (User.IsInRole(Customer.StaffRole))
        {
            var staff = await _context.StaffAccounts.FindAsync(
                GetAccountId("StaffAccountID"));

            if (staff == null)
            {
                return Challenge();
            }

            Email = staff.Email;

            if (!ModelState.IsValid)
            {
                Edit = true;
                return Page();
            }

            staff.StaffName = Input.FullName.Trim();

            await _context.SaveChangesAsync();
            await InternalAccountAuth.SignInAsync(HttpContext, staff);
        }
        else
        {
            return Challenge();
        }

        TempData["Success"] = "Your profile has been updated.";

        return RedirectToPage();
    }
}

