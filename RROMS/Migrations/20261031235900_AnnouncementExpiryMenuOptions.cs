using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261031235900_AnnouncementExpiryMenuOptions")]
public partial class AnnouncementExpiryMenuOptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ExpiresAt",
            table: "Announcements",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "MenuOptions",
            columns: table => new
            {
                MenuOptionID = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_MenuOptions", x => x.MenuOptionID));

        migrationBuilder.CreateIndex(
            name: "IX_MenuOptions_Kind_Name",
            table: "MenuOptions",
            columns: new[] { "Kind", "Name" },
            unique: true);

        // Seed the choices already offered by the dish editor.
        migrationBuilder.Sql("""
            INSERT INTO MenuOptions ([Kind], [Name]) VALUES
            ('Protein','Vegetarian'), ('Protein','Chicken'), ('Protein','Beef'),
            ('Protein','Lamb'), ('Protein','Duck'), ('Protein','Seafood'), ('Protein','Molluscs'),
            ('Dietary','Vegetarian'), ('Dietary','Vegan'), ('Dietary','Pescatarian'),
            ('Dietary','Gluten-free'), ('Dietary','Dairy-free'), ('Dietary','No pork'),
            ('Dietary','No beef'), ('Dietary','No chicken'), ('Dietary','No nuts'),
            ('Allergen','Milk'), ('Allergen','Egg'), ('Allergen','Wheat'),
            ('Allergen','Peanuts'), ('Allergen','Tree nuts'), ('Allergen','Nuts'),
            ('Allergen','Soy'), ('Allergen','Fish'), ('Allergen','Shellfish'),
            ('Allergen','Sesame'), ('Allergen','Mustard'), ('Allergen','Celery'),
            ('Allergen','Sulphites');
            """);

        // Preserve custom values that already exist on dishes before options became configurable.
        migrationBuilder.Sql("""
            INSERT INTO MenuOptions ([Kind], [Name])
            SELECT 'Protein', legacy.[Name]
            FROM (
                SELECT DISTINCT LTRIM(RTRIM(ProteinType)) AS [Name]
                FROM MenuItems WHERE ProteinType IS NOT NULL
            ) AS legacy
            WHERE LEN(legacy.[Name]) BETWEEN 1 AND 40
              AND NOT EXISTS (SELECT 1 FROM MenuOptions o
                  WHERE o.[Kind] = 'Protein' AND o.[Name] = legacy.[Name]);

            ;WITH DietaryValues AS
            (
                SELECT DISTINCT LTRIM(RTRIM(s.value)) AS [Name]
                FROM MenuItems m CROSS APPLY STRING_SPLIT(m.DietaryTags, ',') s
            )
            INSERT INTO MenuOptions ([Kind], [Name])
            SELECT 'Dietary', legacy.[Name] FROM DietaryValues AS legacy
            WHERE LEN(legacy.[Name]) BETWEEN 1 AND 60
              AND NOT EXISTS (SELECT 1 FROM MenuOptions o
                  WHERE o.[Kind] = 'Dietary' AND o.[Name] = legacy.[Name]);

            ;WITH AllergenValues AS
            (
                SELECT DISTINCT LTRIM(RTRIM(s.value)) AS [Name]
                FROM MenuItems m CROSS APPLY STRING_SPLIT(m.Allergens, ',') s
            )
            INSERT INTO MenuOptions ([Kind], [Name])
            SELECT 'Allergen', legacy.[Name] FROM AllergenValues AS legacy
            WHERE LEN(legacy.[Name]) BETWEEN 1 AND 60
              AND NOT EXISTS (SELECT 1 FROM MenuOptions o
                  WHERE o.[Kind] = 'Allergen' AND o.[Name] = legacy.[Name]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MenuOptions");
        migrationBuilder.DropColumn(name: "ExpiresAt", table: "Announcements");
    }
}
