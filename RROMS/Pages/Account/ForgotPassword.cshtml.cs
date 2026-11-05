using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RROMS.Services;
namespace RROMS.Pages.Account;

[EnableRateLimiting("accounts")]
public class ForgotPasswordModel(CustomerAccountService accounts, AdministratorAccountService administrators) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    public bool Requested { get; set; }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        await accounts.SendPasswordReset(Email, HttpContext.RequestAborted);
        await administrators.SendPasswordReset(Email, HttpContext.RequestAborted);
        Requested = true;
        return Page();
    }
}
