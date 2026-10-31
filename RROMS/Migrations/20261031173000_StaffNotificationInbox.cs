using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261031173000_StaffNotificationInbox")]
public partial class StaffNotificationInbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StaffNotifications",
            columns: table => new
            {
                StaffNotificationID = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Message = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: false),
                Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Audience = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                TargetUrl = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_StaffNotifications", x => x.StaffNotificationID); });

        migrationBuilder.CreateTable(
            name: "StaffNotificationReads",
            columns: table => new
            {
                StaffNotificationReadID = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                StaffNotificationID = table.Column<int>(type: "int", nullable: false),
                RecipientKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StaffNotificationReads", x => x.StaffNotificationReadID);
                table.ForeignKey("FK_StaffNotificationReads_StaffNotifications_StaffNotificationID",
                    x => x.StaffNotificationID, "StaffNotifications", "StaffNotificationID", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_StaffNotifications_Audience_CreatedAt",
            table: "StaffNotifications", columns: new[] { "Audience", "CreatedAt" });

        migrationBuilder.CreateIndex(name: "IX_StaffNotificationReads_StaffNotificationID_RecipientKey",
            table: "StaffNotificationReads", columns: new[] { "StaffNotificationID", "RecipientKey" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StaffNotificationReads");
        migrationBuilder.DropTable(name: "StaffNotifications");
    }
}
