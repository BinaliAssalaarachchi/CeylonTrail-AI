using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Auth;

public class RegisterRequest : IValidatableObject
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FirstName))
            yield return new ValidationResult("First name cannot be blank.", new[] { nameof(FirstName) });
        if (string.IsNullOrWhiteSpace(LastName))
            yield return new ValidationResult("Last name cannot be blank.", new[] { nameof(LastName) });
    }
}
