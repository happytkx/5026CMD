using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261109100000_StaffNotificationDismissal")]
public partial class StaffNotificationDismissal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DismissedAt",
            table: "StaffNotificationReads",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DismissedAt",
            table: "StaffNotificationReads");
    }
}
