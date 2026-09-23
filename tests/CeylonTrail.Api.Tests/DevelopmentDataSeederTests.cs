using CeylonTrail.Api.Data;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class DevelopmentDataSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesPrivilegedUsersWithHashedPasswords()
    {
        await using var dbContext = CreateDbContext();

        await DevelopmentDataSeeder.SeedAsync(dbContext, new PasswordHasher<User>());

        var users = await dbContext.Users.OrderBy(user => user.Email).ToListAsync();

        Assert.Equal(2, users.Count);
        Assert.Equal(
            ["ADMIN@TEST.COM", "COORDINATOR@TEST.COM"],
            users.Select(user => user.Email).ToArray());
        Assert.Equal(UserRole.Administrator, users[0].Role);
        Assert.Equal(UserRole.TravelCoordinator, users[1].Role);
        Assert.All(users, user =>
        {
            Assert.DoesNotContain("Test@123", user.PasswordHash);
            Assert.Equal(
                PasswordVerificationResult.Success,
                new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, "Test@123"));
            Assert.True(user.IsActive);
        });
    }

    [Fact]
    public async Task SeedAsync_IsIdempotentAndUsesNormalizedEmail()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Existing",
            LastName = "Coordinator",
            Email = "COORDINATOR@TEST.COM",
            PasswordHash = "existing-hash",
            Role = UserRole.TravelCoordinator,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var passwordHasher = new PasswordHasher<User>();
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);

        Assert.Equal(2, await dbContext.Users.CountAsync());
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "COORDINATOR@TEST.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "ADMIN@TEST.COM"));
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
