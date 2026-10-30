// Adds customer credentials without deleting or reassigning existing reservation records.
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace RROMS.Migrations;
public partial class CustomerAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("NormalizedEmail", "Customers", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.Sql("IF COL_LENGTH('dbo.Customers', 'PasswordHash') IS NULL ALTER TABLE [Customers] ADD [PasswordHash] nvarchar(256) NULL;");
        migrationBuilder.AddColumn<bool>("EmailConfirmed", "Customers", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("VerificationTokenHash", "Customers", type: "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<DateTime>("VerificationExpiresUtc", "Customers", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>("VerificationSentUtc", "Customers", type: "datetime2", nullable: true);
        migrationBuilder.Sql("IF COL_LENGTH('dbo.Customers', 'FailedLoginCount') IS NULL ALTER TABLE [Customers] ADD [FailedLoginCount] int NOT NULL DEFAULT 0;");
        migrationBuilder.AddColumn<DateTime>("LockedUntilUtc", "Customers", type: "datetime2", nullable: true);
        migrationBuilder.CreateIndex("IX_Customers_NormalizedEmail", "Customers", "NormalizedEmail", unique: true, filter: "[NormalizedEmail] IS NOT NULL");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Customers_NormalizedEmail", "Customers");
        foreach (var column in new[] { "NormalizedEmail", "PasswordHash", "EmailConfirmed", "VerificationTokenHash", "VerificationExpiresUtc", "VerificationSentUtc", "FailedLoginCount", "LockedUntilUtc" })
            migrationBuilder.DropColumn(column, "Customers");
    }
}
