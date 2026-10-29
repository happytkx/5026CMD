// Shared account helpers: password rules and the cookie sign-in that issues the CustomerID, name and role claims read by ReservationIdentity and the authorization policies.
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using RROMS.Models;

namespace RROMS.Services;

public static class CustomerAuth
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);

    // Password rules: at least 8 characters with a digit, an upper-case and a lower-case letter.
    public static IEnumerable<string> PasswordProblems(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            yield return "Password must be at least 8 characters.";
        if (password == null)
            yield break;
        if (!password.Any(char.IsDigit))
            yield return "Password must contain a digit.";
        if (!password.Any(char.IsUpper))
            yield return "Password must contain an upper-case letter.";
        if (!password.Any(char.IsLower))
            yield return "Password must contain a lower-case letter.";
    }

    public static Task SignInAsync(HttpContext http, Customer customer, bool persistent = false)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customer.CustomerID.ToString()),
            new Claim("CustomerID", customer.CustomerID.ToString()),
            new Claim("SessionVersion", customer.SessionVersion.ToString()),
            new Claim(ClaimTypes.Name, customer.CustomerName),
            new Claim(ClaimTypes.Role, customer.Role)
        };
        return http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = persistent });
    }

    // Where each kind of account lands after signing in.
    public static string LandingPage(string role) => role switch
    {
        Customer.AdminRole => "/Admin/Index",
        Customer.StaffRole => "/Staff/Index",
        _ => "/Reservations/Index"
    };
}
