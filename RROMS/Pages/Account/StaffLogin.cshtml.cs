// Role-restricted internal sign-in for restaurant staff and administrators.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Account;

[EnableRateLimiting("staff-accounts")]
public class StaffLoginModel(StaffAccountService staffAccounts, AdministratorAccountService administrators) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = "";
    [BindProperty, Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [BindProperty]
    public bool RememberMe { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var administrator = await administrators.Authenticate(Email, Password);
        if (administrator is not null)
        {
            Basket.Clear(HttpContext.Session);
            await InternalAccountAuth.SignInAsync(HttpContext, administrator, RememberMe);
            return LocalRedirect(Destination(Customer.AdminRole));
        }

        var staff = await staffAccounts.Authenticate(Email, Password);
        if (staff is null)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return Page();
        }

        Basket.Clear(HttpContext.Session);
        await InternalAccountAuth.SignInAsync(HttpContext, staff, RememberMe);
        return LocalRedirect(Destination(Customer.StaffRole));
    }

    private string Destination(string role) =>
        Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Page(CustomerAuth.LandingPage(role))!;
}
