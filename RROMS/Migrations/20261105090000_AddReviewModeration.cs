using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261105090000_AddReviewModeration")]
    public partial class AddReviewModeration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Reviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenAt",
                table: "Reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenBy",
                table: "Reviews",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsHidden", table: "Reviews");
            migrationBuilder.DropColumn(name: "HiddenAt", table: "Reviews");
            migrationBuilder.DropColumn(name: "HiddenBy", table: "Reviews");
        }
    }
}
