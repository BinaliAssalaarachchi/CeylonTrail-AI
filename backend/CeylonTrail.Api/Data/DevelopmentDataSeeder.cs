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

        // Remove legacy demo attraction if it exists so only user-created attractions exist
        var demoAttraction = await dbContext.Attractions
            .Include(a => a.Schedules)
            .Include(a => a.ExperienceSlots)
            .Include(a => a.Images)
            .SingleOrDefaultAsync(item => item.Id == DemoAttractionId || item.Name == DemoAttractionName, cancellationToken);

        if (demoAttraction is not null)
        {
            var availSlots = await dbContext.AvailabilitySlots
                .Where(s => s.AttractionId == demoAttraction.Id)
                .ToListAsync(cancellationToken);
            dbContext.AvailabilitySlots.RemoveRange(availSlots);
            dbContext.Attractions.Remove(demoAttraction);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private sealed record SeedUser(
        string Email,
        string FirstName,
        string LastName,
        UserRole Role);
}
