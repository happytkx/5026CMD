// Separate reset tokens from signup verification; version cookies when passwords change.
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace RROMS.Migrations;
public partial class CustomerPasswordReset : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("PasswordResetTokenHash", "Customers", type: "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<DateTime>("PasswordResetExpiresUtc", "Customers", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>("PasswordResetSentUtc", "Customers", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<int>("SessionVersion", "Customers", type: "int", nullable: false, defaultValue: 0);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "PasswordResetTokenHash", "PasswordResetExpiresUtc", "PasswordResetSentUtc", "SessionVersion" })
            migrationBuilder.DropColumn(column, "Customers");
    }
}
