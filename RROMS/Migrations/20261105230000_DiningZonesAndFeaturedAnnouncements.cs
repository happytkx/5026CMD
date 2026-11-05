using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261105230000_DiningZonesAndFeaturedAnnouncements")]
public partial class DiningZonesAndFeaturedAnnouncements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DisplayName", table: "DiningAreas",
            type: "nvarchar(80)", maxLength: 80, nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "StartsAt", table: "Announcements",
            type: "datetime2", nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsFeatured", table: "Announcements",
            type: "bit", nullable: false, defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsPromotional", table: "Announcements",
            type: "bit", nullable: false, defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "ShowInPopup", table: "Announcements",
            type: "bit", nullable: false, defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DisplayName", table: "DiningAreas");
        migrationBuilder.DropColumn(name: "StartsAt", table: "Announcements");
        migrationBuilder.DropColumn(name: "IsFeatured", table: "Announcements");
        migrationBuilder.DropColumn(name: "IsPromotional", table: "Announcements");
        migrationBuilder.DropColumn(name: "ShowInPopup", table: "Announcements");
    }
}
