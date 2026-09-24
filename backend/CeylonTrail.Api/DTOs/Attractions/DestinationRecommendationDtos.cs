using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Attractions;

public sealed class DestinationRecommendationRequest : IValidatableObject
{
    [StringLength(100)] public string? District { get; set; }
    public IReadOnlyList<string> Interests { get; set; } = Array.Empty<string>();
    public IReadOnlyList<Guid> CategoryIds { get; set; } = Array.Empty<Guid>();
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal? MaxBudget { get; set; }
    public DateOnly? Date { get; set; }
    [Range(1, 50)] public int Limit { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Interests.Any(string.IsNullOrWhiteSpace))
            yield return new ValidationResult("Interests must not contain empty values.", new[] { nameof(Interests) });
        if (CategoryIds.Count != CategoryIds.Distinct().Count())
            yield return new ValidationResult("CategoryIds must not contain duplicates.", new[] { nameof(CategoryIds) });
    }
}

public sealed record DestinationTrustedSchedule(
    DayOfWeek DayOfWeek,
    TimeOnly? OpeningTime,
    TimeOnly? ClosingTime,
    bool IsClosed);

public sealed record DestinationTrustedSlot(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int Capacity,
    int AvailableCapacity);

public sealed record DestinationTrustedAttraction(
    Guid AttractionId,
    string Name,
    string District,
    Guid CategoryId,
    string Category,
    decimal Price,
    string Status,
    bool IsActive,
    IReadOnlyList<DestinationTrustedSchedule> Schedules,
    IReadOnlyList<DestinationTrustedSlot> ExperienceSlots);

public sealed record DestinationAgentRequest(
    DestinationRecommendationRequest Request,
    IReadOnlyList<DestinationTrustedAttraction> TrustedAttractions);

public sealed record DestinationCandidateResponse(
    Guid AttractionId,
    string Name,
    string District,
    Guid CategoryId,
    string Category,
    decimal Price,
    IReadOnlyList<DestinationTrustedSchedule> OpeningHours,
    IReadOnlyList<DestinationTrustedSlot> Availability,
    IReadOnlyList<string> MatchReasons,
    int Score);

public sealed record DestinationAgentResponse(
    IReadOnlyList<DestinationCandidateResponse> Candidates,
    string Status,
    string? Message);
