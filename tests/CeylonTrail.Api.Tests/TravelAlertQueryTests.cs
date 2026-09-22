using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.TravelAlerts;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class TravelAlertQueryTests
{
    [Fact]
    public async Task QueryAsync_UsesDefaultPaginationAndReturnsMetadata()
    {
        await using var dbContext = CreateDbContext();
        SeedAlerts(dbContext, 12);
        var service = new TravelAlertService(dbContext);

        var result = await service.QueryAsync(new TravelAlertQueryRequest());

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Response!.Page);
        Assert.Equal(10, result.Response.PageSize);
        Assert.Equal(12, result.Response.TotalCount);
        Assert.Equal(2, result.Response.TotalPages);
        Assert.Equal(10, result.Response.Items.Count);
    }

    [Fact]
    public async Task QueryAsync_AppliesDistrictAndEnumFilters()
    {
        await using var dbContext = CreateDbContext();
        SeedAlerts(dbContext, 6);
        var service = new TravelAlertService(dbContext);

        var result = await service.QueryAsync(new TravelAlertQueryRequest
        {
            District = "Kandy",
            Status = TravelAlertStatus.Active,
            Severity = TravelAlertSeverity.High,
            AlertType = TravelAlertType.Weather
        });

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Response!.Items);
        Assert.All(result.Response.Items, alert =>
        {
            Assert.Equal("Kandy", alert.District);
            Assert.Equal(TravelAlertStatus.Active, alert.Status);
            Assert.Equal(TravelAlertSeverity.High, alert.Severity);
            Assert.Equal(TravelAlertType.Weather, alert.AlertType);
        });
    }

    [Fact]
    public async Task QueryAsync_SearchesTitleDescriptionAndDistrictCaseInsensitively()
    {
        await using var dbContext = CreateDbContext();
        SeedAlerts(dbContext, 3);
        var service = new TravelAlertService(dbContext);

        var result = await service.QueryAsync(new TravelAlertQueryRequest
        {
            Search = "WARNING"
        });

        Assert.True(result.Succeeded);
        Assert.Single(result.Response!.Items);
        Assert.Contains("Flood", result.Response.Items[0].Title);
    }

    [Fact]
    public async Task QueryAsync_AppliesControlledSorting()
    {
        await using var dbContext = CreateDbContext();
        SeedAlerts(dbContext, 3);
        var service = new TravelAlertService(dbContext);

        var result = await service.QueryAsync(new TravelAlertQueryRequest
        {
            SortBy = "title",
            SortDirection = "asc"
        });

        Assert.True(result.Succeeded);
        Assert.Equal(
            result.Response!.Items.OrderBy(alert => alert.Title).Select(alert => alert.Id),
            result.Response.Items.Select(alert => alert.Id));
    }

    [Fact]
    public async Task QueryAsync_RejectsInvalidPageBoundsAndSortValues()
    {
        await using var dbContext = CreateDbContext();
        var service = new TravelAlertService(dbContext);

        var invalidPage = await service.QueryAsync(new TravelAlertQueryRequest { Page = 0 });
        var invalidSize = await service.QueryAsync(new TravelAlertQueryRequest { PageSize = 101 });
        var invalidSort = await service.QueryAsync(new TravelAlertQueryRequest { SortBy = "id" });

        Assert.False(invalidPage.Succeeded);
        Assert.False(invalidSize.Succeeded);
        Assert.False(invalidSort.Succeeded);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmptyPageWhenNoAlertsMatch()
    {
        await using var dbContext = CreateDbContext();
        SeedAlerts(dbContext, 2);
        var service = new TravelAlertService(dbContext);

        var result = await service.QueryAsync(new TravelAlertQueryRequest
        {
            District = "Jaffna"
        });

        Assert.True(result.Succeeded);
        Assert.Empty(result.Response!.Items);
        Assert.Equal(0, result.Response.TotalCount);
        Assert.Equal(0, result.Response.TotalPages);
    }

    private static void SeedAlerts(ApplicationDbContext dbContext, int count)
    {
        var creator = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Travel",
            LastName = "Coordinator",
            Email = "coordinator@example.com",
            PasswordHash = "hash",
            Role = UserRole.TravelCoordinator,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(creator);

        var alerts = Enumerable.Range(0, count).Select(index => new TravelAlert
        {
            Id = Guid.NewGuid(),
            Title = index == 0 ? "Flood warning" : $"Alert {index:00}",
            Description = index == 1 ? "Flood risk is increasing." : "Routine travel information.",
            AlertType = index % 2 == 0 ? TravelAlertType.Weather : TravelAlertType.RoadClosure,
            Severity = index % 3 == 0 ? TravelAlertSeverity.High : TravelAlertSeverity.Medium,
            District = index % 2 == 0 ? "Kandy" : "Colombo",
            StartDateTime = new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc).AddDays(index),
            EndDateTime = new DateTime(2026, 9, 21, 18, 0, 0, DateTimeKind.Utc).AddDays(index),
            Status = index % 2 == 0 ? TravelAlertStatus.Active : TravelAlertStatus.Draft,
            CreatedByUserId = creator.Id,
            CreatedByUser = creator,
            CreatedAt = DateTime.UtcNow.AddMinutes(index),
            UpdatedAt = DateTime.UtcNow.AddMinutes(index)
        });

        dbContext.TravelAlerts.AddRange(alerts);
        dbContext.SaveChanges();
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
