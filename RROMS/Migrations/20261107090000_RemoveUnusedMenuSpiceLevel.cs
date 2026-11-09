using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261107090000_RemoveUnusedMenuSpiceLevel")]
public partial class RemoveUnusedMenuSpiceLevel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SpiceLevel",
            table: "MenuItems");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SpiceLevel",
            table: "MenuItems",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }
}
