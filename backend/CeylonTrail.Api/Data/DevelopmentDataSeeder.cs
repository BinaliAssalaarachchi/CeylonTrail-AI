using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Data;

public static class DevelopmentDataSeeder
{
    private const string SeedPassword = "Test@123";

    private static readonly SeedUser[] SeedUsers =
    [
        new("coordinator@test.com", "Test", "Coordinator", UserRole.TravelCoordinator),
        new("admin@test.com", "Test", "Administrator", UserRole.Administrator)
    ];

    public static async Task SeedAsync(
        ApplicationDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        var usersAdded = false;

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
            usersAdded = true;
        }

        if (usersAdded)
        {
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
