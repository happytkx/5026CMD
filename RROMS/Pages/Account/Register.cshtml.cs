// Customer self-registration: creates a Customer account with a hashed password and requests email confirmation.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Account;

public class RegisterModel(CustomerAccountService accounts) : PageModel
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
        if (!ModelState.IsValid) return Page();
        try
        {
            var customer = await accounts.Register(FullName, Email, PhoneNo ?? "", Password);
            await accounts.SendVerification(customer, HttpContext.RequestAborted);
            return RedirectToPage("VerifyEmail", new { email = customer.Email });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return Page();
        }
    }
}
