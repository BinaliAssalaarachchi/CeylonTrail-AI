using CeylonTrail.Api.DTOs.Attractions;

namespace CeylonTrail.Api.Interfaces;

public sealed record DestinationAgentServiceResult(
    DestinationAgentResponse? Value = null,
    string? Error = null,
    bool ServiceUnavailable = false)
{
    public bool Succeeded => Value is not null && Error is null;
}

public interface IDestinationAgentService
{
    Task<DestinationAgentServiceResult> RecommendAsync(
        DestinationRecommendationRequest request,
        CancellationToken cancellationToken = default);
}
