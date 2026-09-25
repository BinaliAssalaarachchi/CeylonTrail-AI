using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeylonTrail.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentWorkflowPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CurrentStage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentWorkflows_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentWorkflows_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkflowStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentWorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    OutputSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ValidationResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    TravelIntelligenceExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflowStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentWorkflowStages_AgentWorkflows_AgentWorkflowId",
                        column: x => x.AgentWorkflowId,
                        principalTable: "AgentWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_RequestedByUserId_UpdatedAt",
                table: "AgentWorkflows",
                columns: new[] { "RequestedByUserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_TripId_StartedAt",
                table: "AgentWorkflows",
                columns: new[] { "TripId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_Status_UpdatedAt",
                table: "AgentWorkflows",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_WorkflowId",
                table: "AgentWorkflows",
                column: "WorkflowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowStages_AgentWorkflowId_AgentRole_AttemptNumber",
                table: "AgentWorkflowStages",
                columns: new[] { "AgentWorkflowId", "AgentRole", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowStages_AgentWorkflowId_Sequence_AttemptNumber",
                table: "AgentWorkflowStages",
                columns: new[] { "AgentWorkflowId", "Sequence", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowStages_ApprovalRequestId",
                table: "AgentWorkflowStages",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowStages_TravelIntelligenceExecutionId",
                table: "AgentWorkflowStages",
                column: "TravelIntelligenceExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowStages_ValidationResultId",
                table: "AgentWorkflowStages",
                column: "ValidationResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentWorkflowStages");

            migrationBuilder.DropTable(
                name: "AgentWorkflows");
        }
    }
}
