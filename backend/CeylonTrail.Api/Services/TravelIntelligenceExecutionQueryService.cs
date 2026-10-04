using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.DTOs.Planner;
using CeylonTrail.Api.DTOs.TravelIntelligence;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class TravelIntelligenceExecutionQueryService(ApplicationDbContext dbContext, ILogger<TravelIntelligenceExecutionQueryService> logger) : ITravelIntelligenceExecutionQueryService
{
    private const int MaxPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<TravelIntelligenceExecutionPageResponse?> ListForOwnerAsync(Guid validationResultId, Guid ownerUserId, TravelIntelligenceExecutionQuery query, CancellationToken cancellationToken = default)
    {
        var ownsValidation = await dbContext.ValidationResults.AsNoTracking().AnyAsync(result => result.Id == validationResultId && result.CreatedByUserId == ownerUserId, cancellationToken);
        if (!ownsValidation) return null;
        var executions = ApplyFilters(dbContext.TravelIntelligenceExecutions.AsNoTracking(), query).Where(execution => execution.ValidationResultId == validationResultId && execution.RequestedByUserId == ownerUserId);
        return await BuildPageAsync(executions, query, cancellationToken);
    }

    public async Task<TravelIntelligenceExecutionDetailResponse?> GetForOwnerAsync(Guid validationResultId, Guid executionId, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var execution = await DetailQuery().SingleOrDefaultAsync(item => item.Id == executionId && item.ValidationResultId == validationResultId && item.RequestedByUserId == ownerUserId, cancellationToken);
        return execution is null ? null : ToDetail(execution);
    }

    public async Task<TravelIntelligenceExecutionPageResponse> ListForStaffAsync(TravelIntelligenceExecutionQuery query, CancellationToken cancellationToken = default) =>
        await BuildPageAsync(ApplyFilters(dbContext.TravelIntelligenceExecutions.AsNoTracking(), query), query, cancellationToken);

    public async Task<TravelIntelligenceExecutionDetailResponse?> GetForStaffAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var execution = await DetailQuery().SingleOrDefaultAsync(item => item.Id == executionId, cancellationToken);
        return execution is null ? null : ToDetail(execution);
    }

    private IQueryable<TravelIntelligenceExecution> DetailQuery() => dbContext.TravelIntelligenceExecutions.AsNoTracking()
        .Include(execution => execution.Steps)
        .Include(execution => execution.ApprovalRequest).ThenInclude(approval => approval!.Decision)
        .Include(execution => execution.ApprovalRequest).ThenInclude(approval => approval!.ValidationResult);

    private async Task<TravelIntelligenceExecutionPageResponse> BuildPageAsync(IQueryable<TravelIntelligenceExecution> query, TravelIntelligenceExecutionQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, MaxPageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Include(execution => execution.ApprovalRequest).ThenInclude(approval => approval!.Decision)
            .Include(execution => execution.ApprovalRequest).ThenInclude(approval => approval!.ValidationResult)
            .OrderByDescending(execution => execution.StartedAt).ThenByDescending(execution => execution.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new TravelIntelligenceExecutionPageResponse
        {
            Items = items.Select(ToListItem).ToList(), Page = page, PageSize = pageSize, TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    private static IQueryable<TravelIntelligenceExecution> ApplyFilters(IQueryable<TravelIntelligenceExecution> query, TravelIntelligenceExecutionQuery request)
    {
        if (request.ValidationResultId.HasValue) query = query.Where(execution => execution.ValidationResultId == request.ValidationResultId.Value);
        if (!string.IsNullOrWhiteSpace(request.ExecutionStatus)) query = query.Where(execution => execution.ExecutionStatus == request.ExecutionStatus);
        if (request.RiskLevel.HasValue) query = query.Where(execution => execution.RiskLevel == request.RiskLevel.Value);
        if (request.RequiresHumanApproval.HasValue) query = query.Where(execution => execution.RequiresHumanApproval == request.RequiresHumanApproval.Value);
        if (request.UsedFallback.HasValue) query = query.Where(execution => execution.UsedFallback == request.UsedFallback.Value);
        return query;
    }

    private TravelIntelligenceExecutionDetailResponse ToDetail(TravelIntelligenceExecution execution)
    {
        var detail = new TravelIntelligenceExecutionDetailResponse
        {
            ExecutionId = execution.Id, WorkflowId = execution.WorkflowId, ValidationResultId = execution.ValidationResultId,
            AgentName = execution.AgentName, AgentVersion = execution.AgentVersion, ExecutionStatus = execution.ExecutionStatus,
            RiskLevel = execution.RiskLevel, IsFeasible = execution.IsFeasible, RecommendedAction = execution.RecommendedAction,
            Summary = execution.Summary, RequiresHumanApproval = execution.RequiresHumanApproval, Provider = execution.Provider,
            ProviderName = execution.ProviderName, ModelName = execution.ModelName, ProviderAttempted = execution.ProviderAttempted,
            ProviderSucceeded = execution.ProviderSucceeded, UsedFallback = execution.UsedFallback, FallbackReason = execution.FallbackReason,
            ProviderLatencyMs = execution.ProviderLatencyMs, ProviderAttemptCount = execution.ProviderAttemptCount,
            StartedAt = execution.StartedAt, CompletedAt = execution.CompletedAt, DurationMs = execution.DurationMs,
            ResultSummary = execution.ResultSummary, ObjectiveName = execution.ObjectiveName, ObjectiveDescription = execution.ObjectiveDescription,
            ObjectiveSource = execution.ObjectiveSource, ToolSelectionProviderAttempted = execution.ToolSelectionProviderAttempted,
            ToolSelectionFallbackUsed = execution.ToolSelectionFallbackUsed, ToolSelectionFallbackReason = execution.ToolSelectionFallbackReason,
            SelectionAttemptCount = execution.SelectionAttemptCount, Steps = execution.Steps.OrderBy(step => step.Sequence).Select(ToStep).ToList(),
            SharedTrace = ToSharedTrace(execution),
            Approval = execution.ApprovalRequest is null ? null : ApprovalRequestService.ToResponseForRead(execution.ApprovalRequest)
        };
        detail.SelectedToolNames = ReadJson(execution.SelectedToolNamesJson, Array.Empty<string>(), "selected tools", execution.Id).ToList();
        detail.RejectedToolNames = ReadJson(execution.RejectedToolNamesJson, Array.Empty<string>(), "rejected tools", execution.Id).ToList();
        detail.Recommendations = ReadJson(execution.RecommendationsJson, Array.Empty<TravelIntelligenceRecommendation>(), "recommendations", execution.Id).ToList();
        detail.AffectedItems = ReadJson(execution.AffectedItemsJson, Array.Empty<TravelIntelligenceAffectedItem>(), "affected items", execution.Id).ToList();
        detail.Alternatives = ReadJson(execution.AlternativesJson, Array.Empty<TravelIntelligenceAlternativeRecommendation>(), "alternatives", execution.Id).ToList();
        detail.SafeWindows = ReadJson(execution.SafeWindowsJson, Array.Empty<TravelIntelligenceSafeWindowSuggestion>(), "safe windows", execution.Id).ToList();
        return detail;
    }

    private static TravelIntelligenceExecutionListItemResponse ToListItem(TravelIntelligenceExecution execution) => new()
    {
        ExecutionId = execution.Id, WorkflowId = execution.WorkflowId, ValidationResultId = execution.ValidationResultId,
        AgentName = execution.AgentName, AgentVersion = execution.AgentVersion, ExecutionStatus = execution.ExecutionStatus,
        RiskLevel = execution.RiskLevel, IsFeasible = execution.IsFeasible, RecommendedAction = execution.RecommendedAction,
        Summary = execution.Summary, RequiresHumanApproval = execution.RequiresHumanApproval, Provider = execution.Provider,
        ProviderName = execution.ProviderName, ModelName = execution.ModelName, ProviderAttempted = execution.ProviderAttempted,
        ProviderSucceeded = execution.ProviderSucceeded, UsedFallback = execution.UsedFallback, FallbackReason = execution.FallbackReason,
        ProviderLatencyMs = execution.ProviderLatencyMs, ProviderAttemptCount = execution.ProviderAttemptCount,
        StartedAt = execution.StartedAt, CompletedAt = execution.CompletedAt, DurationMs = execution.DurationMs,
        Approval = execution.ApprovalRequest is null ? null : ApprovalRequestService.ToResponseForRead(execution.ApprovalRequest)
    };

    private static TravelIntelligenceExecutionStepResponse ToStep(TravelIntelligenceExecutionStep step) => new()
    {
        Sequence = step.Sequence, StepId = step.StepId, Name = step.Name, Purpose = step.Purpose,
        PlannedToolName = step.PlannedToolName, ExecutedToolName = step.ExecutedToolName, Status = step.Status,
        DurationMs = step.DurationMs, ResultSummary = step.ResultSummary
    };

    private static AgentExecutionTrace ToSharedTrace(TravelIntelligenceExecution execution) => new(
        "TravelIntelligence",
        "Analyze validated itinerary/travel conditions using bounded safety and travel-intelligence tools and produce an auditable recommendation for human review.",
        $"Validation {execution.ValidationResultId}; risk {execution.RiskLevel}.",
        execution.Steps
            .Where(step => !string.IsNullOrWhiteSpace(step.ExecutedToolName))
            .OrderBy(step => step.Sequence)
            .Select((step, index) => new AgentTraceStep(
                index + 1,
                step.ExecutedToolName!,
                step.Purpose,
                step.Status.ToString() is "Completed" or "Failed" or "Skipped" ? step.Status.ToString() : "Failed",
                string.IsNullOrWhiteSpace(step.ResultSummary) ? "No operational result summary." : step.ResultSummary[..Math.Min(step.ResultSummary.Length, 500)],
                step.DurationMs))
            .ToList(),
        $"Recommended action: {execution.RecommendedAction}; execution status {execution.ExecutionStatus}.",
        $"Execution status {execution.ExecutionStatus}; authoritative validation state preserved.",
        execution.ResultSummary,
        execution.UsedFallback ? execution.FallbackReason : null,
        execution.DurationMs);

    private T ReadJson<T>(string json, T fallback, string field, Guid executionId)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback; }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Malformed optional {Field} snapshot for Travel Intelligence execution {ExecutionId}.", field, executionId);
            return fallback;
        }
    }
}
