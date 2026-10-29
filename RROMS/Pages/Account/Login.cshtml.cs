// Email and password sign-in for customers, staff and administrators, with a temporary lockout after repeated failures.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Account;

public class LoginModel(CustomerAccountService accounts) : PageModel
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

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? LocalRedirect(Destination(null)) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();
        var customer = await accounts.Authenticate(Email, Password);
        if (customer == null) return Fail();
        await CustomerAuth.SignInAsync(HttpContext, customer, RememberMe);
        return LocalRedirect(Destination(customer.Role));
    }

    private IActionResult Fail()
    {
        ModelState.AddModelError("", "Invalid email or password.");
        return Page();
    }

    private string Destination(string? role) =>
        Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Page(CustomerAuth.LandingPage(role ?? (User.IsInRole(Customer.AdminRole) ? Customer.AdminRole : User.IsInRole(Customer.StaffRole) ? Customer.StaffRole : Customer.CustomerRole)))!;
}
