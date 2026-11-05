// Customer self-registration: creates a Customer account with a hashed password and requests email confirmation.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Services;

namespace RROMS.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly CustomerAccountService _customerAccounts;

    public RegisterModel(CustomerAccountService customerAccounts)
    {
        _customerAccounts = customerAccounts;
    }

    [BindProperty, Required, StringLength(100)]
    public string FullName { get; set; } = "";

    [BindProperty, Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = "";

    [BindProperty, Phone, StringLength(20)]
    public string? PhoneNo { get; set; }

    [BindProperty, Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [BindProperty, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateRegistrationPassword();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        return await CreateAccountAsync();
    }

    private void ValidateRegistrationPassword()
    {
        foreach (string message in CustomerAuth.PasswordProblems(Password))
        {
            ModelState.AddModelError(nameof(Password), message);
        }
    }

    private async Task<IActionResult> CreateAccountAsync()
    {
        try
        {
            var newCustomer = await _customerAccounts.Register(
                FullName, Email, PhoneNo ?? "", Password);

            await _customerAccounts.SendVerification(
                newCustomer, HttpContext.RequestAborted);

            return RedirectToPage(
                "VerifyEmail",
                new { email = newCustomer.Email });
        }
        catch (InvalidOperationException error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
            return Page();
        }
    }
}

