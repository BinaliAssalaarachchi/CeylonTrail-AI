using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Trips;

public sealed class AddTripPreferenceRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string PreferenceType { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Value { get; set; } = string.Empty;
}
