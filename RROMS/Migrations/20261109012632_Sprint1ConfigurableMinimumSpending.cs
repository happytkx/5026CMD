using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1ConfigurableMinimumSpending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StandardMinimumSpendPerGuest",
                table: "RestaurantSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<decimal>(
                name: "VipMinimumSpendLargeGroup",
                table: "RestaurantSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 3600m);

            migrationBuilder.AddColumn<decimal>(
                name: "VipMinimumSpendSmallGroup",
                table: "RestaurantSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 2400m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandardMinimumSpendPerGuest",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "VipMinimumSpendLargeGroup",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "VipMinimumSpendSmallGroup",
                table: "RestaurantSettings");
        }
    }
}
