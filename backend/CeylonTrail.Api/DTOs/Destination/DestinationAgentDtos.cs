namespace CeylonTrail.Api.DTOs.Destination;

public sealed record DestinationActivityRequirement(
    string Reference,
    string? ActivityType,
    string? PreferredDistrict,
    IReadOnlyList<string> PreferredCategories,
    decimal? MaxCost);

public sealed record DestinationCandidateAttraction(
    Guid AttractionId,
    string Name,
    string? Category,
    string? District,
    string? DescriptionSummary,
    decimal Price,
    string? OpeningInformation);

public sealed record DestinationAgentRequest(
    Guid WorkflowId,
    Guid TripId,
    DateOnly StartDate,
    DateOnly EndDate,
    int Duration,
    decimal Budget,
    IReadOnlyList<string> Interests,
    IReadOnlyList<string> PreferredRegions,
    IReadOnlyList<DestinationActivityRequirement> Requirements,
    IReadOnlyList<DestinationCandidateAttraction> CandidateAttractions);

public sealed record DestinationSelection(
    string RequirementReference,
    Guid AttractionId,
    IReadOnlyList<string> FitReasons,
    string Explanation);

public sealed record DestinationAgentResponse(
    Guid WorkflowId,
    IReadOnlyList<DestinationSelection> Selections,
    IReadOnlyList<string> UnmatchedRequirements,
    string Status,
    string Summary);
