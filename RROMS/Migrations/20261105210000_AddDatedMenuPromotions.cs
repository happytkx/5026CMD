using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261105210000_AddDatedMenuPromotions")]
public partial class AddDatedMenuPromotions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "PromotionStartDate",
            table: "MenuItems",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PromotionEndDate",
            table: "MenuItems",
            type: "date",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PromotionStartDate", table: "MenuItems");
        migrationBuilder.DropColumn(name: "PromotionEndDate", table: "MenuItems");
    }
}
