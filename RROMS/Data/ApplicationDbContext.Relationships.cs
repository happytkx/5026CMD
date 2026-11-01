using Microsoft.EntityFrameworkCore;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Data;

public partial class ApplicationDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareRelationships();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareRelationships();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareRelationships()
    {
        // Preserve the existing category-name interface while enforcing a real category FK.
        foreach (var entry in ChangeTracker.Entries<MenuItem>().Where(e =>
            e.State == EntityState.Added || e.State == EntityState.Modified).ToList())
        {
            var item = entry.Entity;
            var category = MenuCategories.Local.FirstOrDefault(c => c.Name == item.Category)
                ?? MenuCategories.SingleOrDefault(c => c.Name == item.Category);
            if (category == null)
            {
                // Supports existing catalogue seed/import paths; admin forms validate categories first.
                category = new MenuCategory { Name = item.Category };
                MenuCategories.Add(category);
            }
            item.MenuCategory = category;
        }

        var user = _http?.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true || !user.IsInRole(Customer.AdminRole)) return;
        var accountId = int.TryParse(user.FindFirst("AdministratorID")?.Value, out var id) ? id : -1;
        var administratorId = Administrators.Where(a => a.AdministratorID == accountId && a.IsActive)
            .Select(a => (int?)a.AdministratorID).SingleOrDefault();
        if (administratorId == null) throw new InvalidOperationException("Administrator profile is missing. Please sign in again.");
        foreach (var entry in ChangeTracker.Entries().Where(e => e.Entity is IAdministratorManaged &&
            (e.State == EntityState.Added || e.State == EntityState.Modified)).ToList())
        {
            var managed = (IAdministratorManaged)entry.Entity;
            if (entry.State == EntityState.Added) managed.CreatedByAdministratorID = administratorId;
            else entry.Property(nameof(IAdministratorManaged.CreatedByAdministratorID)).IsModified = false;
            managed.UpdatedByAdministratorID = administratorId;
        }
    }
}
