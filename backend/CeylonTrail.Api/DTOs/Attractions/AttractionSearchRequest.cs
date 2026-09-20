using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Attractions;

public sealed class AttractionSearchRequest : IValidatableObject
{
    [StringLength(200)]
    public string? Keyword { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    public Guid? CategoryId { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? MinPrice { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? MaxPrice { get; set; }

    public DateOnly? Date { get; set; }

    public string Sort { get; set; } = "name_asc";

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)
        {
            yield return new ValidationResult(
                "MinPrice must be less than or equal to MaxPrice.",
                new[] { nameof(MinPrice), nameof(MaxPrice) });
        }
    }
}
