// Optional development-only role preview (disabled by default) and POST sign-out. Customer login is under /Account/Login.
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Pages;

public class AccessModel(ApplicationDbContext db, IWebHostEnvironment env, IConfiguration config) : PageModel
{
    public bool DemoEnabled => env.IsDevelopment() && config.GetValue<bool>("Sprint2:EnableDemoAccess");
    public List<Customer> Customers { get; set; } = [];
    [BindProperty]
    public int CustomerId
    {
        get; set;
    }
    public async Task<IActionResult> OnGetAsync()
    {
        if (!DemoEnabled) return RedirectToPage("/Account/Login");
        if (DemoEnabled)
            Customers = await db.Customers.AsNoTracking().ToListAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(string role)
    {
        if (!DemoEnabled)
            return NotFound();
        var customer = await db.Customers.FindAsync(CustomerId);
        if (customer == null || role is not ("Customer" or "Restaurant Staff"))
            return BadRequest();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, CustomerId.ToString()),
            new Claim("CustomerID", CustomerId.ToString()), new Claim(ClaimTypes.Name, customer.CustomerName), new Claim(ClaimTypes.Role, role),
            new Claim("DevelopmentPreview", "true") };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return RedirectToPage(role == "Customer" ? "/Reservations/Index" : "/Staff/Index");
    }
    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
