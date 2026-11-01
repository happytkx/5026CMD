using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations
{
    /// <inheritdoc />
    public partial class SeparateInternalAccountCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Administrators_Customers_CustomerID",
                table: "Administrators");

            migrationBuilder.DropIndex(
                name: "IX_Administrators_CustomerID",
                table: "Administrators");

            migrationBuilder.AddColumn<int>(name: "SessionVersion", table: "Administrators", type: "int", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Administrators",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "Administrators",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Administrators",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Administrators",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntilUtc",
                table: "Administrators",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Administrators",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Administrators",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Administrators",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetExpiresUtc",
                table: "Administrators",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetSentUtc",
                table: "Administrators",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetTokenHash",
                table: "Administrators",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // Retain administrator primary keys and salted hashes. Abort rather than discard
            // customer activity if a legacy internal login was also used for dining.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Customers c WHERE c.Role <> 'Customer' AND
                    (EXISTS (SELECT 1 FROM Reservations r WHERE r.CustomerID=c.CustomerID)
                    OR EXISTS (SELECT 1 FROM Orders o WHERE o.CustomerID=c.CustomerID)
                    OR EXISTS (SELECT 1 FROM Reviews r WHERE r.CustomerID=c.CustomerID)
                    OR EXISTS (SELECT 1 FROM CustomerNotifications n WHERE n.CustomerID=c.CustomerID)))
                    THROW 51000, 'Internal accounts have customer activity. Separate that activity before migrating accounts.', 1;
                IF EXISTS (SELECT 1 FROM Customers c JOIN StaffAccounts s ON s.NormalizedEmail=UPPER(LTRIM(RTRIM(c.Email))) WHERE c.Role IN ('Restaurant Staff','Staff'))
                    THROW 51000, 'A legacy staff email already exists in StaffAccounts. Resolve the duplicate before migrating.', 1;
                IF EXISTS (SELECT 1 FROM Customers WHERE Role NOT IN ('Customer','Administrator','Restaurant Staff','Staff'))
                    THROW 51000, 'Unknown account role. Resolve before migrating accounts.', 1;

                INSERT INTO Administrators (CustomerID, Name, Email, NormalizedEmail, PasswordHash, IsActive, IsDemo, FailedLoginCount, SessionVersion)
                SELECT c.CustomerID, c.CustomerName, c.Email, UPPER(LTRIM(RTRIM(c.Email))), COALESCE(c.PasswordHash,''), 1, c.IsDemo, c.FailedLoginCount, c.SessionVersion+1
                FROM Customers c WHERE c.Role='Administrator' AND NOT EXISTS (SELECT 1 FROM Administrators a WHERE a.CustomerID=c.CustomerID);

                UPDATE a SET Name=c.CustomerName, Email=c.Email, NormalizedEmail=UPPER(LTRIM(RTRIM(c.Email))),
                    PasswordHash=COALESCE(c.PasswordHash,''), IsActive=CASE WHEN c.PasswordHash IS NULL THEN 0 ELSE 1 END,
                    IsDemo=c.IsDemo, FailedLoginCount=c.FailedLoginCount, SessionVersion=c.SessionVersion+1,
                    LockedUntilUtc=CASE WHEN c.LockoutEnd IS NOT NULL AND DATEADD(hour,-8,c.LockoutEnd)>COALESCE(c.LockedUntilUtc,'19000101') THEN DATEADD(hour,-8,c.LockoutEnd) ELSE c.LockedUntilUtc END
                FROM Administrators a JOIN Customers c ON c.CustomerID=a.CustomerID;

                INSERT INTO StaffAccounts (StaffName,Email,NormalizedEmail,PasswordHash,IsActive,IsDemo,FailedLoginCount,LockedUntilUtc,SessionVersion)
                SELECT CustomerName,Email,UPPER(LTRIM(RTRIM(Email))),COALESCE(PasswordHash,''),CASE WHEN PasswordHash IS NULL THEN 0 ELSE 1 END,IsDemo,FailedLoginCount,
                    CASE WHEN LockoutEnd IS NOT NULL AND DATEADD(hour,-8,LockoutEnd)>COALESCE(LockedUntilUtc,'19000101') THEN DATEADD(hour,-8,LockoutEnd) ELSE LockedUntilUtc END, SessionVersion+1
                FROM Customers WHERE Role IN ('Restaurant Staff','Staff');
                DELETE FROM Customers WHERE Role <> 'Customer';
                """);
            migrationBuilder.DropColumn(name: "CustomerID", table: "Administrators");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_CustomerRole",
                table: "Customers",
                sql: "[Role] = 'Customer'");

            migrationBuilder.CreateIndex(
                name: "IX_Administrators_NormalizedEmail",
                table: "Administrators",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Returning credentials to Customers would recreate internal/customer identities and
            // invalidate attribution. Restore the pre-migration backup for a full rollback.
            throw new NotSupportedException("Restore the pre-migration database backup to undo account separation without losing credentials.");
        }
    }
}
