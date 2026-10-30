// Email and password sign-in for customers, staff and administrators, with a temporary lockout after repeated failures.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Models;
using RROMS.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace RROMS.Pages.Account;

[EnableRateLimiting("staff-accounts")]
public class LoginModel(CustomerAccountService accounts, StaffAccountService staffAccounts) : PageModel
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
        var customer = await accounts.Authenticate(Email, Password);
        if (customer != null)
        {
            await CustomerAuth.SignInAsync(HttpContext, customer, RememberMe);
            return LocalRedirect(Destination(customer.Role));
        }

        var staff = await staffAccounts.Authenticate(Email, Password);
        if (staff == null) return Fail();
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, "staff:" + staff.StaffAccountID),
            new Claim("StaffAccountID", staff.StaffAccountID.ToString()),
            new Claim(ClaimTypes.Name, staff.StaffName),
            new Claim(ClaimTypes.Email, staff.Email),
            new Claim(ClaimTypes.Role, Customer.StaffRole),
            new Claim("SessionVersion", staff.SessionVersion.ToString())
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = RememberMe });
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
