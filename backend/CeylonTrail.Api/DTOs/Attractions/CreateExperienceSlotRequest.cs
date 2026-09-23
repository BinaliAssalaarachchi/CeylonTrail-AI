using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Attractions;

public sealed class CreateExperienceSlotRequest : IValidatableObject
{
    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    [Range(1, int.MaxValue)]
    public int Capacity { get; set; }

    [Range(0, int.MaxValue)]
    public int AvailableCapacity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime >= EndTime)
        {
            yield return new ValidationResult(
                "StartTime must be before EndTime.",
                new[] { nameof(StartTime), nameof(EndTime) });
        }

        if (AvailableCapacity > Capacity)
        {
            yield return new ValidationResult(
                "AvailableCapacity cannot exceed Capacity.",
                new[] { nameof(AvailableCapacity), nameof(Capacity) });
        }
    }
}
