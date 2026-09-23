using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Attractions;

public class CreateAttractionRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string District { get; set; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 1)]
    public string Address { get; set; } = string.Empty;

    [Range(-90, 90)]
    public decimal Latitude { get; set; }

    [Range(-180, 180)]
    public decimal Longitude { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Price { get; set; }
}
