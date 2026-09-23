using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Attractions;

public sealed class AttractionImageRequest
{
    [Required, Url, StringLength(2048)]
    public string ImageUrl { get; set; } = string.Empty;

    [StringLength(300)]
    public string? AltText { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }
}
