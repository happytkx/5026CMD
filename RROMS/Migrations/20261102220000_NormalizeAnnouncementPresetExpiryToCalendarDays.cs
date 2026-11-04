using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations
{
    public partial class NormalizeAnnouncementPresetExpiryToCalendarDays : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Earlier presets ended at the exact time of day they were saved.
            // Keep the same final calendar date, but expire at the beginning of it
            // so the preceding date is the last full day of visibility.
            // Custom expiry dates and announcements without expiry are unchanged.
            migrationBuilder.Sql(@"
UPDATE [Announcements]
SET [ExpiresAt] = CAST(CAST([ExpiresAt] AS date) AS datetime2)
WHERE [ExpiresAt] IS NOT NULL
  AND [DurationMode] IN (N'1', N'7', N'30')
  AND CAST([ExpiresAt] AS time) <> '00:00:00';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original time of day cannot reliably be recovered for
            // announcements whose duration was updated after publication.
        }
    }
}
