using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RROMS.Data;

#nullable disable

namespace RROMS.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261106220000_RemoveDemoBarAndBookings")]
public partial class RemoveDemoBarAndBookings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // One-time cleanup of the explicitly confirmed Bar/B01 demo data.
        // Require the expected Bar (Indoor) relationship before touching any reservations.
        // EF Core executes this migration transactionally; an unexpected layout aborts safely.
        migrationBuilder.Sql("""
            IF (SELECT COUNT(*) FROM [DiningAreas]
                WHERE [AreaType] = N'Indoor' AND [DisplayName] = N'Bar') > 1
                THROW 51001, 'Multiple Bar dining areas found. Demo cleanup aborted.', 1;

            DECLARE @BarAreaId int = (
                SELECT TOP (1) [DiningAreaID] FROM [DiningAreas]
                WHERE [AreaType] = N'Indoor' AND [DisplayName] = N'Bar'
            );

            IF @BarAreaId IS NOT NULL
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM [RestaurantTables]
                    WHERE [DiningAreaID] = @BarAreaId AND [TableID] <> N'B01'
                )
                    THROW 51002, 'Bar contains a table other than B01. Demo cleanup aborted.', 1;

                IF EXISTS (
                    SELECT 1 FROM [RestaurantTables]
                    WHERE [TableID] = N'B01' AND [DiningAreaID] <> @BarAreaId
                )
                    THROW 51003, 'B01 does not belong to Bar. Demo cleanup aborted.', 1;

                DECLARE @ReservationIds table ([ReservationID] int PRIMARY KEY);
                INSERT INTO @ReservationIds ([ReservationID])
                SELECT [ReservationID] FROM [Reservations] WHERE [TableID] = N'B01';

                DECLARE @OrderIds table ([OrderID] int PRIMARY KEY);
                INSERT INTO @OrderIds ([OrderID])
                SELECT [OrderID] FROM [Orders]
                WHERE [ReservationID] IN (SELECT [ReservationID] FROM @ReservationIds);

                -- These relationships restrict deleting orders until their dependents are gone.
                DELETE cn FROM [CustomerNotifications] cn
                    INNER JOIN @OrderIds o ON cn.[RelatedOrderID] = o.[OrderID];
                DELETE rv FROM [Reviews] rv
                    INNER JOIN @OrderIds o ON rv.[OrderID] = o.[OrderID];
                DELETE ol FROM [OrderLines] ol
                    INNER JOIN @OrderIds o ON ol.[OrderID] = o.[OrderID];
                DELETE o FROM [Orders] o
                    INNER JOIN @OrderIds ids ON o.[OrderID] = ids.[OrderID];

                -- Remove every direct child of the demo reservations before removing them.
                DELETE b FROM [Bills] b
                    INNER JOIN @ReservationIds ids ON b.[ReservationID] = ids.[ReservationID];
                DELETE p FROM [Payments] p
                    INNER JOIN @ReservationIds ids ON p.[ReservationID] = ids.[ReservationID];
                DELETE e FROM [ReservationEmails] e
                    INNER JOIN @ReservationIds ids ON e.[ReservationID] = ids.[ReservationID];
                DELETE n FROM [Notifications] n
                    INNER JOIN @ReservationIds ids ON n.[ReservationID] = ids.[ReservationID];
                DELETE h FROM [ReservationHistories] h
                    INNER JOIN @ReservationIds ids ON h.[ReservationID] = ids.[ReservationID];
                DELETE r FROM [Reservations] r
                    INNER JOIN @ReservationIds ids ON r.[ReservationID] = ids.[ReservationID];

                DELETE FROM [RestaurantTables] WHERE [TableID] = N'B01' AND [DiningAreaID] = @BarAreaId;
                DELETE FROM [DiningAreas] WHERE [DiningAreaID] = @BarAreaId
                    AND NOT EXISTS (
                        SELECT 1 FROM [RestaurantTables] WHERE [DiningAreaID] = @BarAreaId
                    );
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deleted demo reservations and their transactions cannot be reconstructed.
    }
}
