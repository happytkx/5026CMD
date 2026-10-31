using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261030210000_TrackDishKitchenStatuses")]
public partial class TrackDishKitchenStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "KitchenStatus",
            table: "OrderLines",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Received");

        // Carry existing round progress forward so previously served dishes
        // do not return to Received after deployment.
        migrationBuilder.Sql("""
            UPDATE line
            SET KitchenStatus = CASE
                WHEN line.IsCancelled = 1 OR ord.Status = 'Cancelled' THEN 'Cancelled'
                WHEN ord.Status IN ('Preparing', 'Ready to Serve', 'Served', 'Completed') THEN ord.Status
                ELSE 'Received'
            END
            FROM OrderLines AS line
            INNER JOIN Orders AS ord ON ord.OrderID = line.OrderID;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "KitchenStatus", table: "OrderLines");
    }
}
