using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminReservationBillingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DepositPerGuest",
                table: "RestaurantSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MaximumPartySize",
                table: "RestaurantSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPartySize",
                table: "RestaurantSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "RestaurantSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositPerGuest",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "MaximumPartySize",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "MinimumPartySize",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "RestaurantSettings");
        }
    }
}
