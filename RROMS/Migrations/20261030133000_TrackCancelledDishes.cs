using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations;

public partial class TrackCancelledDishes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsCancelled",
            table: "OrderLines",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "CancelledAt",
            table: "OrderLines",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CancelledBy",
            table: "OrderLines",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CancelledBy", table: "OrderLines");
        migrationBuilder.DropColumn(name: "CancelledAt", table: "OrderLines");
        migrationBuilder.DropColumn(name: "IsCancelled", table: "OrderLines");
    }
}
