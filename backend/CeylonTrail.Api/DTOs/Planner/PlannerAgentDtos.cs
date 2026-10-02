namespace CeylonTrail.Api.DTOs.Planner;

public sealed record AgentTraceStep(
    int Sequence,
    string Tool,
    string Purpose,
    string Status,
    string ResultSummary,
    int? DurationMs);

public sealed record AgentExecutionTrace(
    string Agent,
    string Responsibility,
    string InputSummary,
    IReadOnlyList<AgentTraceStep> Steps,
    string? Decision,
    string? Validation,
    string? OutputSummary,
    string? SafeFailure,
    int? DurationMs);

public sealed record PlannerAgentRequest(
    string TripId,
    DateOnly StartDate,
    DateOnly EndDate,
    int Duration,
    decimal Budget,
    IReadOnlyList<string> Interests,
    IReadOnlyList<string> PreferredRegions,
    IReadOnlyList<PlannerPreference> Preferences,
    IReadOnlyList<PlannerCandidateAttraction> CandidateAttractions);

public sealed record PlannerPreference(string Type, string Value);

public sealed record PlannerCandidateAttraction(
    string Id,
    string Name,
    string? Category,
    string? Region,
    decimal Price,
    string? Description,
    string? OpeningInformation);

public sealed record PlannerAgentResponse(
    IReadOnlyList<PlannerDay> Days,
    decimal EstimatedCost,
    string Status,
    string? Message,
    AgentExecutionTrace? Trace = null);

public sealed record PlannerDay(
    int DayNumber,
    DateOnly Date,
    IReadOnlyList<PlannerItem> Items);

public sealed record PlannerItem(
    Guid AttractionId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal EstimatedCost,
    string? Notes);
