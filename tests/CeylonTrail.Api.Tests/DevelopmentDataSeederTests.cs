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

        Assert.Equal(4, users.Count);
        Assert.Equal(
            ["ADMIN@TEST.COM", "COORDINATOR@TEST.COM", "PROVIDER@TEST.COM", "TOURIST@TEST.COM"],
            users.Select(user => user.Email).ToArray());
        Assert.Equal(UserRole.Administrator, users[0].Role);
        Assert.Equal(UserRole.TravelCoordinator, users[1].Role);
        Assert.Equal(UserRole.TourismProvider, users[2].Role);
        Assert.Equal(UserRole.Tourist, users[3].Role);
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

        Assert.Equal(4, await dbContext.Users.CountAsync());
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "COORDINATOR@TEST.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "ADMIN@TEST.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "PROVIDER@TEST.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "TOURIST@TEST.COM"));
    }

    [Fact]
    public async Task SeedAsync_SeedsCategoriesAndDoesNotCreateDemoAttraction()
    {
        await using var dbContext = CreateDbContext();

        var passwordHasher = new PasswordHasher<User>();
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);

        var categories = await dbContext.Categories.ToListAsync();
        Assert.Equal(4, categories.Count);
        Assert.Contains(categories, c => c.Name == "Historical & Cultural");
        Assert.Contains(categories, c => c.Name == "Nature & Wildlife");
        Assert.Contains(categories, c => c.Name == "Adventure & Outdoor");
        Assert.Contains(categories, c => c.Name == "Beaches & Coastal");

        Assert.Empty(dbContext.Attractions);
        Assert.Empty(dbContext.AvailabilitySlots);
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
