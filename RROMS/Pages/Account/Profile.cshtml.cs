// Lets a signed-in user view and update their name, phone number and address. The sign-in email is read-only.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Account;

[Authorize]
public class ProfileModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public ProfileInput Input { get; set; } = new();
    public bool IsCustomer => User.IsInRole(Customer.CustomerRole);
    private int InternalId(string claim) => int.TryParse(User.FindFirst(claim)?.Value, out var id) ? id : -1;
    public string Email { get; set; } = "";

    public class ProfileInput
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = "";
        [Phone, StringLength(20)]
        public string? PhoneNo
        {
            get; set;
        }
        [StringLength(250)]
        public string? Address
        {
            get; set;
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsCustomer) return await InternalProfile(false);
        var customer = await Find();
        if (customer == null)
            return Challenge();
        Email = customer.Email;
        Input = new() { FullName = customer.CustomerName, PhoneNo = customer.PhoneNo, Address = customer.Address };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsCustomer) return await InternalProfile(true);
        var customer = await Find();
        if (customer == null)
            return Challenge();
        if (!ModelState.IsValid)
        {
            Email = customer.Email;
            return Page();
        }
        customer.CustomerName = Input.FullName.Trim();
        customer.PhoneNo = Input.PhoneNo?.Trim() ?? "";
        customer.Address = string.IsNullOrWhiteSpace(Input.Address) ? null : Input.Address.Trim();
        await db.SaveChangesAsync();
        // Refresh the cookie so the name shown in the navigation updates immediately.
        await CustomerAuth.SignInAsync(HttpContext, customer);
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToPage();
    }

    private async Task<IActionResult> InternalProfile(bool save)
    {
        var admin = User.IsInRole(Customer.AdminRole) ? await db.Administrators.FindAsync(InternalId("AdministratorID")) : null;
        var staff = User.IsInRole(Customer.StaffRole) ? await db.StaffAccounts.FindAsync(InternalId("StaffAccountID")) : null;
        if (admin == null && staff == null) return Challenge();
        Email = admin?.Email ?? staff!.Email;
        if (!save) { Input.FullName = admin?.Name ?? staff!.StaffName; return Page(); }
        if (!ModelState.IsValid) return Page();
        if (admin != null) admin.Name = Input.FullName.Trim(); else staff!.StaffName = Input.FullName.Trim();
        await db.SaveChangesAsync();
        if (admin != null) await InternalAccountAuth.SignInAsync(HttpContext, admin);
        else await InternalAccountAuth.SignInAsync(HttpContext, staff!);
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToPage();
    }

    private Task<Customer?> Find()
    {
        var id = ReservationIdentity.CustomerId(User);
        return db.Customers.FirstOrDefaultAsync(c => c.CustomerID == id);
    }
}
