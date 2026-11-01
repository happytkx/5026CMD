using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

internal static class AdministratorRelationshipChecks
{
    public static async Task Run(DbContextOptions<ApplicationDbContext> options, Action<bool, string> check)
    {
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        await using var db = new ApplicationDbContext(options, http);
        var account = new Administrator { Name = "Relationship Admin", Email = "relations@example.test", NormalizedEmail = "RELATIONS@EXAMPLE.TEST", PasswordHash = "test-only" };
        db.Administrators.Add(account);
        await db.SaveChangesAsync();
        var admin = await db.Administrators.SingleAsync(a => a.AdministratorID == account.AdministratorID);
        check(admin.AdministratorID == account.AdministratorID, "Provisioning administrator creates independent account");
        http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim("AdministratorID", account.AdministratorID.ToString()),
            new Claim(ClaimTypes.Role, Customer.AdminRole)
        }, "Test"));
        var category = new MenuCategory { Name = "Ownership test" };
        var dish = new MenuItem { Name = "Ownership dish", Category = category.Name, Price = 20 };
        IAdministratorManaged[] records = [category, dish, new Announcement { Title = "Test", Message = "Test" },
            new DiningArea { AreaType = "Ownership test" }, new RestaurantSetting(),
            new StaffAccount { StaffName = "Test", Email = "ownership@example.test", NormalizedEmail = "OWNERSHIP@EXAMPLE.TEST", PasswordHash = "test-only" }];
        db.AddRange(records.Cast<object>());
        await db.SaveChangesAsync();
        check(records.All(r => r.CreatedByAdministratorID == admin.AdministratorID && r.UpdatedByAdministratorID == admin.AdministratorID), "All six managed entities record administrator attribution");
        check(dish.MenuCategoryID == category.MenuCategoryID, "New menu item receives category foreign key");
        var second = new Administrator { Name = "Second Admin", Email = "second-relations@example.test", NormalizedEmail = "SECOND-RELATIONS@EXAMPLE.TEST", PasswordHash = "test-only" };
        db.Administrators.Add(second); await db.SaveChangesAsync();
        var other = await db.Administrators.SingleAsync(a => a.AdministratorID == second.AdministratorID);
        http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim("AdministratorID", second.AdministratorID.ToString()), new Claim(ClaimTypes.Role, Customer.AdminRole)
        }, "Test"));
        dish.Price = 25; await db.SaveChangesAsync();
        check(dish.CreatedByAdministratorID == admin.AdministratorID && dish.UpdatedByAdministratorID == other.AdministratorID, "Update preserves creator and records actual updating administrator");
        category.Name = "Ownership renamed"; dish.Category = category.Name; await db.SaveChangesAsync();
        check(dish.MenuCategoryID == category.MenuCategoryID, "Category rename preserves identity and item relationship");
        // No requests impersonate the administrator after this scope.
        http.HttpContext = null;
    }
}
