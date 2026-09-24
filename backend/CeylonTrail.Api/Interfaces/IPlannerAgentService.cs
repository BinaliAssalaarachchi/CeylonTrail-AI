using CeylonTrail.Api.DTOs.Planner;

namespace CeylonTrail.Api.Interfaces;

public sealed record PlannerAgentServiceResult(
    PlannerAgentResponse? Value = null,
    string? Error = null,
    bool ServiceUnavailable = false)
{
    public bool Succeeded => Value is not null && Error is null;
}

public interface IPlannerAgentService
{
    Task<PlannerAgentServiceResult> GenerateAsync(
        PlannerAgentRequest request,
        CancellationToken cancellationToken = default);
}
