// Password recovery invalidates all older customer cookies on their next request.
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
namespace RROMS.Services;

public class CustomerCookieEvents(ApplicationDbContext db, IWebHostEnvironment environment, IConfiguration config) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var user = context.Principal;
        if (user == null) return;
        if (environment.IsDevelopment() && config.GetValue<bool>("Sprint2:EnableDemoAccess") &&
            user.FindFirst("DevelopmentPreview")?.Value == "true") return;
        if (user.IsInRole("Administrator"))
        {
            var administratorId = int.TryParse(user.FindFirst("AdministratorID")?.Value, out var parsedAdmin) ? parsedAdmin : -1;
            var administrator = await db.Administrators.AsNoTracking().SingleOrDefaultAsync(a => a.AdministratorID == administratorId && a.IsActive);
            if (administrator == null || user.FindFirst("SessionVersion")?.Value != administrator.SessionVersion.ToString() ||
                (administrator.IsDemo && (!environment.IsDevelopment() || !config.GetValue<bool>("DemoData:Enabled"))))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            return;
        }
        if (user.FindFirst("StaffAccountID") != null)
        {
            var staffId = int.TryParse(user.FindFirst("StaffAccountID")?.Value, out var parsed) ? parsed : -1;
            var staff = await db.StaffAccounts.AsNoTracking().SingleOrDefaultAsync(s => s.StaffAccountID == staffId && s.IsActive);
            if (staff == null || !user.IsInRole("Restaurant Staff") || user.FindFirst("SessionVersion")?.Value != staff.SessionVersion.ToString() ||
                (staff.IsDemo && (!environment.IsDevelopment() || !config.GetValue<bool>("DemoData:Enabled"))))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            return;
        }
        if (!user.IsInRole("Customer") && !user.IsInRole("Restaurant Staff") && !user.IsInRole("Administrator")) return;
        var id = ReservationIdentity.CustomerId(user);
        var account = await db.Customers.AsNoTracking().Where(c => c.CustomerID == id && c.Role == "Customer" && c.PasswordHash != null)
            .Select(c => new { c.SessionVersion, c.IsDemo, c.Role }).SingleOrDefaultAsync();
        if (account == null || user.FindFirst("SessionVersion")?.Value != account.SessionVersion.ToString() ||
            !user.IsInRole(account.Role) ||
            (account.IsDemo && (!environment.IsDevelopment() || !config.GetValue<bool>("DemoData:Enabled"))))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
