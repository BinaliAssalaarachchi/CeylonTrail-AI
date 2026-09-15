using CeylonTrail.Api.DTOs.Auth;

namespace CeylonTrail.Api.Interfaces;

public interface IAuthenticationService
{
    Task<(bool Succeeded, string? Error, AuthResponse? Response)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, AuthResponse? Response)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
