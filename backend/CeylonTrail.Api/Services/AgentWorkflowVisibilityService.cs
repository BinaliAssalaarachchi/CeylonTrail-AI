using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.AgentWorkflows;
using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class AgentWorkflowVisibilityService(ApplicationDbContext dbContext) : IAgentWorkflowVisibilityService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<TouristAgentWorkflowResponse?> GetLatestForTouristAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await QueryWorkflows()
            .Where(candidate => candidate.TripId == tripId && candidate.RequestedByUserId == touristId)
            .OrderByDescending(candidate => candidate.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return workflow is null ? null : await BuildResponseAsync(workflow, staff: false, cancellationToken);
    }

    public async Task<StaffAgentWorkflowResponse?> GetForStaffAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await QueryWorkflows()
            .SingleOrDefaultAsync(candidate => candidate.WorkflowId == workflowId, cancellationToken);
        return workflow is null
            ? null
            : (StaffAgentWorkflowResponse?)await BuildResponseAsync(workflow, staff: true, cancellationToken);
    }

    public async Task ApplyApprovalExecutionOutcomeAsync(
        Guid approvalRequestId,
        ApprovalRequestResponse response,
        CancellationToken cancellationToken = default)
    {
        var stage = await dbContext.AgentWorkflowStages
            .AsNoTracking()
            .Where(candidate => candidate.ApprovalRequestId == approvalRequestId &&
                                candidate.AgentRole == AgentWorkflowAgentRole.TravelIntelligence)
            .Select(candidate => new { candidate.AgentWorkflowId, WorkflowId = candidate.AgentWorkflow.WorkflowId })
            .SingleOrDefaultAsync(cancellationToken);
        response.AgentWorkflowId = stage?.WorkflowId;
        if (stage is null)
        {
            return;
        }

        var bookingStage = await dbContext.AgentWorkflowStages
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AgentWorkflowId == stage.AgentWorkflowId && candidate.AgentRole == AgentWorkflowAgentRole.BookingAction, cancellationToken);
        if (bookingStage is null)
        {
            return;
        }

        var audit = ReadExecutionAudit(bookingStage.OutputSnapshotJson);
        if (audit is null)
        {
            return;
        }

        response.ExecutionSucceeded = audit.Succeeded;
        response.BookingId = audit.BookingId;
        response.ExecutionMessage = audit.Error ?? (audit.Succeeded ? "Approved booking executed." : "Approved decision recorded, but booking execution failed safely.");
    }

    private IQueryable<AgentWorkflow> QueryWorkflows() => dbContext.AgentWorkflows
        .AsNoTracking()
        .Include(workflow => workflow.Trip).ThenInclude(trip => trip.Preferences)
        .Include(workflow => workflow.RequestedByUser)
        .Include(workflow => workflow.Stages.OrderBy(stage => stage.Sequence).ThenBy(stage => stage.AttemptNumber));

    private async Task<TouristAgentWorkflowResponse> BuildResponseAsync(
        AgentWorkflow workflow,
        bool staff,
        CancellationToken cancellationToken)
    {
        var approvalStage = workflow.Stages.FirstOrDefault(stage => stage.AgentRole == AgentWorkflowAgentRole.TravelIntelligence);
        var bookingStage = workflow.Stages.FirstOrDefault(stage => stage.AgentRole == AgentWorkflowAgentRole.BookingAction);
        var approval = approvalStage?.ApprovalRequestId is Guid approvalId
            ? await dbContext.ApprovalRequests.AsNoTracking().Include(request => request.Decision).SingleOrDefaultAsync(request => request.Id == approvalId, cancellationToken)
            : null;
        var execution = approvalStage?.TravelIntelligenceExecutionId is Guid executionId
            ? await dbContext.TravelIntelligenceExecutions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == executionId, cancellationToken)
            : null;
        var audit = ReadExecutionAudit(bookingStage?.OutputSnapshotJson);
        var response = new TouristAgentWorkflowResponse
        {
            WorkflowId = workflow.WorkflowId,
            TripId = workflow.TripId,
            TripName = workflow.Trip?.Name,
            DestinationName = workflow.Trip?.Preferences?.FirstOrDefault(p => p.PreferenceType == "Region" || p.PreferenceType == "Destination" || p.PreferenceType == "District")?.Value,
            Status = workflow.Status,
            StartedAt = workflow.StartedAt,
            CompletedAt = workflow.CompletedAt,
            RequiresApproval = approval is not null || ReadRequiresApproval(approvalStage?.OutputSnapshotJson),
            ReviewStatus = approval?.Status,
            ExecutionSucceeded = audit?.Succeeded,
            BookingId = audit?.BookingId,
            SafeMessage = SafeMessage(workflow, audit),
            Stages = workflow.Stages.OrderBy(stage => stage.Sequence).Select(ToStage).ToList()
        };

        if (!staff)
        {
            return response;
        }

        return new StaffAgentWorkflowResponse
        {
            WorkflowId = response.WorkflowId,
            TripId = response.TripId,
            TripName = response.TripName,
            DestinationName = response.DestinationName,
            Status = response.Status,
            StartedAt = response.StartedAt,
            CompletedAt = response.CompletedAt,
            RequiresApproval = response.RequiresApproval,
            ReviewStatus = response.ReviewStatus,
            ExecutionSucceeded = response.ExecutionSucceeded,
            BookingId = response.BookingId,
            SafeMessage = response.SafeMessage,
            Stages = response.Stages,
            RequestedByUserId = workflow.RequestedByUserId,
            TouristName = workflow.RequestedByUser is null ? null : $"{workflow.RequestedByUser.FirstName} {workflow.RequestedByUser.LastName}".Trim(),
            TouristEmail = workflow.RequestedByUser?.Email,
            RiskLevel = execution?.RiskLevel,
            IsFeasible = execution?.IsFeasible,
            RecommendedAction = execution?.RecommendedAction,
            ApprovalRequestId = approval?.Id,
            DecidedByUserId = approval?.Decision?.DecidedByUserId,
            DecidedAt = approval?.Decision?.DecidedAt,
            ExecutionMessage = audit?.Error ?? (audit?.Succeeded == true ? "Approved booking executed." : null),
            BookingProposals = ReadProposals(bookingStage?.OutputSnapshotJson)
        };
    }

    private static AgentWorkflowStageSummaryResponse ToStage(AgentWorkflowStage stage) => new()
    {
        Sequence = stage.Sequence,
        AgentRole = stage.AgentRole,
        Status = stage.Status,
        StartedAt = stage.StartedAt,
        CompletedAt = stage.CompletedAt,
        Summary = ReadStageSummary(stage)
    };

    private static string ReadStageSummary(AgentWorkflowStage stage)
    {
        if (!string.IsNullOrWhiteSpace(stage.ErrorSummary))
        {
            return stage.ErrorSummary;
        }

        try
        {
            using var document = JsonDocument.Parse(stage.OutputSnapshotJson ?? "{}");
            var root = document.RootElement;
            return stage.AgentRole switch
            {
                AgentWorkflowAgentRole.Planner => $"Planner produced {(root.TryGetProperty("dayCount", out var days) ? days.GetInt32() : 0)} itinerary day(s).",
                AgentWorkflowAgentRole.Destination => $"Destination recommendations grounded the itinerary.",
                AgentWorkflowAgentRole.BookingAction => root.TryGetProperty("proposals", out var proposals) ? $"Booking proposal contains {proposals.GetArrayLength()} option(s)." : "Booking proposal prepared.",
                AgentWorkflowAgentRole.TravelIntelligence => root.TryGetProperty("requiresApproval", out var approval) && approval.GetBoolean() ? "Travel safety assessment requires coordinator review." : "Travel safety assessment completed.",
                _ => string.Empty
            };
        }
        catch (JsonException)
        {
            return "Stage details are unavailable.";
        }
    }

    private static bool ReadRequiresApproval(string? snapshot)
    {
        try
        {
            using var document = JsonDocument.Parse(snapshot ?? "{}");
            return document.RootElement.TryGetProperty("requiresApproval", out var value) && value.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<AgentWorkflowBookingProposalResponse> ReadProposals(string? snapshot)
    {
        try
        {
            using var document = JsonDocument.Parse(snapshot ?? "{}");
            return document.RootElement.TryGetProperty("proposals", out var proposals)
                ? JsonSerializer.Deserialize<List<AgentWorkflowBookingProposalResponse>>(proposals.GetRawText(), JsonOptions) ?? []
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ExecutionAudit? ReadExecutionAudit(string? snapshot)
    {
        try
        {
            using var document = JsonDocument.Parse(snapshot ?? "{}");
            if (!document.RootElement.TryGetProperty("execution", out var execution)) return null;
            return JsonSerializer.Deserialize<ExecutionAudit>(execution.GetRawText(), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SafeMessage(AgentWorkflow workflow, ExecutionAudit? audit) =>
        audit?.Error ?? (audit?.Succeeded == true ? "Approved booking executed successfully." : workflow.FailureSummary ?? workflow.Status switch
        {
            AgentWorkflowStatus.AwaitingApproval => "Coordinator review is pending.",
            AgentWorkflowStatus.Completed => "Workflow completed successfully.",
            AgentWorkflowStatus.Cancelled => "Workflow was rejected.",
            AgentWorkflowStatus.FailedSafe => "Workflow failed safely. Please review and try again.",
            AgentWorkflowStatus.Running => "Workflow is processing.",
            _ => "Workflow is ready to process."
        });

    private sealed class ExecutionAudit
    {
        public bool Succeeded { get; set; }
        public Guid? BookingId { get; set; }
        public string? Error { get; set; }
    }
}
