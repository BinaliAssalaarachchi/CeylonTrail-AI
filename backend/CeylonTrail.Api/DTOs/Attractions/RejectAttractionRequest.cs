using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Attractions;

public sealed class RejectAttractionRequest
{
    [Required(ErrorMessage = "A rejection reason is required.")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "Rejection reason must be between 3 and 1000 characters.")]
    public string Reason { get; set; } = string.Empty;
}
