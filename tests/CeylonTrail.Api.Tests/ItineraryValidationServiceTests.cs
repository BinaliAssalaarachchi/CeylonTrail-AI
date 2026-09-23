using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class ItineraryValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_WithValidItinerary_IsFeasible()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var result = await CreateService(dbContext).ValidateAsync(CreateRequest(), user.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(ValidationOverallStatus.Valid, result.Response!.OverallStatus);
        Assert.Equal(ValidationRiskLevel.Low, result.Response.RiskLevel);
        Assert.True(result.Response.IsFeasible);
        Assert.Empty(result.Response.Issues);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidTimeRange_CreatesBlockingIssue()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var request = CreateRequest();
        request.Items[0].EndDateTime = request.Items[0].StartDateTime;

        var result = await CreateService(dbContext).ValidateAsync(request, user.Id);

        var issue = Assert.Single(result.Response!.Issues);
        Assert.Equal(ValidationIssueType.InvalidTimeRange, issue.IssueType);
        Assert.True(issue.IsBlocking);
        Assert.Equal(ValidationOverallStatus.Invalid, result.Response.OverallStatus);
        Assert.False(result.Response.IsFeasible);
    }

    [Fact]
    public async Task ValidateAsync_WithOverlappingItems_CreatesScheduleConflict()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var request = CreateRequest();
        request.Items.Add(new ItineraryItemRequest
        {
            Reference = "item-2",
            Title = "Temple",
            District = "Kandy",
            StartDateTime = Utc(9, 30),
            EndDateTime = Utc(12, 0)
        });

        var result = await CreateService(dbContext).ValidateAsync(request, user.Id);

        Assert.Contains(result.Response!.Issues, issue =>
            issue.IssueType == ValidationIssueType.ScheduleConflict &&
            issue.IsBlocking &&
            issue.RuleCode == "ITINERARY_SCHEDULE_OVERLAP");
    }

    [Fact]
    public async Task ValidateAsync_WhenEstimatedCostExceedsBudget_CreatesBudgetIssue()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var request = CreateRequest();
        request.Budget = 60000;
        request.EstimatedCost = 65000;

        var result = await CreateService(dbContext).ValidateAsync(request, user.Id);

        var issue = Assert.Single(result.Response!.Issues);
        Assert.Equal(ValidationIssueType.BudgetExceeded, issue.IssueType);
        Assert.Contains("5000.00", issue.Message);
        Assert.Equal(1, result.Response.BlockingIssueCount);
    }

    [Fact]
    public async Task ValidateAsync_WithHighActiveAlert_CreatesWarningAndRaisesRisk()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        AddAlert(dbContext, user.Id, TravelAlertSeverity.High, TravelAlertStatus.Active, "Kandy");

        var result = await CreateService(dbContext).ValidateAsync(CreateRequest(), user.Id);

        var issue = Assert.Single(result.Response!.Issues);
        Assert.Equal(ValidationIssueType.TravelAlert, issue.IssueType);
        Assert.Equal(ValidationIssueSeverity.High, issue.Severity);
        Assert.False(issue.IsBlocking);
        Assert.Equal(ValidationOverallStatus.Warning, result.Response.OverallStatus);
        Assert.Equal(ValidationRiskLevel.High, result.Response.RiskLevel);
        Assert.True(result.Response.IsFeasible);
    }

    [Fact]
    public async Task ValidateAsync_WithCriticalActiveAlert_IsInvalidAndBlocking()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        AddAlert(dbContext, user.Id, TravelAlertSeverity.Critical, TravelAlertStatus.Active, "Kandy");

        var result = await CreateService(dbContext).ValidateAsync(CreateRequest(), user.Id);

        var issue = Assert.Single(result.Response!.Issues);
        Assert.Equal(ValidationIssueType.TravelAlert, issue.IssueType);
        Assert.True(issue.IsBlocking);
        Assert.Equal(ValidationRiskLevel.Critical, result.Response.RiskLevel);
        Assert.Equal(ValidationOverallStatus.Invalid, result.Response.OverallStatus);
        Assert.False(result.Response.IsFeasible);
    }

    [Fact]
    public async Task ValidateAsync_WithUnrelatedDistrictAlert_DoesNotCreateAlertIssue()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        AddAlert(dbContext, user.Id, TravelAlertSeverity.Critical, TravelAlertStatus.Active, "Colombo");

        var result = await CreateService(dbContext).ValidateAsync(CreateRequest(), user.Id);

        Assert.DoesNotContain(result.Response!.Issues, issue => issue.IssueType == ValidationIssueType.TravelAlert);
    }

    [Fact]
    public async Task ValidateAsync_WithInactiveOrCancelledAlert_IgnoresAlerts()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        AddAlert(dbContext, user.Id, TravelAlertSeverity.Critical, TravelAlertStatus.Draft, "Kandy");
        AddAlert(dbContext, user.Id, TravelAlertSeverity.Critical, TravelAlertStatus.Cancelled, "Kandy");

        var result = await CreateService(dbContext).ValidateAsync(CreateRequest(), user.Id);

        Assert.DoesNotContain(result.Response!.Issues, issue => issue.IssueType == ValidationIssueType.TravelAlert);
    }

    [Fact]
    public async Task ValidateAsync_PersistsResultAndIssues_AndGetIsOwnerScoped()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var otherUser = AddUser(dbContext);
        var service = CreateService(dbContext);

        var created = await service.ValidateAsync(CreateRequest(), user.Id);
        var loaded = await service.GetByIdAsync(created.Response!.Id, user.Id);
        var hidden = await service.GetByIdAsync(created.Response.Id, otherUser.Id);

        Assert.NotNull(loaded);
        Assert.Equal(created.Response.Id, loaded!.Id);
        Assert.Null(hidden);
        Assert.Single(dbContext.ValidationResults);
    }

    private static ItineraryValidationService CreateService(ApplicationDbContext dbContext) =>
        new(dbContext);

    private static ItineraryValidationRequest CreateRequest() => new()
    {
        TripReference = "trip-001",
        Items =
        {
            new ItineraryItemRequest
            {
                Reference = "item-1",
                Title = "Kandy Lake",
                District = "Kandy",
                StartDateTime = Utc(9, 0),
                EndDateTime = Utc(10, 0)
            }
        }
    };

    private static DateTime Utc(int hour, int minute = 0) =>
        new(2026, 9, 21, hour, minute, 0, DateTimeKind.Utc);

    private static User AddUser(ApplicationDbContext dbContext)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Validation",
            LastName = "Tester",
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return user;
    }

    private static void AddAlert(
        ApplicationDbContext dbContext,
        Guid creatorId,
        TravelAlertSeverity severity,
        TravelAlertStatus status,
        string district)
    {
        dbContext.TravelAlerts.Add(new TravelAlert
        {
            Id = Guid.NewGuid(),
            Title = $"{severity} alert",
            Description = "Test alert",
            AlertType = TravelAlertType.Weather,
            Severity = severity,
            District = district,
            StartDateTime = Utc(8, 30),
            EndDateTime = Utc(10, 30),
            Status = status,
            CreatedByUserId = creatorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
