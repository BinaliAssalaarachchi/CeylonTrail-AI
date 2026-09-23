using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeylonTrail.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAttractionsRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_Users_UserId",
                table: "Favorites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExperienceSlots_Price_NonNegative",
                table: "ExperienceSlots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExperienceSlots_ReservedCount_Valid",
                table: "ExperienceSlots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttractionSchedules_TimeRange",
                table: "AttractionSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Attractions_CategoryId_IsApproved_IsActive",
                table: "Attractions");

            migrationBuilder.DropIndex(
                name: "IX_Attractions_ProviderId_IsApproved",
                table: "Attractions");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "ExperienceSlots");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Attractions");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Attractions");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Favorites",
                newName: "TouristId");

            migrationBuilder.RenameIndex(
                name: "IX_Favorites_UserId_AttractionId",
                table: "Favorites",
                newName: "IX_Favorites_TouristId_AttractionId");

            migrationBuilder.RenameColumn(
                name: "ReservedCount",
                table: "ExperienceSlots",
                newName: "AvailableCapacity");

            migrationBuilder.RenameColumn(
                name: "OpenTime",
                table: "AttractionSchedules",
                newName: "OpeningTime");

            migrationBuilder.RenameColumn(
                name: "CloseTime",
                table: "AttractionSchedules",
                newName: "ClosingTime");

            migrationBuilder.RenameColumn(
                name: "Location",
                table: "Attractions",
                newName: "Address");

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Attractions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Attractions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PendingApproval");

            migrationBuilder.CreateTable(
                name: "AttractionImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttractionImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttractionImages_Attractions_AttractionId",
                        column: x => x.AttractionId,
                        principalTable: "Attractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExperienceSlots_AvailableCapacity_Valid",
                table: "ExperienceSlots",
                sql: "\"AvailableCapacity\" >= 0 AND \"AvailableCapacity\" <= \"Capacity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttractionSchedules_TimeRange",
                table: "AttractionSchedules",
                sql: "\"IsClosed\" OR (\"OpeningTime\" IS NOT NULL AND \"ClosingTime\" IS NOT NULL AND \"OpeningTime\" < \"ClosingTime\")");

            migrationBuilder.CreateIndex(
                name: "IX_Attractions_CategoryId_Status_IsActive",
                table: "Attractions",
                columns: new[] { "CategoryId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Attractions_ProviderId_Status",
                table: "Attractions",
                columns: new[] { "ProviderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttractionImages_AttractionId_SortOrder",
                table: "AttractionImages",
                columns: new[] { "AttractionId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Users_TouristId",
                table: "Favorites",
                column: "TouristId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_Users_TouristId",
                table: "Favorites");

            migrationBuilder.DropTable(
                name: "AttractionImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExperienceSlots_AvailableCapacity_Valid",
                table: "ExperienceSlots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttractionSchedules_TimeRange",
                table: "AttractionSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Attractions_CategoryId_Status_IsActive",
                table: "Attractions");

            migrationBuilder.DropIndex(
                name: "IX_Attractions_ProviderId_Status",
                table: "Attractions");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Attractions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Attractions");

            migrationBuilder.RenameColumn(
                name: "TouristId",
                table: "Favorites",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Favorites_TouristId_AttractionId",
                table: "Favorites",
                newName: "IX_Favorites_UserId_AttractionId");

            migrationBuilder.RenameColumn(
                name: "AvailableCapacity",
                table: "ExperienceSlots",
                newName: "ReservedCount");

            migrationBuilder.RenameColumn(
                name: "OpeningTime",
                table: "AttractionSchedules",
                newName: "OpenTime");

            migrationBuilder.RenameColumn(
                name: "ClosingTime",
                table: "AttractionSchedules",
                newName: "CloseTime");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "Attractions",
                newName: "Location");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "ExperienceSlots",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Attractions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Attractions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExperienceSlots_Price_NonNegative",
                table: "ExperienceSlots",
                sql: "\"Price\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExperienceSlots_ReservedCount_Valid",
                table: "ExperienceSlots",
                sql: "\"ReservedCount\" >= 0 AND \"ReservedCount\" <= \"Capacity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttractionSchedules_TimeRange",
                table: "AttractionSchedules",
                sql: "\"IsClosed\" OR \"OpenTime\" < \"CloseTime\"");

            migrationBuilder.CreateIndex(
                name: "IX_Attractions_CategoryId_IsApproved_IsActive",
                table: "Attractions",
                columns: new[] { "CategoryId", "IsApproved", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Attractions_ProviderId_IsApproved",
                table: "Attractions",
                columns: new[] { "ProviderId", "IsApproved" });

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Users_UserId",
                table: "Favorites",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
