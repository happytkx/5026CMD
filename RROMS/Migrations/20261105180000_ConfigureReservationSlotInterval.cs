using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261105180000_ConfigureReservationSlotInterval")]
public partial class ConfigureReservationSlotInterval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SlotIntervalMinutes",
            table: "RestaurantSettings",
            type: "int",
            nullable: false,
            defaultValue: 30);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SlotIntervalMinutes",
            table: "RestaurantSettings");
    }
}
