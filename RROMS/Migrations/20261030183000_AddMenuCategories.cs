using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261030183000_AddMenuCategories")]
public partial class AddMenuCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MenuCategories",
            columns: table => new
            {
                MenuCategoryID = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_MenuCategories", x => x.MenuCategoryID));

        migrationBuilder.CreateIndex(
            name: "IX_MenuCategories_Name",
            table: "MenuCategories",
            column: "Name",
            unique: true);

        // Keep all existing menu classifications, including currently unavailable dishes.
        migrationBuilder.Sql("""
            INSERT INTO MenuCategories (Name)
            SELECT DISTINCT Category FROM MenuItems
            WHERE Category IS NOT NULL AND LEN(LTRIM(RTRIM(Category))) > 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MenuCategories");
    }
}
