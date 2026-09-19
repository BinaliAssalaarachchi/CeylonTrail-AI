using CeylonTrail.Api.Configuration;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Auth;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task RegisterAndLogin_StoresOnlyAHashAndReturnsSafeUserData()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        const string password = "ValidPassword123!";

        var registration = await service.RegisterAsync(new RegisterRequest
        {
            FirstName = "Nimal",
            LastName = "Perera",
            Email = "NIMAL@example.com",
            Password = password,
            Role = nameof(UserRole.Tourist)
        });

        Assert.True(registration.Succeeded);
        Assert.NotNull(registration.Response);
        Assert.Equal("NIMAL@EXAMPLE.COM", registration.Response!.User.Email);
        Assert.DoesNotContain(password, dbContext.Users.Single().PasswordHash);

        var login = await service.LoginAsync(new LoginRequest
        {
            Email = "nimal@example.com",
            Password = password
        });

        Assert.True(login.Succeeded);
        Assert.NotNull(login.Response);
        Assert.Equal(nameof(UserRole.Tourist), login.Response!.User.Role);
        Assert.DoesNotContain("PasswordHash", login.Response.User.GetType().GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_UsesGenericError()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.RegisterAsync(new RegisterRequest
        {
            FirstName = "Nimal",
            LastName = "Perera",
            Email = "nimal@example.com",
            Password = "ValidPassword123!",
            Role = nameof(UserRole.Tourist)
        });

        var result = await service.LoginAsync(new LoginRequest
        {
            Email = "unknown@example.com",
            Password = "wrong-password"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid email or password.", result.Error);
        Assert.Null(result.Response);
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AuthenticationService CreateService(ApplicationDbContext dbContext) => new(
        dbContext,
        new PasswordHasher<User>(),
        new JwtOptions
        {
            Issuer = "CeylonTrail.Tests",
            Audience = "CeylonTrail.Tests",
            SigningKey = "test-signing-key-that-is-long-enough",
            ExpiryMinutes = 60
        });
}
