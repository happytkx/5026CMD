// Customer-only sign-in. Staff and administrators use the separate internal login page.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Models;
using RROMS.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace RROMS.Pages.Account;

[EnableRateLimiting("accounts")]
public class LoginModel(CustomerAccountService accounts) : PageModel
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

        var customer = await accounts.Authenticate(Email, Password);
        if (customer is null)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return Page();
        }

        Basket.Clear(HttpContext.Session);
        await CustomerAuth.SignInAsync(HttpContext, customer, RememberMe);
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl!
            : Url.Page(CustomerAuth.LandingPage(Customer.CustomerRole))!);
    }
}
