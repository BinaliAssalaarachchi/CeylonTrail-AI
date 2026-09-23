using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeylonTrail.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddItineraryValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValidationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OverallStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsFeasible = table.Column<bool>(type: "boolean", nullable: false),
                    TotalIssueCount = table.Column<int>(type: "integer", nullable: false),
                    BlockingIssueCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationResults_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ValidationIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidationResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    RelatedDistrict = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RelatedItemReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationIssues_ValidationResults_ValidationResultId",
                        column: x => x.ValidationResultId,
                        principalTable: "ValidationResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValidationIssues_IssueType_Severity",
                table: "ValidationIssues",
                columns: new[] { "IssueType", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_ValidationIssues_ValidationResultId",
                table: "ValidationIssues",
                column: "ValidationResultId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationResults_CreatedAt",
                table: "ValidationResults",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationResults_CreatedByUserId",
                table: "ValidationResults",
                column: "CreatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValidationIssues");

            migrationBuilder.DropTable(
                name: "ValidationResults");
        }
    }
}
