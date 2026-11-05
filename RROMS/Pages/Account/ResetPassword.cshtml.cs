using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RROMS.Services;
namespace RROMS.Pages.Account;

[EnableRateLimiting("accounts")]
public class ResetPasswordModel(CustomerAccountService accounts, AdministratorAccountService administrators) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? AccountType { get; set; } = "Customer";
    private Task<bool> CanReset() => AccountType == "Administrator" ? administrators.CanResetPassword(Id, Token) : AccountType is null or "Customer" ? accounts.CanResetPassword(Id, Token) : Task.FromResult(false);
    private Task<bool> Reset() => AccountType == "Administrator" ? administrators.ResetPassword(Id, Token, Password) : accounts.ResetPassword(Id, Token, Password);
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Token { get; set; }
    [BindProperty, Required, StringLength(128, MinimumLength = 10), DataType(DataType.Password), Display(Name = "New password")]
    public string Password { get; set; } = "";
    [BindProperty, Required, Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = "";
    public bool LinkValid { get; set; }
    private void ProtectResponse()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
    public async Task OnGetAsync() { ProtectResponse(); LinkValid = await CanReset(); }
    public async Task<IActionResult> OnPostAsync()
    {
        ProtectResponse(); LinkValid = await CanReset();
        if (!LinkValid || !ModelState.IsValid) return Page();
        if (!await Reset()) { LinkValid = false; return Page(); }
        TempData["AccountMessage"] = "Your password has been updated. Please sign in with your new password. Previous signed-in sessions have been ended.";
        return RedirectToPage(AccountType == "Administrator" ? "StaffLogin" : "Login");
    }
}
