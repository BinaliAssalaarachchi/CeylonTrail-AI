using CeylonTrail.Api.DTOs.TravelIntelligence;

namespace CeylonTrail.Api.Interfaces;

public interface ITravelIntelligenceExecutionQueryService
{
    Task<TravelIntelligenceExecutionPageResponse?> ListForOwnerAsync(Guid validationResultId, Guid ownerUserId, TravelIntelligenceExecutionQuery query, CancellationToken cancellationToken = default);
    Task<TravelIntelligenceExecutionDetailResponse?> GetForOwnerAsync(Guid validationResultId, Guid executionId, Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<TravelIntelligenceExecutionPageResponse> ListForStaffAsync(TravelIntelligenceExecutionQuery query, CancellationToken cancellationToken = default);
    Task<TravelIntelligenceExecutionDetailResponse?> GetForStaffAsync(Guid executionId, CancellationToken cancellationToken = default);
}
