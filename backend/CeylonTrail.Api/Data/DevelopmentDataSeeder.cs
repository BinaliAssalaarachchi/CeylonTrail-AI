using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Data;

public static class DevelopmentDataSeeder
{
    private const string SeedPassword = "Test@123";
    private const string DemoAttractionName = "Sigiriya Heritage Sunrise Trail";
    private const decimal DemoAttractionPrice = 6500m;
    private const decimal DemoAvailabilityPrice = 6500m;

    private static readonly Guid DemoAttractionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly SeedUser[] SeedUsers =
    [
        new("tourist@test.com", "Test", "Tourist", UserRole.Tourist),
        new("provider@test.com", "Test", "Provider", UserRole.TourismProvider),
        new("coordinator@test.com", "Test", "Coordinator", UserRole.TravelCoordinator),
        new("admin@test.com", "Test", "Administrator", UserRole.Administrator)
    ];

    public static async Task SeedAsync(
        ApplicationDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        foreach (var seedUser in SeedUsers)
        {
            var normalizedEmail = NormalizeEmail(seedUser.Email);
            if (await dbContext.Users.AnyAsync(
                    user => user.Email == normalizedEmail,
                    cancellationToken))
            {
                continue;
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = seedUser.FirstName,
                LastName = seedUser.LastName,
                Email = normalizedEmail,
                Role = seedUser.Role,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            user.PasswordHash = passwordHasher.HashPassword(user, SeedPassword);
            dbContext.Users.Add(user);
        }

        var categories = new[]
        {
            new Category { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Historical & Cultural", Description = "Ancient temples, palaces, and UNESCO heritage monuments." },
            new Category { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Nature & Wildlife", Description = "National parks, bird sanctuaries, and rainforest expeditions." },
            new Category { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Adventure & Outdoor", Description = "Mountain hiking, white water rafting, and surfing." },
            new Category { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "Beaches & Coastal", Description = "Tropical coastlines, whale watching, and marine sanctuaries." }
        };

        foreach (var category in categories)
        {
            if (!await dbContext.Categories.AnyAsync(c => c.Name == category.Name, cancellationToken))
            {
                dbContext.Categories.Add(category);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var provider = await dbContext.Users
            .SingleAsync(user => user.Email == "PROVIDER@TEST.COM", cancellationToken);
        var demoCategory = await dbContext.Categories
            .SingleAsync(item => item.Name == "Historical & Cultural", cancellationToken);

        var attraction = await dbContext.Attractions
            .SingleOrDefaultAsync(item => item.Id == DemoAttractionId, cancellationToken);

        if (attraction is null)
        {
            attraction = new Attraction
            {
                Id = DemoAttractionId,
                ProviderId = provider.Id,
                CategoryId = demoCategory.Id,
                Name = DemoAttractionName,
                Description = "A guided sunrise heritage walk with panoramic views of the ancient Sigiriya landscape.",
                District = "Matale",
                Address = "Sigiriya Rock Fortress, Sigiriya, Matale, Sri Lanka",
                Latitude = 7.9570m,
                Longitude = 80.7603m,
                Price = DemoAttractionPrice,
                Status = "Approved",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Attractions.Add(attraction);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureSchedulesAsync(dbContext, attraction.Id, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var demoDate = today.AddDays(7);
        var experienceSlot = await dbContext.ExperienceSlots
            .SingleOrDefaultAsync(slot =>
                slot.AttractionId == attraction.Id &&
                slot.Date >= today &&
                slot.StartTime == new TimeOnly(9, 0), cancellationToken);

        if (experienceSlot is null)
        {
            dbContext.ExperienceSlots.Add(new ExperienceSlot
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                AttractionId = attraction.Id,
                Date = demoDate,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(11, 0),
                Capacity = 40,
                AvailableCapacity = 40
            });
        }

        var startTime = DateTime.SpecifyKind(demoDate.ToDateTime(new TimeOnly(9, 0)), DateTimeKind.Utc);
        var endTime = DateTime.SpecifyKind(demoDate.ToDateTime(new TimeOnly(11, 0)), DateTimeKind.Utc);
        var availabilitySlot = await dbContext.AvailabilitySlots
            .SingleOrDefaultAsync(slot =>
                slot.AttractionId == attraction.Id &&
                slot.StartTime >= DateTime.UtcNow &&
                slot.StartTime.Hour == 9, cancellationToken);

        if (availabilitySlot is null)
        {
            dbContext.AvailabilitySlots.Add(new AvailabilitySlot
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                AttractionId = attraction.Id,
                StartTime = startTime,
                EndTime = endTime,
                MaxCapacity = 40,
                BookedCapacity = 0,
                PricePerPerson = DemoAvailabilityPrice,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureSchedulesAsync(
        ApplicationDbContext dbContext,
        Guid attractionId,
        CancellationToken cancellationToken)
    {
        var existingDays = await dbContext.AttractionSchedules
            .Where(schedule => schedule.AttractionId == attractionId)
            .Select(schedule => schedule.DayOfWeek)
            .ToListAsync(cancellationToken);

        var missingDays = Enum.GetValues<DayOfWeek>().Except(existingDays);
        foreach (var day in missingDays)
        {
            dbContext.AttractionSchedules.Add(new AttractionSchedule
            {
                Id = Guid.NewGuid(),
                AttractionId = attractionId,
                DayOfWeek = day,
                OpeningTime = new TimeOnly(8, 0),
                ClosingTime = new TimeOnly(17, 0),
                IsClosed = false
            });
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private sealed record SeedUser(
        string Email,
        string FirstName,
        string LastName,
        UserRole Role);
}
