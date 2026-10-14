// Adds booking lifecycle fields and opening hours, with backfills for existing reservations. Later migrations may remove unused settings.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2ReservationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_TableID",
                table: "Reservations");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ClosingTime",
                table: "RestaurantSettings",
                type: "time",
                nullable: false,
                defaultValue: TimeSpan.FromHours(22));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "OpeningTime",
                table: "RestaurantSettings",
                type: "time",
                nullable: false,
                defaultValue: TimeSpan.FromHours(12));

            migrationBuilder.AlterColumn<string>(
                name: "BookingReference",
                table: "Reservations",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationDate",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedInAt",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Reservations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "HoldExpiresAt",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LateArrivalNotified",
                table: "Reservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumSpend",
                table: "Reservations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalStart",
                table: "Reservations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Reservations",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            // Preserve existing bookings and their original modification deadline.
            migrationBuilder.Sql("UPDATE RestaurantSettings SET ReservationDurationMinutes = 120;");
            migrationBuilder.Sql("UPDATE Reservations SET OriginalStart = DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', StartTime), ReservationDate), CreatedDate = DATEADD(HOUR, 8, GETUTCDATE()), MinimumSpend = CASE WHEN PreferredDiningOption = 'VIP Room' THEN CASE WHEN PartySize <= 8 THEN 2400 ELSE 3600 END ELSE PartySize * 50 END, HoldExpiresAt = CASE WHEN ReservationStatus = 'Pending' THEN DATEADD(MINUTE, 10, DATEADD(HOUR, 8, GETUTCDATE())) ELSE NULL END;");
            migrationBuilder.Sql("UPDATE r SET OriginalStart = DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', h.OldTime), h.OldDate) FROM Reservations r CROSS APPLY (SELECT TOP 1 OldDate, OldTime FROM ReservationHistories WHERE ReservationID = r.ReservationID ORDER BY ChangedDate, HistoryID) h;");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_BookingReference",
                table: "Reservations",
                column: "BookingReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_TableID_ReservationDate_StartTime",
                table: "Reservations",
                columns: new[] { "TableID", "ReservationDate", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_BookingReference",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_TableID_ReservationDate_StartTime",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ClosingTime",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "OpeningTime",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "CancellationDate",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CheckedInAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "HoldExpiresAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "LateArrivalNotified",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "MinimumSpend",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "OriginalStart",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Reservations");

            migrationBuilder.AlterColumn<string>(
                name: "BookingReference",
                table: "Reservations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_TableID",
                table: "Reservations",
                column: "TableID");
        }
    }
}



