using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Admin;

[Authorize(Policy = "Admin")]
public class AccountDetailsModel(ApplicationDbContext db, IPasswordHasher<StaffAccount> staffHasher,
    IPasswordHasher<Customer> customerHasher) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Type { get; set; } = "customer";
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public bool Edit { get; set; }

    public bool IsCustomer => string.Equals(Type, "customer", StringComparison.OrdinalIgnoreCase);
    public Customer? CustomerProfile { get; private set; }
    public StaffAccount? StaffProfile { get; private set; }
    public List<Reservation> Reservations { get; private set; } = [];
    public List<CustomerOrder> Orders { get; private set; } = [];
    public List<Review> CustomerReviews { get; private set; } = [];
    public List<(string Name, int Quantity)> FavouriteDishes { get; private set; } = [];
    public int TotalReservations { get; private set; }
    public int CompletedVisits { get; private set; }
    public int TotalOrders { get; private set; }
    public decimal TotalPaidDining { get; private set; }

    private int? AdministratorId =>
        int.TryParse(User.FindFirst("AdministratorID")?.Value, out var id) ? id : null;
    private IActionResult Feedback(string message, bool error = false, bool editing = false)
    {
        TempData[error ? "AccountProfileError" : "AccountProfileSuccess"] = message;
        return RedirectToPage(new { type = Type, id = Id, edit = editing });
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id <= 0 || (Type != "customer" && Type != "staff")) return NotFound();
        if (IsCustomer)
        {
            CustomerProfile = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerID == Id);
            if (CustomerProfile is null) return NotFound();
            TotalReservations = await db.Reservations.CountAsync(r => r.CustomerID == Id);
            CompletedVisits = await db.Reservations.CountAsync(r => r.CustomerID == Id && r.ReservationStatus == "Completed");
            TotalOrders = await db.Orders.CountAsync(o => o.CustomerID == Id);
            TotalPaidDining = await (from bill in db.Bills.AsNoTracking()
                                     join reservation in db.Reservations.AsNoTracking() on bill.ReservationID equals reservation.ReservationID
                                     where reservation.CustomerID == Id && bill.BillStatus == "Paid"
                                     select (decimal?)bill.TotalBill).SumAsync() ?? 0m;
            Reservations = await db.Reservations.AsNoTracking().Where(r => r.CustomerID == Id)
                .OrderByDescending(r => r.ReservationDate).ThenByDescending(r => r.StartTime)
                .Take(30).ToListAsync();
            Orders = await db.Orders.AsNoTracking().Include(o => o.Lines).Where(o => o.CustomerID == Id)
                .OrderByDescending(o => o.CreatedAt).Take(30).ToListAsync();
            var favouriteRows = await db.OrderLines.AsNoTracking()
                .Where(l => l.Order.CustomerID == Id && l.Order.Status != "Cancelled" && !l.IsCancelled)
                .GroupBy(l => l.ItemName)
                .Select(g => new { Name = g.Key, Quantity = g.Sum(l => l.Quantity) })
                .OrderByDescending(g => g.Quantity).ThenBy(g => g.Name).Take(5).ToListAsync();
            FavouriteDishes = favouriteRows.Select(row => (row.Name, row.Quantity)).ToList();
            CustomerReviews = await db.Reviews.AsNoTracking().Where(r => r.CustomerID == Id)
                .OrderByDescending(r => r.CreatedAt).Take(10).ToListAsync();
        }
        else
        {
            StaffProfile = await db.StaffAccounts.AsNoTracking().SingleOrDefaultAsync(s => s.StaffAccountID == Id);
            if (StaffProfile is null) return NotFound();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(string type, int id, string? name,
        string? email, string? phoneNo, string? address, string? newPassword)
    {
        Type = type;
        Id = id;
        if (Id <= 0 || (Type != "customer" && Type != "staff")) return NotFound();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(email) || email.Trim().Length > 100 ||
            !new EmailAddressAttribute().IsValid(email.Trim()))
            return Feedback("Enter a valid name and email, up to 100 characters each.", true, true);
        if (!string.IsNullOrEmpty(newPassword) && newPassword.Length is < 10 or > 128)
            return Feedback("A replacement password must contain 10–128 characters.", true, true);
        if (IsCustomer && (phoneNo?.Trim().Length > 20 || address?.Trim().Length > 250))
            return Feedback("Phone must be 20 characters or fewer and address 250 characters or fewer.", true, true);

        var cleanEmail = email.Trim();
        var normalized = CustomerAccountService.Normalize(cleanEmail);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var usedByAdmin = await db.Administrators.AnyAsync(a => a.NormalizedEmail == normalized);
        var usedByStaff = await db.StaffAccounts.AnyAsync(a => a.NormalizedEmail == normalized &&
            (IsCustomer || a.StaffAccountID != Id));
        var usedByCustomer = await db.Customers.AnyAsync(c =>
            (c.NormalizedEmail == normalized || c.Email.ToUpper() == normalized) &&
            (!IsCustomer || c.CustomerID != Id));
        if (usedByAdmin || usedByStaff || usedByCustomer)
            return Feedback("This email is already used by another account.", true, true);

        if (IsCustomer)
        {
            var customer = await db.Customers.FindAsync(Id);
            if (customer is null) return NotFound();
            var credentialsChanged = customer.NormalizedEmail != normalized || !string.IsNullOrEmpty(newPassword);
            customer.CustomerName = name.Trim();
            customer.Email = cleanEmail;
            customer.NormalizedEmail = normalized;
            customer.PhoneNo = phoneNo?.Trim() ?? "";
            customer.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
            if (!string.IsNullOrEmpty(newPassword))
            {
                customer.PasswordHash = customerHasher.HashPassword(customer, newPassword);
                customer.EmailConfirmed = true;
                customer.FailedLoginCount = 0;
                customer.LockedUntilUtc = null;
                customer.LockoutEnd = null;
            }
            if (credentialsChanged) customer.SessionVersion++;
        }
        else
        {
            var staff = await db.StaffAccounts.FindAsync(Id);
            if (staff is null) return NotFound();
            var credentialsChanged = staff.NormalizedEmail != normalized || !string.IsNullOrEmpty(newPassword);
            staff.StaffName = name.Trim();
            staff.Email = cleanEmail;
            staff.NormalizedEmail = normalized;
            staff.UpdatedByAdministratorID = AdministratorId;
            if (!string.IsNullOrEmpty(newPassword))
            {
                staff.PasswordHash = staffHasher.HashPassword(staff, newPassword);
                staff.FailedLoginCount = 0;
                staff.LockedUntilUtc = null;
            }
            if (credentialsChanged) staff.SessionVersion++;
        }
        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Feedback("Could not save these changes. Check for a duplicate email and try again.", true, true);
        }
        return Feedback("Account changes saved successfully.");
    }

    public async Task<IActionResult> OnPostSetActiveAsync(string type, int id, bool isActive)
    {
        Type = type;
        Id = id;
        if (Id <= 0 || (Type != "customer" && Type != "staff")) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (IsCustomer)
        {
            var customer = await db.Customers.FindAsync(Id);
            if (customer is null) return NotFound();
            if (customer.IsActive != isActive)
            {
                customer.IsActive = isActive;
                customer.SessionVersion++;
                if (isActive)
                {
                    customer.FailedLoginCount = 0;
                    customer.LockedUntilUtc = null;
                    customer.LockoutEnd = null;
                }
            }
        }
        else
        {
            var staff = await db.StaffAccounts.FindAsync(Id);
            if (staff is null) return NotFound();
            if (staff.IsActive != isActive)
            {
                staff.IsActive = isActive;
                staff.SessionVersion++;
                staff.UpdatedByAdministratorID = AdministratorId;
                if (isActive)
                {
                    staff.FailedLoginCount = 0;
                    staff.LockedUntilUtc = null;
                }
            }
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Feedback(isActive ? "Account reactivated." : "Account deactivated. Existing sign-in sessions have been revoked.");
    }
}
