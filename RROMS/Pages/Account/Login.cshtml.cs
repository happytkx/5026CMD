
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Account;

// Customer login only.
// Staff and administrators use StaffLogin.cshtml.
[EnableRateLimiting("accounts")]
public class LoginModel : PageModel
{
    private readonly CustomerAccountService _accounts;

    public LoginModel(CustomerAccountService accounts)
    {
        _accounts = accounts;
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

        var customer = await _accounts.Authenticate(Email, Password);

        if (customer == null)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return Page();
        }

        // Clear the previous session basket.
        Basket.Clear(HttpContext.Session);

        await CustomerAuth.SignInAsync(
            HttpContext, customer, RememberMe);

        if (Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl!);
        }

        return LocalRedirect(
            Url.Page(CustomerAuth.LandingPage(Customer.CustomerRole))!);
    }
}
