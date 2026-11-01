using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RROMS.Migrations
{
    /// <inheritdoc />
    public partial class AdministratorOwnershipAndMenuCategoryLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByAdministratorID",
                table: "StaffAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByAdministratorID",
                table: "StaffAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByAdministratorID",
                table: "RestaurantSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByAdministratorID",
                table: "RestaurantSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByAdministratorID",
                table: "MenuItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MenuCategoryID",
                table: "MenuItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByAdministratorID",
                table: "MenuItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByAdministratorID",
                table: "MenuCategories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByAdministratorID",
                table: "MenuCategories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByAdministratorID",
                table: "DiningAreas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByAdministratorID",
                table: "DiningAreas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByAdministratorID",
                table: "Announcements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByAdministratorID",
                table: "Announcements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Administrators",
                columns: table => new
                {
                    AdministratorID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Administrators", x => x.AdministratorID);
                    table.ForeignKey(
                        name: "FK_Administrators_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve existing accounts and catalogue data before enabling foreign keys.
            migrationBuilder.Sql("""
                INSERT INTO Administrators (CustomerID)
                SELECT CustomerID FROM Customers WHERE Role = N'Administrator';
                INSERT INTO MenuCategories (Name)
                SELECT DISTINCT i.Category FROM MenuItems i
                WHERE NOT EXISTS (SELECT 1 FROM MenuCategories c WHERE c.Name = i.Category);
                UPDATE i SET MenuCategoryID = c.MenuCategoryID
                FROM MenuItems i JOIN MenuCategories c ON c.Name = i.Category;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccounts_CreatedByAdministratorID",
                table: "StaffAccounts",
                column: "CreatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccounts_UpdatedByAdministratorID",
                table: "StaffAccounts",
                column: "UpdatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantSettings_CreatedByAdministratorID",
                table: "RestaurantSettings",
                column: "CreatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantSettings_UpdatedByAdministratorID",
                table: "RestaurantSettings",
                column: "UpdatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CreatedByAdministratorID",
                table: "MenuItems",
                column: "CreatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuCategoryID",
                table: "MenuItems",
                column: "MenuCategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_UpdatedByAdministratorID",
                table: "MenuItems",
                column: "UpdatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_CreatedByAdministratorID",
                table: "MenuCategories",
                column: "CreatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_UpdatedByAdministratorID",
                table: "MenuCategories",
                column: "UpdatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_DiningAreas_CreatedByAdministratorID",
                table: "DiningAreas",
                column: "CreatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_DiningAreas_UpdatedByAdministratorID",
                table: "DiningAreas",
                column: "UpdatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatedByAdministratorID",
                table: "Announcements",
                column: "CreatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_UpdatedByAdministratorID",
                table: "Announcements",
                column: "UpdatedByAdministratorID");

            migrationBuilder.CreateIndex(
                name: "IX_Administrators_CustomerID",
                table: "Administrators",
                column: "CustomerID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Administrators_CreatedByAdministratorID",
                table: "Announcements",
                column: "CreatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Administrators_UpdatedByAdministratorID",
                table: "Announcements",
                column: "UpdatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DiningAreas_Administrators_CreatedByAdministratorID",
                table: "DiningAreas",
                column: "CreatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DiningAreas_Administrators_UpdatedByAdministratorID",
                table: "DiningAreas",
                column: "UpdatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuCategories_Administrators_CreatedByAdministratorID",
                table: "MenuCategories",
                column: "CreatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuCategories_Administrators_UpdatedByAdministratorID",
                table: "MenuCategories",
                column: "UpdatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Administrators_CreatedByAdministratorID",
                table: "MenuItems",
                column: "CreatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Administrators_UpdatedByAdministratorID",
                table: "MenuItems",
                column: "UpdatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_MenuCategories_MenuCategoryID",
                table: "MenuItems",
                column: "MenuCategoryID",
                principalTable: "MenuCategories",
                principalColumn: "MenuCategoryID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RestaurantSettings_Administrators_CreatedByAdministratorID",
                table: "RestaurantSettings",
                column: "CreatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RestaurantSettings_Administrators_UpdatedByAdministratorID",
                table: "RestaurantSettings",
                column: "UpdatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffAccounts_Administrators_CreatedByAdministratorID",
                table: "StaffAccounts",
                column: "CreatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffAccounts_Administrators_UpdatedByAdministratorID",
                table: "StaffAccounts",
                column: "UpdatedByAdministratorID",
                principalTable: "Administrators",
                principalColumn: "AdministratorID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Administrators_CreatedByAdministratorID",
                table: "Announcements");

            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Administrators_UpdatedByAdministratorID",
                table: "Announcements");

            migrationBuilder.DropForeignKey(
                name: "FK_DiningAreas_Administrators_CreatedByAdministratorID",
                table: "DiningAreas");

            migrationBuilder.DropForeignKey(
                name: "FK_DiningAreas_Administrators_UpdatedByAdministratorID",
                table: "DiningAreas");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuCategories_Administrators_CreatedByAdministratorID",
                table: "MenuCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuCategories_Administrators_UpdatedByAdministratorID",
                table: "MenuCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Administrators_CreatedByAdministratorID",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Administrators_UpdatedByAdministratorID",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_MenuCategories_MenuCategoryID",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_RestaurantSettings_Administrators_CreatedByAdministratorID",
                table: "RestaurantSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_RestaurantSettings_Administrators_UpdatedByAdministratorID",
                table: "RestaurantSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffAccounts_Administrators_CreatedByAdministratorID",
                table: "StaffAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffAccounts_Administrators_UpdatedByAdministratorID",
                table: "StaffAccounts");

            migrationBuilder.DropTable(
                name: "Administrators");

            migrationBuilder.DropIndex(
                name: "IX_StaffAccounts_CreatedByAdministratorID",
                table: "StaffAccounts");

            migrationBuilder.DropIndex(
                name: "IX_StaffAccounts_UpdatedByAdministratorID",
                table: "StaffAccounts");

            migrationBuilder.DropIndex(
                name: "IX_RestaurantSettings_CreatedByAdministratorID",
                table: "RestaurantSettings");

            migrationBuilder.DropIndex(
                name: "IX_RestaurantSettings_UpdatedByAdministratorID",
                table: "RestaurantSettings");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_CreatedByAdministratorID",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_MenuCategoryID",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_UpdatedByAdministratorID",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuCategories_CreatedByAdministratorID",
                table: "MenuCategories");

            migrationBuilder.DropIndex(
                name: "IX_MenuCategories_UpdatedByAdministratorID",
                table: "MenuCategories");

            migrationBuilder.DropIndex(
                name: "IX_DiningAreas_CreatedByAdministratorID",
                table: "DiningAreas");

            migrationBuilder.DropIndex(
                name: "IX_DiningAreas_UpdatedByAdministratorID",
                table: "DiningAreas");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_CreatedByAdministratorID",
                table: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_UpdatedByAdministratorID",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "CreatedByAdministratorID",
                table: "StaffAccounts");

            migrationBuilder.DropColumn(
                name: "UpdatedByAdministratorID",
                table: "StaffAccounts");

            migrationBuilder.DropColumn(
                name: "CreatedByAdministratorID",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedByAdministratorID",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "CreatedByAdministratorID",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "MenuCategoryID",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "UpdatedByAdministratorID",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "CreatedByAdministratorID",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "UpdatedByAdministratorID",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "CreatedByAdministratorID",
                table: "DiningAreas");

            migrationBuilder.DropColumn(
                name: "UpdatedByAdministratorID",
                table: "DiningAreas");

            migrationBuilder.DropColumn(
                name: "CreatedByAdministratorID",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "UpdatedByAdministratorID",
                table: "Announcements");
        }
    }
}
