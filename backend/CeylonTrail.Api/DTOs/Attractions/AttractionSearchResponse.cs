namespace CeylonTrail.Api.DTOs.Attractions;

public sealed record AttractionSearchResponse(
    IReadOnlyList<AttractionResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
