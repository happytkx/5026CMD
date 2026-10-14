// Used by Entity Framework migration tools to construct the database context without starting the website. Normal website connections come from configuration.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace RROMS.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=RROMS;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
