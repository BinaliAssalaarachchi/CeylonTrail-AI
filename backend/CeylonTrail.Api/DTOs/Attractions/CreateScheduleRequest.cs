using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Attractions;

public sealed class CreateScheduleRequest : IValidatableObject
{
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly? OpeningTime { get; set; }

    public TimeOnly? ClosingTime { get; set; }

    public bool IsClosed { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsClosed && (!OpeningTime.HasValue || !ClosingTime.HasValue || OpeningTime >= ClosingTime))
        {
            yield return new ValidationResult(
                "An open schedule requires opening and closing times, with opening time before closing time.",
                new[] { nameof(OpeningTime), nameof(ClosingTime) });
        }
    }
}
