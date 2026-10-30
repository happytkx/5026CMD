using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RROMS.Services;
namespace RROMS.Pages.Account;
[EnableRateLimiting("accounts")]
public class ConfirmEmailModel(CustomerAccountService accounts) : PageModel
{
    public bool Confirmed { get; set; }
    public async Task OnGetAsync(int id, string? token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Confirmed = token != null && await accounts.Confirm(id, token);
    }
}
