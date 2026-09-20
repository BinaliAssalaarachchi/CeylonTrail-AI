using CeylonTrail.Api.DTOs.Attractions;

namespace CeylonTrail.Api.Interfaces;

public interface IAttractionService
{
    Task<ServiceResult<AttractionResponse>> CreateAsync(
        CreateAttractionRequest request,
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AttractionSearchResponse>> SearchAsync(
        AttractionSearchRequest request,
        Guid? viewerId = null,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AttractionResponse>> GetByIdAsync(
        Guid attractionId,
        Guid? viewerId = null,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AttractionResponse>> ApproveAsync(
        Guid attractionId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AttractionResponse>> UpdateAsync(
        Guid attractionId,
        UpdateAttractionRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeleteAsync(
        Guid attractionId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AttractionScheduleResponse>> AddScheduleAsync(
        Guid attractionId,
        CreateScheduleRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ExperienceSlotResponse>> AddSlotAsync(
        Guid attractionId,
        CreateExperienceSlotRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AvailabilityResponse>> GetAvailabilityAsync(
        Guid attractionId,
        DateOnly? date,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FavoriteResponse>> AddFavoriteAsync(
        Guid attractionId,
        Guid touristId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> RemoveFavoriteAsync(
        Guid attractionId,
        Guid touristId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AttractionImageResponse>> AddImageAsync(
        Guid attractionId,
        AttractionImageRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> RemoveImageAsync(
        Guid attractionId,
        Guid imageId,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
