using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace RROMS.Pages.Account;

// Keep existing staff bookmarks working through the shared sign-in page.
public class StaffLoginModel : PageModel
{
    public IActionResult OnGet(string? returnUrl) => RedirectToPage("Login", new { ReturnUrl = returnUrl });
    public IActionResult OnPost(string? returnUrl) => RedirectToPage("Login", new { ReturnUrl = returnUrl });
}
