using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
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

        Assert.Equal(8, users.Count);
        Assert.Equal(
            [
                "ADMIN@EVALUATOR.COM", "ADMIN@TEST.COM",
                "COORDINATOR@EVALUATOR.COM", "COORDINATOR@TEST.COM",
                "PROVIDER@EVALUATOR.COM", "PROVIDER@TEST.COM",
                "TOURIST@EVALUATOR.COM", "TOURIST@TEST.COM"
            ],
            users.Select(user => user.Email).ToArray());
        Assert.All(users.Where(user => user.Email.StartsWith("ADMIN@")), user => Assert.Equal(UserRole.Administrator, user.Role));
        Assert.All(users.Where(user => user.Email.StartsWith("COORDINATOR@")), user => Assert.Equal(UserRole.TravelCoordinator, user.Role));
        Assert.All(users.Where(user => user.Email.StartsWith("PROVIDER@")), user => Assert.Equal(UserRole.TourismProvider, user.Role));
        Assert.All(users.Where(user => user.Email.StartsWith("TOURIST@")), user => Assert.Equal(UserRole.Tourist, user.Role));
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
            Email = "COORDINATOR@EVALUATOR.COM",
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

        Assert.Equal(8, await dbContext.Users.CountAsync());
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "COORDINATOR@EVALUATOR.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "ADMIN@EVALUATOR.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "PROVIDER@EVALUATOR.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "TOURIST@EVALUATOR.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "COORDINATOR@TEST.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "ADMIN@TEST.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "PROVIDER@TEST.COM"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "TOURIST@TEST.COM"));
    }

    [Fact]
    public async Task SeedAsync_SeedsCategoriesAndRealisticIdempotentDemoDataset()
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

        Assert.Equal(8, await dbContext.Attractions.CountAsync());
        Assert.Equal(8, await dbContext.AvailabilitySlots.CountAsync());
        Assert.Equal(8, await dbContext.ExperienceSlots.CountAsync());
        Assert.Equal(56, await dbContext.AttractionSchedules.CountAsync());
        Assert.Contains(await dbContext.Attractions.ToListAsync(), attraction =>
            attraction.Id == Guid.Parse("55555555-5555-5555-5555-555555555555") &&
            attraction.Status == "Approved" && attraction.IsActive);
    }

    [Fact]
    public async Task SeedAsync_PreservesBookedDemoAttractionAndAvailabilitySlot()
    {
        await using var dbContext = CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);

        var provider = await dbContext.Users.SingleAsync(user => user.Email == "PROVIDER@EVALUATOR.COM");
        var tourist = await dbContext.Users.SingleAsync(user => user.Email == "TOURIST@EVALUATOR.COM");
        var attractionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var slotId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);
        var now = DateTime.UtcNow;
        var slot = await dbContext.AvailabilitySlots.SingleAsync(item => item.Id == slotId);
        slot.BookedCapacity = 1;
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

        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);

        Assert.NotNull(await dbContext.Attractions.SingleOrDefaultAsync(item => item.Id == attractionId));
        Assert.NotNull(await dbContext.AvailabilitySlots.SingleOrDefaultAsync(item => item.Id == slotId));
        Assert.NotNull(await dbContext.BookingItems.SingleOrDefaultAsync(item => item.Id == bookingItem.Id));
        Assert.Equal(1, (await dbContext.AvailabilitySlots.SingleAsync(item => item.Id == slotId)).BookedCapacity);
    }

    [Fact]
    public async Task SeedAsync_PublicSearchReturnsApprovedActiveDatasetWithFilteringAndPagination()
    {
        await using var dbContext = CreateDbContext();
        await DevelopmentDataSeeder.SeedAsync(dbContext, new PasswordHasher<User>());
        var service = new AttractionService(dbContext);

        var all = await service.SearchAsync(new AttractionSearchRequest { PageSize = 100 });
        Assert.True(all.Succeeded);
        Assert.Equal(8, all.Value!.TotalCount);
        Assert.All(all.Value.Items, item =>
        {
            Assert.Equal("Approved", item.Status);
            Assert.True(item.IsActive);
            Assert.NotEmpty(item.ExperienceSlots);
        });

        var adventureCategoryId = (await dbContext.Categories
            .SingleAsync(category => category.Name == "Adventure & Outdoor")).Id;
        var page = await service.SearchAsync(new AttractionSearchRequest
        {
            CategoryId = adventureCategoryId,
            Page = 1,
            PageSize = 1,
            Sort = "price_desc"
        });
        Assert.True(page.Succeeded);
        Assert.Equal(1, page.Value!.TotalCount);
        Assert.Single(page.Value.Items);
        Assert.Equal("Ella Tea Country Hike", page.Value.Items[0].Name);
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
