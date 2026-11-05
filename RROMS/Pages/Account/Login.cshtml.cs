// Email and password sign-in for customers, staff and administrators, with a temporary lockout after repeated failures.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Models;
using RROMS.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace RROMS.Pages.Account;

[EnableRateLimiting("staff-accounts")]
public class LoginModel(CustomerAccountService accounts, StaffAccountService staffAccounts, AdministratorAccountService administrators) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = "";
    [BindProperty, Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [BindProperty]
    public bool RememberMe
    {
        get; set;
    }
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl
    {
        get; set;
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();
        var administrator = await administrators.Authenticate(Email, Password);
        if (administrator != null)
        {
            Basket.Clear(HttpContext.Session);
            await InternalAccountAuth.SignInAsync(HttpContext, administrator, RememberMe);
            return LocalRedirect(Destination(Customer.AdminRole));
        }
        var customer = await accounts.Authenticate(Email, Password);
        if (customer != null)
        {
            Basket.Clear(HttpContext.Session);
            await CustomerAuth.SignInAsync(HttpContext, customer, RememberMe);
            return LocalRedirect(Destination(customer.Role));
        }

        var staff = await staffAccounts.Authenticate(Email, Password);
        if (staff == null) return Fail();
        Basket.Clear(HttpContext.Session);
        await InternalAccountAuth.SignInAsync(HttpContext, staff, RememberMe);
        return LocalRedirect(Destination(Customer.StaffRole));
    }

    private IActionResult Fail()
    {
        ModelState.AddModelError("", "Invalid email or password.");
        return Page();
    }

    private string Destination(string role) =>
        Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Page(CustomerAuth.LandingPage(role))!;
}
