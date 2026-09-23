using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CeylonTrail.Api.Configuration;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Auth;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CeylonTrail.Api.Services;

public sealed class AuthenticationService(
    ApplicationDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    JwtOptions jwtOptions) : IAuthenticationService
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<(bool Succeeded, string? Error, AuthResponse? Response)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role) ||
            !Enum.IsDefined(role) ||
            (role != UserRole.Tourist && role != UserRole.TourismProvider))
        {
            return (false, "The requested role is not available for public registration.", null);
        }

        if (await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            return (false, "An account with that email already exists.", null);
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = normalizedEmail,
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null, CreateAuthResponse(user));
    }

    public async Task<(bool Succeeded, string? Error, AuthResponse? Response)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == normalizedEmail,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            return (false, InvalidCredentialsMessage, null);
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return (false, InvalidCredentialsMessage, null);
        }

        return (true, null, CreateAuthResponse(user));
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(jwtOptions.ExpiryMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            ToUserResponse(user));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static UserResponse ToUserResponse(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role.ToString(),
        user.IsActive);
}
