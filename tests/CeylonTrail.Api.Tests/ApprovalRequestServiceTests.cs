using CeylonTrail.Api.Data;
using CeylonTrail.Api.Controllers;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class ApprovalRequestServiceTests
{
    [Fact]
    public async Task CreateOrReusePendingAsync_CreatesSafePendingSnapshot()
    {
        await using var dbContext = CreateDbContext();
        var requester = AddUser(dbContext, UserRole.Tourist);
        var validation = AddValidation(dbContext, requester.Id);
        var service = new ApprovalRequestService(dbContext);

        var result = await service.CreateOrReusePendingAsync(
            validation.Id,
            requester.Id,
            Recommendation(validation.Id));

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalRequestStatus.Pending, result.Response!.Status);
        Assert.Equal(ApprovalRecommendedAction.Reschedule, result.Response.RecommendedAction);
        Assert.Equal(ValidationRiskLevel.Critical, result.Response.RiskLevel);
        Assert.Equal("item-1", result.Response.AffectedItemReferences.Single());
        Assert.Equal(validation.IsFeasible, dbContext.ValidationResults.Single().IsFeasible);
    }

    [Fact]
    public async Task CreateOrReusePendingAsync_ReusesExistingPendingRequest()
    {
        await using var dbContext = CreateDbContext();
        var requester = AddUser(dbContext, UserRole.Tourist);
        var validation = AddValidation(dbContext, requester.Id);
        var service = new ApprovalRequestService(dbContext);

        var first = await service.CreateOrReusePendingAsync(
            validation.Id,
            requester.Id,
            Recommendation(validation.Id));
        var second = await service.CreateOrReusePendingAsync(
            validation.Id,
            requester.Id,
            Recommendation(validation.Id));

        Assert.Equal(first.Response!.Id, second.Response!.Id);
        Assert.Single(dbContext.ApprovalRequests);
    }

    [Fact]
    public async Task DecideAsync_ApprovesAndAuditsAuthenticatedDecisionMaker()
    {
        await using var dbContext = CreateDbContext();
        var requester = AddUser(dbContext, UserRole.Tourist);
        var coordinator = AddUser(dbContext, UserRole.TravelCoordinator);
        var validation = AddValidation(dbContext, requester.Id);
        var service = new ApprovalRequestService(dbContext);
        var created = await service.CreateOrReusePendingAsync(
            validation.Id,
            requester.Id,
            Recommendation(validation.Id));

        var result = await service.DecideAsync(
            created.Response!.Id,
            coordinator.Id,
            ApprovalDecisionType.Approved,
            "Reviewed the critical alert.");

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalRequestStatus.Approved, result.Response!.Status);
        Assert.NotNull(result.Response.Decision);
        Assert.Equal(coordinator.Id, result.Response.Decision!.DecidedByUserId);
        Assert.Equal("Reviewed the critical alert.", result.Response.Decision.Comment);
        Assert.False(dbContext.ValidationResults.Single().IsFeasible);
    }

    [Fact]
    public async Task DecideAsync_RejectsPendingRequest()
    {
        await using var dbContext = CreateDbContext();
        var requester = AddUser(dbContext, UserRole.Tourist);
        var coordinator = AddUser(dbContext, UserRole.TravelCoordinator);
        var validation = AddValidation(dbContext, requester.Id);
        var service = new ApprovalRequestService(dbContext);
        var created = await service.CreateOrReusePendingAsync(
            validation.Id,
            requester.Id,
            Recommendation(validation.Id));

        var result = await service.DecideAsync(
            created.Response!.Id,
            coordinator.Id,
            ApprovalDecisionType.Rejected,
            null);

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalRequestStatus.Rejected, result.Response!.Status);
        Assert.Equal(ApprovalDecisionType.Rejected, result.Response.Decision!.Decision);
    }

    [Fact]
    public async Task DecideAsync_CannotDecideAlreadyApprovedRequest()
    {
        await using var dbContext = CreateDbContext();
        var requester = AddUser(dbContext, UserRole.Tourist);
        var coordinator = AddUser(dbContext, UserRole.TravelCoordinator);
        var validation = AddValidation(dbContext, requester.Id);
        var service = new ApprovalRequestService(dbContext);
        var created = await service.CreateOrReusePendingAsync(
            validation.Id,
            requester.Id,
            Recommendation(validation.Id));

        await service.DecideAsync(
            created.Response!.Id,
            coordinator.Id,
            ApprovalDecisionType.Approved,
            null);
        var second = await service.DecideAsync(
            created.Response.Id,
            coordinator.Id,
            ApprovalDecisionType.Rejected,
            null);

        Assert.False(second.Succeeded);
        Assert.Contains("already", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApprovalRequestsController_IsRestrictedToCoordinatorAndAdministrator()
    {
        var authorization = typeof(ApprovalRequestsController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        Assert.Equal("TravelCoordinator,Administrator", authorization.Roles);
    }

    private static TravelIntelligenceResponse Recommendation(Guid validationId) => new()
    {
        Summary = "Critical travel alert requires human review.",
        RiskLevel = ValidationRiskLevel.Critical,
        RecommendedAction = TravelIntelligenceAction.Reschedule,
        RequiresHumanApproval = true,
        AffectedItemReferences = ["item-1"],
        ValidationResultId = validationId,
        IsFeasible = false
    };

    private static ValidationResult AddValidation(ApplicationDbContext dbContext, Guid userId)
    {
        var validation = new ValidationResult
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = userId,
            OverallStatus = ValidationOverallStatus.Invalid,
            RiskLevel = ValidationRiskLevel.Critical,
            IsFeasible = false,
            TotalIssueCount = 1,
            BlockingIssueCount = 1,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ValidationResults.Add(validation);
        dbContext.SaveChanges();
        return validation;
    }

    private static User AddUser(ApplicationDbContext dbContext, UserRole role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Approval",
            LastName = "Tester",
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            Role = role,
            IsActive = true,
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
