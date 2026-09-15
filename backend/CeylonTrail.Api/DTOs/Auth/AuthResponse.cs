namespace CeylonTrail.Api.DTOs.Auth;

public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserResponse User);
