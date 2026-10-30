using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Services;
namespace RROMS.Pages.Account;

[EnableRateLimiting("accounts")]
public class VerifyEmailModel(ApplicationDbContext db, CustomerAccountService accounts) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    public string? Notice { get; set; }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var normalized = CustomerAccountService.Normalize(Email);
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.NormalizedEmail == normalized);
        if (customer != null) await accounts.SendVerification(customer, HttpContext.RequestAborted);
        Notice = "If this address belongs to an account awaiting verification, a new link has been requested. Please check your inbox and spam folder. Allow a minute between requests; contact the restaurant if no email arrives.";
        return Page();
    }
}
