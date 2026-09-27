using System.ComponentModel.DataAnnotations;
using CeylonTrail.Api.Models;
using DataValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace CeylonTrail.Api.DTOs.Trips;

public sealed class StaffTripQuery : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [StringLength(150)]
    public string? Search { get; set; }

    public TripStatus? Status { get; set; }

    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public string SortBy { get; set; } = "updatedAt";

    public string SortDirection { get; set; } = "desc";

    public IEnumerable<DataValidationResult> Validate(ValidationContext validationContext)
    {
        var sortFields = new[] { "name", "startDate", "endDate", "status", "createdAt", "updatedAt" };
        if (!sortFields.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
        {
            yield return new DataValidationResult("SortBy is not supported.", new[] { nameof(SortBy) });
        }

        if (!string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            yield return new DataValidationResult("SortDirection must be asc or desc.", new[] { nameof(SortDirection) });
        }

        if (DateFrom.HasValue && DateTo.HasValue && DateFrom > DateTo)
        {
            yield return new DataValidationResult("DateFrom must be before or equal to DateTo.", new[] { nameof(DateFrom), nameof(DateTo) });
        }
    }
}
