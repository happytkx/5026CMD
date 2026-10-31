using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations
{
    /// <summary>
    /// Preserve an item subtotal and the tax already recorded by Sprint 3
    /// as separate values on each Sprint 4 final bill.
    /// </summary>
    public partial class Sprint4TaxAwareBills : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FoodSubtotal",
                table: "Bills",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Bills",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Older bills never stored tax separately. Preserve their
            // original billed amount rather than rewriting payment history.
            // Unpaid drafts must be regenerated before settlement because
            // CheckSettlementAsync validates them against source orders.
            migrationBuilder.Sql(
                "UPDATE [Bills] SET [FoodSubtotal] = [TotalBill] WHERE [FoodSubtotal] = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FoodSubtotal",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "Bills");
        }
    }
}
