using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations
{
    public partial class AddAnnouncementDurationMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DurationMode",
                table: "Announcements",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "never");

            // Match unchanged preset durations for existing announcements.
            // Any duration not recognisable as a preset keeps its date as custom.
            migrationBuilder.Sql(@"
UPDATE [Announcements]
SET [DurationMode] = CASE
    WHEN [ExpiresAt] IS NULL THEN N'never'
    WHEN ABS(DATEDIFF(second, [CreatedAt], [ExpiresAt]) - 86400) <= 120 THEN N'1'
    WHEN ABS(DATEDIFF(second, [CreatedAt], [ExpiresAt]) - 604800) <= 120 THEN N'7'
    WHEN ABS(DATEDIFF(second, [CreatedAt], [ExpiresAt]) - 2592000) <= 120 THEN N'30'
    ELSE N'custom'
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMode",
                table: "Announcements");
        }
    }
}
