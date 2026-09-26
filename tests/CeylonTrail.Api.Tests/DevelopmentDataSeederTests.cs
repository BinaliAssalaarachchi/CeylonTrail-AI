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
    public async Task SeedAsync_CreatesBookableApprovedDemoAttraction()
    {
        await using var dbContext = CreateDbContext();

        var passwordHasher = new PasswordHasher<User>();
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);

        var provider = await dbContext.Users.SingleAsync(user => user.Email == "PROVIDER@TEST.COM");
        var attraction = await dbContext.Attractions
            .Include(item => item.Schedules)
            .Include(item => item.ExperienceSlots)
            .SingleAsync(item => item.Name == "Sigiriya Heritage Sunrise Trail");
        var availability = await dbContext.AvailabilitySlots
            .SingleAsync(slot => slot.AttractionId == attraction.Id);

        Assert.Equal(provider.Id, attraction.ProviderId);
        Assert.Equal("Approved", attraction.Status);
        Assert.True(attraction.IsActive);
        Assert.Equal(6500m, attraction.Price);
        Assert.NotEmpty(attraction.Schedules);
        Assert.NotEmpty(attraction.ExperienceSlots);
        Assert.Contains(attraction.ExperienceSlots, slot => slot.Date >= DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.True(availability.EndTime > DateTime.UtcNow);
        Assert.Equal(40, availability.MaxCapacity);
        Assert.Equal(0, availability.BookedCapacity);
        Assert.Equal(6500m, availability.PricePerPerson);
        Assert.Equal(1, await dbContext.Attractions.CountAsync(item => item.Name == "Sigiriya Heritage Sunrise Trail"));
        Assert.Equal(1, await dbContext.AvailabilitySlots.CountAsync(slot => slot.AttractionId == attraction.Id));
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
