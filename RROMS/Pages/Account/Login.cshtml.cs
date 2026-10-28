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

public class LoginModel(ApplicationDbContext db, IPasswordHasher<Customer> hasher) : PageModel
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
        var email = Email.Trim();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email);
        var now = ReservationPolicy.Now;
        if (customer?.PasswordHash == null)
        {
            // Hash anyway so an unknown email costs the same as a wrong password.
            hasher.HashPassword(new Customer(), Password);
            return Fail();
        }
        if (customer.LockoutEnd > now)
        {
            ModelState.AddModelError("", $"Too many failed attempts. Please try again in {Math.Ceiling((customer.LockoutEnd.Value - now).TotalMinutes)} minute(s).");
            return Page();
        }
        var result = hasher.VerifyHashedPassword(customer, customer.PasswordHash, Password);
        if (result == PasswordVerificationResult.Failed)
        {
            customer.FailedLoginCount++;
            if (customer.FailedLoginCount >= CustomerAuth.MaxFailedAttempts)
            {
                customer.LockoutEnd = now.Add(CustomerAuth.LockoutDuration);
                customer.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync();
            return Fail();
        }
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            customer.PasswordHash = hasher.HashPassword(customer, Password);
        customer.FailedLoginCount = 0;
        customer.LockoutEnd = null;
        await db.SaveChangesAsync();
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
