using System.Security.Cryptography;
using System.Text;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Data;

public static class DevelopmentDataSeeder
{
    private const string SeedPassword = "Test@123";
    private const string ApprovedStatus = "Approved";
    private const string ProviderEmail = "PROVIDER@TEST.COM";
    private static readonly DateOnly DemoDate = new(2026, 10, 3);
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

        await SeedDemoAttractionsAsync(dbContext, cancellationToken);
    }

    private static async Task SeedDemoAttractionsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.Users.SingleAsync(
            user => user.Email == ProviderEmail,
            cancellationToken);
        var categories = await dbContext.Categories
            .ToDictionaryAsync(category => category.Name, cancellationToken);

        var definitions = new[]
        {
            new DemoAttraction(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Guid.Parse("77777777-7777-7777-7777-777777777777"),
                "Sigiriya Heritage Sunrise Trail", "Historical & Cultural", "Matale",
                "A guided sunrise walk around the Sigiriya heritage landscape.",
                "Sigiriya, Matale, Sri Lanka", 7.9570m, 80.7603m, 6500m, 2),
            new DemoAttraction(
                Guid.Parse("55555555-5555-5555-5555-555555555556"), null,
                "Kandy Lake and Temple Walk", "Historical & Cultural", "Kandy",
                "A small-group cultural walk through central Kandy landmarks.",
                "Kandy, Sri Lanka", 7.2906m, 80.6337m, 4200m, 2),
            new DemoAttraction(
                Guid.Parse("55555555-5555-5555-5555-555555555557"), null,
                "Galle Fort Heritage Walk", "Historical & Cultural", "Galle",
                "A guided walking experience through the historic fort quarter.",
                "Galle Fort, Galle, Sri Lanka", 6.0329m, 80.2168m, 3800m, 2),
            new DemoAttraction(
                Guid.Parse("55555555-5555-5555-5555-555555555558"), null,
                "Udawalawe Wildlife Safari", "Nature & Wildlife", "Ratnapura",
                "A planned wildlife outing with a local safari provider.",
                "Udawalawe, Sri Lanka", 6.4750m, 80.8880m, 9000m, 3),
            new DemoAttraction(
                Guid.Parse("55555555-5555-5555-5555-555555555559"), null,
                "Sinharaja Rainforest Nature Walk", "Nature & Wildlife", "Ratnapura",
                "A guided forest walk focused on nature observation and conservation.",
                "Weddagala, Sri Lanka", 6.4170m, 80.4500m, 5500m, 2),
            new DemoAttraction(
                Guid.Parse("55555555-5555-5555-5555-555555555560"), null,
                "Ella Tea Country Hike", "Adventure & Outdoor", "Badulla",
                "A moderate hill-country hike through tea-growing landscapes.",
                "Ella, Badulla, Sri Lanka", 6.8667m, 81.0466m, 4800m, 2),
            new DemoAttraction(
                Guid.Parse("55555555-5555-5555-5555-555555555561"), null,
                "Bentota River Kayak", "Beaches & Coastal", "Galle",
                "A scheduled beginner-friendly paddle on the Bentota river.",
                "Bentota, Galle, Sri Lanka", 6.4210m, 80.0000m, 7000m, 2),
            new DemoAttraction(
                Guid.Parse("55555555-5555-5555-5555-555555555562"), null,
                "Arugam Bay Coastal Surf Lesson", "Beaches & Coastal", "Ampara",
                "An introductory surf lesson with equipment and instructor support.",
                "Arugam Bay, Ampara, Sri Lanka", 6.8400m, 81.8360m, 8000m, 2)
        };

        foreach (var definition in definitions)
        {
            if (!categories.TryGetValue(definition.Category, out var category))
                continue;

            var now = DateTime.UtcNow;
            var attraction = await dbContext.Attractions
                .SingleOrDefaultAsync(item => item.Id == definition.Id, cancellationToken);
            if (attraction is null)
            {
                attraction = new Attraction
                {
                    Id = definition.Id,
                    ProviderId = provider.Id,
                    CategoryId = category.Id,
                    Name = definition.Name,
                    Description = definition.Description,
                    District = definition.District,
                    Address = definition.Address,
                    Latitude = definition.Latitude,
                    Longitude = definition.Longitude,
                    Price = definition.Price,
                    Status = ApprovedStatus,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.Attractions.Add(attraction);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await SeedSchedulesAsync(dbContext, attraction.Id, cancellationToken);
            await SeedAvailabilityAsync(dbContext, definition, cancellationToken);
            await SeedExperienceSlotAsync(dbContext, definition, cancellationToken);
        }
    }

    private static async Task SeedSchedulesAsync(
        ApplicationDbContext dbContext,
        Guid attractionId,
        CancellationToken cancellationToken)
    {
        var existingDays = (await dbContext.AttractionSchedules
            .Where(schedule => schedule.AttractionId == attractionId)
            .Select(schedule => schedule.DayOfWeek)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            if (existingDays.Contains(day))
                continue;
            dbContext.AttractionSchedules.Add(new AttractionSchedule
            {
                Id = Guid.NewGuid(),
                AttractionId = attractionId,
                DayOfWeek = day,
                OpeningTime = new TimeOnly(6, 0),
                ClosingTime = new TimeOnly(18, 0),
                IsClosed = false
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAvailabilityAsync(
        ApplicationDbContext dbContext,
        DemoAttraction definition,
        CancellationToken cancellationToken)
    {
        var slotId = definition.AvailabilitySlotId ??
            DeterministicGuid(definition.Id, "availability");
        var existing = await dbContext.AvailabilitySlots
            .AnyAsync(slot => slot.Id == slotId, cancellationToken);
        if (existing)
            return;

        var start = DemoDate.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        dbContext.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = slotId,
            AttractionId = definition.Id,
            StartTime = start,
            EndTime = start.AddHours(definition.DurationHours),
            MaxCapacity = 20,
            BookedCapacity = 0,
            PricePerPerson = definition.Price,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedExperienceSlotAsync(
        ApplicationDbContext dbContext,
        DemoAttraction definition,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.ExperienceSlots.AnyAsync(
            slot => slot.AttractionId == definition.Id && slot.Date == DemoDate,
            cancellationToken);
        if (exists)
            return;

        dbContext.ExperienceSlots.Add(new ExperienceSlot
        {
            Id = DeterministicGuid(definition.Id, "experience"),
            AttractionId = definition.Id,
            Date = DemoDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(8 + definition.DurationHours, 0),
            Capacity = 20,
            AvailableCapacity = 20
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Guid DeterministicGuid(Guid source, string suffix) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{source:N}:{suffix}")));

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private sealed record SeedUser(
        string Email,
        string FirstName,
        string LastName,
        UserRole Role);

    private sealed record DemoAttraction(
        Guid Id,
        Guid? AvailabilitySlotId,
        string Name,
        string Category,
        string District,
        string Description,
        string Address,
        decimal Latitude,
        decimal Longitude,
        decimal Price,
        int DurationHours);
}
