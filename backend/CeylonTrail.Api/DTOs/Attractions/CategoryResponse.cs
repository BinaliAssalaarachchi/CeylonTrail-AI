namespace CeylonTrail.Api.DTOs.Attractions;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description);
