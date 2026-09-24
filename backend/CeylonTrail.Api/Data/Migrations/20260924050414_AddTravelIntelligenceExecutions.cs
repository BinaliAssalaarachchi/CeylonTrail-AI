using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeylonTrail.Api.Data.Migrations
{
    public partial class AddTravelIntelligenceExecutions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TravelIntelligenceExecutionId",
                table: "ApprovalRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TravelIntelligenceExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValidationResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AgentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ObjectiveName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObjectiveDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ObjectiveSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExecutionStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsFeasible = table.Column<bool>(type: "boolean", nullable: false),
                    RecommendedAction = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequiresHumanApproval = table.Column<bool>(type: "boolean", nullable: false),
                    Provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ProviderAttempted = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                    UsedFallback = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ProviderLatencyMs = table.Column<int>(type: "integer", nullable: true),
                    ProviderAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ToolSelectionProviderAttempted = table.Column<bool>(type: "boolean", nullable: false),
                    SelectedToolNamesJson = table.Column<string>(type: "jsonb", nullable: false),
                    RejectedToolNamesJson = table.Column<string>(type: "jsonb", nullable: false),
                    ToolSelectionFallbackUsed = table.Column<bool>(type: "boolean", nullable: false),
                    ToolSelectionFallbackReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SelectionAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    RecommendationsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AffectedItemsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AlternativesJson = table.Column<string>(type: "jsonb", nullable: false),
                    SafeWindowsJson = table.Column<string>(type: "jsonb", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    ResultSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelIntelligenceExecutions", x => x.Id);
                    table.ForeignKey("FK_TravelIntelligenceExecutions_Users_RequestedByUserId", x => x.RequestedByUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_TravelIntelligenceExecutions_ValidationResults_ValidationResultId", x => x.ValidationResultId, "ValidationResults", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TravelIntelligenceExecutionSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelIntelligenceExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StepId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PlannedToolName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ExecutedToolName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    ResultSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelIntelligenceExecutionSteps", x => x.Id);
                    table.ForeignKey("FK_TravelIntelligenceExecutionSteps_TravelIntelligenceExecutions_TravelIntelligenceExecutionId", x => x.TravelIntelligenceExecutionId, "TravelIntelligenceExecutions", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_ApprovalRequests_TravelIntelligenceExecutionId", "ApprovalRequests", "TravelIntelligenceExecutionId", unique: true);
            migrationBuilder.CreateIndex("IX_TravelIntelligenceExecutions_WorkflowId", "TravelIntelligenceExecutions", "WorkflowId", unique: true);
            migrationBuilder.CreateIndex("IX_TravelIntelligenceExecutions_ValidationResultId_StartedAt", "TravelIntelligenceExecutions", new[] { "ValidationResultId", "StartedAt" });
            migrationBuilder.CreateIndex("IX_TravelIntelligenceExecutions_RequestedByUserId_StartedAt", "TravelIntelligenceExecutions", new[] { "RequestedByUserId", "StartedAt" });
            migrationBuilder.CreateIndex("IX_TravelIntelligenceExecutions_ExecutionStatus_StartedAt", "TravelIntelligenceExecutions", new[] { "ExecutionStatus", "StartedAt" });
            migrationBuilder.CreateIndex("IX_TravelIntelligenceExecutionSteps_TravelIntelligenceExecutionId_Sequence", "TravelIntelligenceExecutionSteps", new[] { "TravelIntelligenceExecutionId", "Sequence" }, unique: true);
            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRequests_TravelIntelligenceExecutions_TravelIntelligenceExecutionId",
                table: "ApprovalRequests",
                column: "TravelIntelligenceExecutionId",
                principalTable: "TravelIntelligenceExecutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRequests_TravelIntelligenceExecutions_TravelIntelligenceExecutionId",
                table: "ApprovalRequests");
            migrationBuilder.DropTable("TravelIntelligenceExecutionSteps");
            migrationBuilder.DropTable("TravelIntelligenceExecutions");
            migrationBuilder.DropIndex("IX_ApprovalRequests_TravelIntelligenceExecutionId", "ApprovalRequests");
            migrationBuilder.DropColumn("TravelIntelligenceExecutionId", "ApprovalRequests");
        }
    }
}
