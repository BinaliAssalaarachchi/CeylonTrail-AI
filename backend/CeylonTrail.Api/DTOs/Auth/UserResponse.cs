namespace CeylonTrail.Api.DTOs.Auth;

public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsActive);
