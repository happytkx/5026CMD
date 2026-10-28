// Customer self-registration: creates a Customer account with a hashed password and signs the new customer in.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Account;

public class RegisterModel(ApplicationDbContext db, IPasswordHasher<Customer> hasher) : PageModel
{
    [BindProperty, Required, StringLength(100)]
    public string FullName { get; set; } = "";
    [BindProperty, Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = "";
    [BindProperty, Phone, StringLength(20)]
    public string? PhoneNo
    {
        get; set;
    }
    [BindProperty, Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [BindProperty, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl
    {
        get; set;
    }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        foreach (var problem in CustomerAuth.PasswordProblems(Password))
            ModelState.AddModelError(nameof(Password), problem);
        var email = Email?.Trim() ?? "";
        if (ModelState.IsValid && await db.Customers.AnyAsync(c => c.Email == email))
            ModelState.AddModelError(nameof(Email), "An account with this email already exists.");
        if (!ModelState.IsValid)
            return Page();
        var customer = new Customer
        {
            CustomerName = FullName.Trim(),
            Email = email,
            PhoneNo = PhoneNo?.Trim() ?? "",
            Role = Customer.CustomerRole,
            CreatedAt = ReservationPolicy.Now
        };
        customer.PasswordHash = hasher.HashPassword(customer, Password);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        await CustomerAuth.SignInAsync(HttpContext, customer);
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl!) : RedirectToPage("/Reservations/Index");
    }
}
