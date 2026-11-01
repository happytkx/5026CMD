using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using RROMS.Models;
namespace RROMS.Services;
public static class InternalAccountAuth
{
    public static Task SignInAsync(HttpContext http, Administrator a, bool persistent = false) =>
        SignIn(http, "AdministratorID", "admin:" + a.AdministratorID, a.AdministratorID, a.Name, a.Email, Customer.AdminRole, a.SessionVersion, persistent);
    public static Task SignInAsync(HttpContext http, StaffAccount a, bool persistent = false) =>
        SignIn(http, "StaffAccountID", "staff:" + a.StaffAccountID, a.StaffAccountID, a.StaffName, a.Email, Customer.StaffRole, a.SessionVersion, persistent);
    private static Task SignIn(HttpContext http, string key, string subject, int id, string name, string email, string role, int version, bool persistent) =>
        http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, subject), new Claim(key, id.ToString()),
            new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role),
            new Claim("SessionVersion", version.ToString())
        }, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = persistent });
}
