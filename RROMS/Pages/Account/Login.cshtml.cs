// Email and password sign-in for customers, staff and administrators, with a temporary lockout after repeated failures.

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Account;

[EnableRateLimiting("staff-accounts")]
public class LoginModel : PageModel
{
    private readonly CustomerAccountService _customerAccounts;
    private readonly StaffAccountService _staffAccounts;
    private readonly AdministratorAccountService _adminAccounts;

    public LoginModel(
        CustomerAccountService customerAccounts,
        StaffAccountService staffAccounts,
        AdministratorAccountService adminAccounts)
    {
        _customerAccounts = customerAccounts;
        _staffAccounts = staffAccounts;
        _adminAccounts = adminAccounts;
    }

    [BindProperty, Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = "";

    [BindProperty, Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [BindProperty]
    public bool RememberMe { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Check whether the account belongs to an administrator.
        var admin = await _adminAccounts.Authenticate(Email, Password);

        if (admin != null)
        {
            Basket.Clear(HttpContext.Session);

            await InternalAccountAuth.SignInAsync(
                HttpContext, admin, RememberMe);

            return LocalRedirect(GetDestination(Customer.AdminRole));
        }

        // Check whether the account belongs to a customer.
        var customer = await _customerAccounts.Authenticate(
            Email, Password);

        if (customer != null)
        {
            Basket.Clear(HttpContext.Session);

            await CustomerAuth.SignInAsync(
                HttpContext, customer, RememberMe);

            return LocalRedirect(GetDestination(customer.Role));
        }

        // Check whether the account belongs to restaurant staff.
        var staff = await _staffAccounts.Authenticate(
            Email, Password);

        if (staff != null)
        {
            Basket.Clear(HttpContext.Session);

            await InternalAccountAuth.SignInAsync(
                HttpContext, staff, RememberMe);

            return LocalRedirect(GetDestination(Customer.StaffRole));
        }

        ModelState.AddModelError(
            "", "Invalid email or password.");

        return Page();
    }

    private string GetDestination(string role)
    {
        if (Url.IsLocalUrl(ReturnUrl))
        {
            return ReturnUrl!;
        }

        return Url.Page(CustomerAuth.LandingPage(role))!;
    }
}
