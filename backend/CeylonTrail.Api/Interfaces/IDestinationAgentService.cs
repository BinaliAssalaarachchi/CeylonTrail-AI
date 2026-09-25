using CeylonTrail.Api.DTOs.Destination;

namespace CeylonTrail.Api.Interfaces;

public sealed record DestinationAgentServiceResult(
    DestinationAgentResponse? Value = null,
    string? Error = null,
    bool ServiceUnavailable = false);

public interface IDestinationAgentService
{
    Task<DestinationAgentServiceResult> SelectAsync(
        DestinationAgentRequest request,
        CancellationToken cancellationToken = default);
}
