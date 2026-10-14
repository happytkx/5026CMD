// Removes eight unused customer, dining-area and settings columns. Booking rows and fixed reservation policies are retained.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedSprint2Attributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositPerPerson",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "GracePeriod",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "LateArrivalPeriod",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "MaxModificationCount",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "ReservationDurationMinutes",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "AreaName",
                table: "DiningAreas");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "DiningAreas");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "Customers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DepositPerPerson",
                table: "RestaurantSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "GracePeriod",
                table: "RestaurantSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LateArrivalPeriod",
                table: "RestaurantSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxModificationCount",
                table: "RestaurantSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReservationDurationMinutes",
                table: "RestaurantSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AreaName",
                table: "DiningAreas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "DiningAreas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}

