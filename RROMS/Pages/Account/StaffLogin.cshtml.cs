using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RROMS.Services;
namespace RROMS.Pages.Account;

[EnableRateLimiting("staff-accounts")]
public class StaffLoginModel(StaffAccountService accounts) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [BindProperty, Required, StringLength(128), DataType(DataType.Password)] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var account = await accounts.Authenticate(Email, Password);
        if (account == null) { ModelState.AddModelError("", "Unable to sign in. Check your staff credentials or try again later."); return Page(); }
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, "staff:" + account.StaffAccountID),
            new Claim("StaffAccountID", account.StaffAccountID.ToString()), new Claim(ClaimTypes.Name, account.StaffName),
            new Claim(ClaimTypes.Email, account.Email), new Claim(ClaimTypes.Role, "Restaurant Staff"),
            new Claim("SessionVersion", account.SessionVersion.ToString()) }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl!) : RedirectToPage("/Staff/Index");
    }
}
