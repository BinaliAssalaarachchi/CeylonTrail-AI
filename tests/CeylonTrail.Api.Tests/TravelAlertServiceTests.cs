using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.TravelAlerts;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class TravelAlertServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesAlertWithUtcAuditFields()
    {
        await using var dbContext = CreateDbContext();
        var creator = AddUser(dbContext, isActive: true);
        var service = new TravelAlertService(dbContext);

        var result = await service.CreateAsync(CreateRequest(), creator.Id);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(creator.Id, result.Response!.CreatedByUserId);
        Assert.Equal(DateTimeKind.Utc, result.Response.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, result.Response.UpdatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, result.Response.StartDateTime.Kind);
        Assert.Equal(DateTimeKind.Utc, result.Response.EndDateTime.Kind);
    }

    [Fact]
    public async Task CreateAsync_WhenEndDateIsNotLaterThanStartDate_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var creator = AddUser(dbContext, isActive: true);
        var service = new TravelAlertService(dbContext);
        var request = CreateRequest();
        request.EndDateTime = request.StartDateTime;

        var result = await service.CreateAsync(request, creator.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("EndDateTime must be later than StartDateTime.", result.Error);
        Assert.Empty(dbContext.TravelAlerts);
    }

    [Fact]
    public async Task CreateAsync_WhenCreatorIsInactiveOrMissing_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var inactiveCreator = AddUser(dbContext, isActive: false);
        var service = new TravelAlertService(dbContext);

        var inactiveResult = await service.CreateAsync(CreateRequest(), inactiveCreator.Id);
        var missingResult = await service.CreateAsync(CreateRequest(), Guid.NewGuid());

        Assert.False(inactiveResult.Succeeded);
        Assert.False(missingResult.Succeeded);
        Assert.Equal("The authenticated creator does not exist or is inactive.", inactiveResult.Error);
        Assert.Equal("The authenticated creator does not exist or is inactive.", missingResult.Error);
    }

    [Fact]
    public async Task UpdateAsync_PreservesCreatorAndCreatedAt_AndRefreshesUpdatedAt()
    {
        await using var dbContext = CreateDbContext();
        var creator = AddUser(dbContext, isActive: true);
        var service = new TravelAlertService(dbContext);
        var created = await service.CreateAsync(CreateRequest(), creator.Id);
        var originalCreatedAt = created.Response!.CreatedAt;
        var originalUpdatedAt = created.Response.UpdatedAt;

        await Task.Delay(2);
        var update = new UpdateTravelAlertRequest
        {
            Title = "Updated alert",
            Description = "Heavy rain is expected in the district.",
            AlertType = TravelAlertType.Weather,
            Severity = TravelAlertSeverity.Medium,
            District = "Kandy",
            StartDateTime = new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Unspecified),
            EndDateTime = new DateTime(2026, 9, 21, 18, 0, 0, DateTimeKind.Unspecified),
            Status = TravelAlertStatus.Active,
            Source = null
        };
        update.Title = "Updated alert";

        var result = await service.UpdateAsync(created.Response.Id, update);

        Assert.True(result.Succeeded);
        Assert.Equal(creator.Id, result.Response!.CreatedByUserId);
        Assert.Equal(originalCreatedAt, result.Response.CreatedAt);
        Assert.True(result.Response.UpdatedAt > originalUpdatedAt);
        Assert.Equal("Updated alert", result.Response.Title);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingAlert()
    {
        await using var dbContext = CreateDbContext();
        var creator = AddUser(dbContext, isActive: true);
        var service = new TravelAlertService(dbContext);
        var created = await service.CreateAsync(CreateRequest(), creator.Id);

        var result = await service.DeleteAsync(created.Response!.Id);

        Assert.True(result.Succeeded);
        Assert.Empty(dbContext.TravelAlerts);
    }

    private static CreateTravelAlertRequest CreateRequest() => new()
    {
        Title = "Heavy rain warning",
        Description = "Heavy rain is expected in the district.",
        AlertType = TravelAlertType.Weather,
        Severity = TravelAlertSeverity.Medium,
        District = "Kandy",
        StartDateTime = new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Unspecified),
        EndDateTime = new DateTime(2026, 9, 21, 18, 0, 0, DateTimeKind.Unspecified),
        Status = TravelAlertStatus.Active,
        Source = null
    };

    private static User AddUser(ApplicationDbContext dbContext, bool isActive)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Nimal",
            LastName = "Perera",
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            Role = UserRole.TravelCoordinator,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return user;
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
