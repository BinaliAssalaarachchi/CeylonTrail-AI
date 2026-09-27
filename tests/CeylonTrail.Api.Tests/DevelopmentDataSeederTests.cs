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

    [Fact]
    public async Task SeedAsync_PreservesBookedDemoAttractionAndAvailabilitySlot()
    {
        await using var dbContext = CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);

        var provider = await dbContext.Users.SingleAsync(user => user.Email == "PROVIDER@TEST.COM");
        var tourist = await dbContext.Users.SingleAsync(user => user.Email == "TOURIST@TEST.COM");
        var category = await dbContext.Categories.SingleAsync(item => item.Name == "Historical & Cultural");
        var attractionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var slotId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var now = DateTime.UtcNow;
        var attraction = new Attraction
        {
            Id = attractionId,
            ProviderId = provider.Id,
            CategoryId = category.Id,
            Name = "Sigiriya Heritage Sunrise Trail",
            Description = "Seeded demo attraction.",
            District = "Matale",
            Address = "Sigiriya, Matale, Sri Lanka",
            Price = 6500m,
            Status = "Approved",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var slot = new AvailabilitySlot
        {
            Id = slotId,
            AttractionId = attractionId,
            StartTime = now.AddDays(7),
            EndTime = now.AddDays(7).AddHours(2),
            MaxCapacity = 40,
            BookedCapacity = 1,
            PricePerPerson = 6500m,
            CreatedAt = now,
            UpdatedAt = now
        };
        var bookingItem = new BookingItem
        {
            Id = Guid.NewGuid(),
            AvailabilitySlotId = slotId,
            NumberOfGuests = 1,
            UnitPrice = 6500m,
            SubTotal = 6500m
        };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = tourist.Id,
            CurrentStatus = BookingStatus.Draft,
            TotalAmount = 6500m,
            CreatedAt = now,
            UpdatedAt = now,
            Items = [bookingItem]
        };

        dbContext.Attractions.Add(attraction);
        dbContext.AvailabilitySlots.Add(slot);
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);

        Assert.NotNull(await dbContext.Attractions.SingleOrDefaultAsync(item => item.Id == attractionId));
        Assert.NotNull(await dbContext.AvailabilitySlots.SingleOrDefaultAsync(item => item.Id == slotId));
        Assert.NotNull(await dbContext.BookingItems.SingleOrDefaultAsync(item => item.Id == bookingItem.Id));
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
