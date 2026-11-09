using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261106100000_GeneralCustomerReviews")]
public partial class GeneralCustomerReviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Reviews_Orders_OrderID", table: "Reviews");
        migrationBuilder.DropIndex(name: "IX_Reviews_OrderID", table: "Reviews");
        migrationBuilder.DropIndex(name: "IX_Reviews_CustomerID_OrderID", table: "Reviews");
        migrationBuilder.AlterColumn<int>(
            name: "OrderID", table: "Reviews", type: "int", nullable: true,
            oldClrType: typeof(int), oldType: "int");
        migrationBuilder.CreateIndex(name: "IX_Reviews_OrderID", table: "Reviews", column: "OrderID");
        migrationBuilder.CreateIndex(name: "IX_Reviews_CustomerID_OrderID", table: "Reviews",
            columns: new[] { "CustomerID", "OrderID" }, unique: true);
        migrationBuilder.AddForeignKey(name: "FK_Reviews_Orders_OrderID", table: "Reviews",
            column: "OrderID", principalTable: "Orders", principalColumn: "OrderID",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM [Reviews] WHERE [OrderID] IS NULL");
        migrationBuilder.DropForeignKey(name: "FK_Reviews_Orders_OrderID", table: "Reviews");
        migrationBuilder.DropIndex(name: "IX_Reviews_OrderID", table: "Reviews");
        migrationBuilder.DropIndex(name: "IX_Reviews_CustomerID_OrderID", table: "Reviews");
        migrationBuilder.AlterColumn<int>(
            name: "OrderID", table: "Reviews", type: "int", nullable: false,
            defaultValue: 0, oldClrType: typeof(int), oldType: "int", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_Reviews_OrderID", table: "Reviews", column: "OrderID");
        migrationBuilder.CreateIndex(name: "IX_Reviews_CustomerID_OrderID", table: "Reviews",
            columns: new[] { "CustomerID", "OrderID" }, unique: true);
        migrationBuilder.AddForeignKey(name: "FK_Reviews_Orders_OrderID", table: "Reviews",
            column: "OrderID", principalTable: "Orders", principalColumn: "OrderID",
            onDelete: ReferentialAction.Restrict);
    }
}
