using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261106183000_EditableRestaurantPolicies")]
public partial class EditableRestaurantPolicies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "DepositPerGuest", table: "RestaurantSettings", type: "decimal(18,2)", nullable: false, defaultValue: 50m);
        migrationBuilder.AddColumn<decimal>(name: "StandardMinimumSpendPerGuest", table: "RestaurantSettings", type: "decimal(18,2)", nullable: false, defaultValue: 50m);
        migrationBuilder.AddColumn<decimal>(name: "VipEightSeatMinimumSpend", table: "RestaurantSettings", type: "decimal(18,2)", nullable: false, defaultValue: 2400m);
        migrationBuilder.AddColumn<decimal>(name: "VipTwelveSeatMinimumSpend", table: "RestaurantSettings", type: "decimal(18,2)", nullable: false, defaultValue: 3600m);
        migrationBuilder.AddColumn<int>(name: "RescheduleCutoffHours", table: "RestaurantSettings", type: "int", nullable: false, defaultValue: 168);
        migrationBuilder.AddColumn<int>(name: "MaximumReschedules", table: "RestaurantSettings", type: "int", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<int>(name: "ArrivalGraceMinutes", table: "RestaurantSettings", type: "int", nullable: false, defaultValue: 10);
        migrationBuilder.AddColumn<int>(name: "ExtendedArrivalGraceMinutes", table: "RestaurantSettings", type: "int", nullable: false, defaultValue: 30);
        migrationBuilder.AddColumn<int>(name: "StandardDiningMinutes", table: "RestaurantSettings", type: "int", nullable: false, defaultValue: 120);
        migrationBuilder.AddColumn<int>(name: "StandardAdvanceHours", table: "RestaurantSettings", type: "int", nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<int>(name: "VipAdvanceHours", table: "RestaurantSettings", type: "int", nullable: false, defaultValue: 24);
        migrationBuilder.AddColumn<int>(name: "PaymentHoldMinutes", table: "RestaurantSettings", type: "int", nullable: false, defaultValue: 10);
        migrationBuilder.AddColumn<string>(name: "GeneralGuidelines", table: "RestaurantSettings", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "PoliciesUpdatedAt", table: "RestaurantSettings", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<int>(name: "StandardDiningMinutes", table: "Reservations", type: "int", nullable: false, defaultValue: 120);
        migrationBuilder.AddColumn<int>(name: "RescheduleCutoffHours", table: "Reservations", type: "int", nullable: false, defaultValue: 168);
        migrationBuilder.AddColumn<int>(name: "MaximumReschedules", table: "Reservations", type: "int", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<int>(name: "ArrivalGraceMinutes", table: "Reservations", type: "int", nullable: false, defaultValue: 10);
        migrationBuilder.AddColumn<int>(name: "ExtendedArrivalGraceMinutes", table: "Reservations", type: "int", nullable: false, defaultValue: 30);
        migrationBuilder.AddColumn<int>(name: "StandardAdvanceHours", table: "Reservations", type: "int", nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<int>(name: "VipAdvanceHours", table: "Reservations", type: "int", nullable: false, defaultValue: 24);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DepositPerGuest", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "StandardMinimumSpendPerGuest", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "VipEightSeatMinimumSpend", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "VipTwelveSeatMinimumSpend", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "RescheduleCutoffHours", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "MaximumReschedules", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "ArrivalGraceMinutes", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "ExtendedArrivalGraceMinutes", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "StandardDiningMinutes", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "StandardAdvanceHours", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "VipAdvanceHours", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "PaymentHoldMinutes", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "GeneralGuidelines", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "PoliciesUpdatedAt", table: "RestaurantSettings");
        migrationBuilder.DropColumn(name: "StandardDiningMinutes", table: "Reservations");
        migrationBuilder.DropColumn(name: "RescheduleCutoffHours", table: "Reservations");
        migrationBuilder.DropColumn(name: "MaximumReschedules", table: "Reservations");
        migrationBuilder.DropColumn(name: "ArrivalGraceMinutes", table: "Reservations");
        migrationBuilder.DropColumn(name: "ExtendedArrivalGraceMinutes", table: "Reservations");
        migrationBuilder.DropColumn(name: "StandardAdvanceHours", table: "Reservations");
        migrationBuilder.DropColumn(name: "VipAdvanceHours", table: "Reservations");
    }
}
