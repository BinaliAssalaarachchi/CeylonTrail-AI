using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Controllers;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.DTOs.TravelIntelligence;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class TravelIntelligenceExecutionQueryServiceTests
{
    [Fact]
    public async Task OwnerCanListAndRetrieveOnlyOwnExecutionWithTypedSnapshotsAndOrderedSteps()
    {
        var owner = Guid.NewGuid();
        var validationId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        await using var db = CreateDbContext();
        Seed(db, owner, validationId, executionId, includeApproval: true);
        var service = CreateService(db);

        var page = await service.ListForOwnerAsync(validationId, owner, new TravelIntelligenceExecutionQuery { PageSize = 200 });
        var detail = await service.GetForOwnerAsync(validationId, executionId, owner);

        Assert.NotNull(page);
        Assert.Equal(100, page!.PageSize);
        Assert.Single(page.Items);
        Assert.NotNull(detail);
        Assert.Equal([1, 2], detail!.Steps.Select(step => step.Sequence));
        Assert.Equal("check-alerts", detail.Steps[0].StepId);
        Assert.NotNull(detail.SharedTrace);
        Assert.Equal("TravelIntelligence", detail.SharedTrace!.Agent);
        Assert.Equal(["check-alerts"], detail.SharedTrace.Steps.Select(step => step.Tool));
        Assert.Equal("recommend", detail.Recommendations.Single().Explanation);
        Assert.Equal("Approved", detail.Approval!.Decision!.Decision.ToString());
    }

    [Fact]
    public async Task OwnerCannotAccessOtherTouristsExecutionOrMismatchedValidation()
    {
        var owner = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var validationId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        await using var db = CreateDbContext();
        Seed(db, otherOwner, validationId, executionId, includeApproval: false);
        var service = CreateService(db);

        Assert.Null(await service.ListForOwnerAsync(validationId, owner, new TravelIntelligenceExecutionQuery()));
        Assert.Null(await service.GetForOwnerAsync(Guid.NewGuid(), executionId, otherOwner));
        Assert.NotNull(await service.GetForOwnerAsync(validationId, executionId, otherOwner));
    }

    [Fact]
    public async Task StaffFiltersAndPaginationDoNotChangeSafeDetailShape()
    {
        await using var db = CreateDbContext();
        var validationId = Guid.NewGuid();
        Seed(db, Guid.NewGuid(), validationId, Guid.NewGuid(), includeApproval: false);
        Seed(db, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), includeApproval: false, usedFallback: true);
        var service = CreateService(db);

        var page = await service.ListForStaffAsync(new TravelIntelligenceExecutionQuery
        {
            ValidationResultId = validationId,
            UsedFallback = false,
            Page = 0,
            PageSize = 0
        });

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task ExecutionWithoutApprovalReturnsNullApproval()
    {
        await using var db = CreateDbContext();
        var executionId = Guid.NewGuid();
        Seed(db, Guid.NewGuid(), Guid.NewGuid(), executionId, includeApproval: false);
        var result = await CreateService(db).GetForStaffAsync(executionId);
        Assert.NotNull(result);
        Assert.Null(result!.Approval);
    }

    [Fact]
    public async Task MalformedOptionalSnapshotReturnsEmptyCollection()
    {
        await using var db = CreateDbContext();
        var executionId = Guid.NewGuid();
        Seed(db, Guid.NewGuid(), Guid.NewGuid(), executionId, includeApproval: false);
        db.TravelIntelligenceExecutions.Single().RecommendationsJson = "not-json";
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetForStaffAsync(executionId);
        Assert.NotNull(result);
        Assert.Empty(result!.Recommendations);
    }

    [Fact]
    public void StaffControllerIsRestrictedToCoordinatorAndAdministrator()
    {
        var attribute = typeof(TravelIntelligenceExecutionsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("TravelCoordinator,Administrator", attribute!.Roles);
        Assert.DoesNotContain("TourismProvider", attribute.Roles);
    }

    [Fact]
    public void ResponseDtosContainNoSecretOrRawProviderFields()
    {
        var propertyNames = typeof(TravelIntelligenceExecutionDetailResponse).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(propertyNames, name => name.Contains("Prompt") || name.Contains("Token") || name.Contains("Secret") || name.Contains("Payload") || name.Contains("Authorization") || name.Contains("ConnectionString"));
    }

    private static TravelIntelligenceExecutionQueryService CreateService(ApplicationDbContext db) =>
        new(db, NullLogger<TravelIntelligenceExecutionQueryService>.Instance);

    private static void Seed(ApplicationDbContext db, Guid owner, Guid validationId, Guid executionId, bool includeApproval, bool usedFallback = false)
    {
        var now = DateTime.UtcNow;
        db.Users.Add(new User { Id = owner, IsActive = true, FirstName = "Owner", LastName = "Test", Email = $"{owner}@test.local", PasswordHash = "hash", CreatedAt = now, UpdatedAt = now });
        db.ValidationResults.Add(new ValidationResult { Id = validationId, CreatedByUserId = owner, CreatedAt = now, RiskLevel = ValidationRiskLevel.High, IsFeasible = true });
        db.TravelIntelligenceExecutions.Add(new TravelIntelligenceExecution
        {
            Id = executionId,
            WorkflowId = executionId.ToString(),
            ValidationResultId = validationId,
            RequestedByUserId = owner,
            AgentName = "agent",
            AgentVersion = "1",
            ObjectiveName = "objective",
            ObjectiveDescription = "description",
            ObjectiveSource = "validation",
            ExecutionStatus = "Completed",
            RiskLevel = ValidationRiskLevel.High,
            IsFeasible = true,
            RecommendedAction = ApprovalRecommendedAction.Proceed,
            Summary = "summary",
            RequiresHumanApproval = includeApproval,
            Provider = "gemini",
            ProviderAttempted = true,
            ProviderSucceeded = !usedFallback,
            UsedFallback = usedFallback,
            FallbackReason = usedFallback ? "Provider failure" : null,
            SelectedToolNamesJson = "[\"check-alerts\"]",
            RejectedToolNamesJson = "[]",
            RecommendationsJson = JsonSerializer.Serialize(new[] { new TravelIntelligenceRecommendation { Explanation = "recommend" } }, JsonOptions),
            AffectedItemsJson = "[]",
            AlternativesJson = "[]",
            SafeWindowsJson = "[]",
            StartedAt = now,
            CompletedAt = now,
            ResultSummary = "result",
            CreatedAt = now
        });
        db.TravelIntelligenceExecutionSteps.AddRange(
            new TravelIntelligenceExecutionStep { Id = Guid.NewGuid(), TravelIntelligenceExecutionId = executionId, Sequence = 2, StepId = "recommend", Name = "Recommend", Purpose = "recommend", Status = TravelIntelligenceExecutionStepStatus.Completed, CreatedAt = now },
            new TravelIntelligenceExecutionStep { Id = Guid.NewGuid(), TravelIntelligenceExecutionId = executionId, Sequence = 1, StepId = "check-alerts", Name = "Check", Purpose = "check", ExecutedToolName = "check-alerts", ResultSummary = "Checked authoritative alerts.", DurationMs = 3, Status = TravelIntelligenceExecutionStepStatus.Completed, CreatedAt = now });
        if (includeApproval)
        {
            var approvalId = Guid.NewGuid();
            db.ApprovalRequests.Add(new ApprovalRequest { Id = approvalId, ValidationResultId = validationId, TravelIntelligenceExecutionId = executionId, RequestedByUserId = owner, Status = ApprovalRequestStatus.Approved, RecommendedAction = ApprovalRecommendedAction.Proceed, RiskLevel = ValidationRiskLevel.High, Summary = "approve", CreatedAt = now, UpdatedAt = now, Decision = new ApprovalDecision { Id = Guid.NewGuid(), ApprovalRequestId = approvalId, DecidedByUserId = Guid.NewGuid(), Decision = ApprovalDecisionType.Approved, DecidedAt = now, Comment = "safe" } });
        }
        db.SaveChanges();
    }

    private static ApplicationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
}
