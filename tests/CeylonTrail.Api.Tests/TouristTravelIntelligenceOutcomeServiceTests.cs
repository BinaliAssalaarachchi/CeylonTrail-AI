using System.Text.Json;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.DTOs.TravelIntelligence;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class TouristTravelIntelligenceOutcomeServiceTests
{
    [Fact]
    public async Task TouristRetrievesLatestOutcomeForOwnTrip()
    {
        await using var db = CreateDbContext();
        var touristId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        AddTrip(db, touristId, tripId);
        AddExecution(db, touristId, tripId, DateTime.UtcNow.AddMinutes(-1), ApprovalRequestStatus.Pending);
        var latest = AddExecution(db, touristId, tripId, DateTime.UtcNow, ApprovalRequestStatus.Approved);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLatestAsync(touristId, tripId);

        Assert.NotNull(result);
        Assert.Equal(latest.Id, result!.ExecutionId);
        Assert.Equal(tripId, result.TripId);
        Assert.Equal(TouristReviewStatus.Approved, result.ReviewStatus);
        Assert.Equal(ApprovalDecisionType.Approved, result.Decision);
    }

    [Fact]
    public async Task OtherTouristAndMissingTripReturnNotFoundResult()
    {
        await using var db = CreateDbContext();
        var owner = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        AddTrip(db, owner, tripId);
        AddExecution(db, owner, tripId, DateTime.UtcNow, null);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Assert.Null(await service.GetLatestAsync(Guid.NewGuid(), tripId));
        Assert.Null(await service.GetLatestAsync(owner, Guid.NewGuid()));
    }

    [Fact]
    public async Task OwnedTripWithoutExecutionReturnsNoAssessmentResult()
    {
        await using var db = CreateDbContext();
        var touristId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        AddTrip(db, touristId, tripId);
        await db.SaveChangesAsync();

        Assert.Null(await CreateService(db).GetLatestAsync(touristId, tripId));
    }

    [Fact]
    public async Task ExactTripReferenceAndDeterministicOrderingSelectLatestExecution()
    {
        await using var db = CreateDbContext();
        var touristId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        AddTrip(db, touristId, tripId);
        AddExecution(db, touristId, tripId, DateTime.UtcNow.AddHours(-1), null);
        var latest = AddExecution(db, touristId, tripId, DateTime.UtcNow, null);
        AddExecution(db, touristId, Guid.NewGuid(), DateTime.UtcNow.AddDays(1), null);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLatestAsync(touristId, tripId);

        Assert.Equal(latest.Id, result!.ExecutionId);
    }

    [Theory]
    [InlineData(ApprovalRequestStatus.Pending, TouristReviewStatus.Pending)]
    [InlineData(ApprovalRequestStatus.Approved, TouristReviewStatus.Approved)]
    [InlineData(ApprovalRequestStatus.Rejected, TouristReviewStatus.Rejected)]
    public async Task ApprovalStatusIsMappedFromExplicitExecutionApproval(
        ApprovalRequestStatus approvalStatus,
        TouristReviewStatus expectedStatus)
    {
        await using var db = CreateDbContext();
        var touristId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        AddTrip(db, touristId, tripId);
        var execution = AddExecution(db, touristId, tripId, DateTime.UtcNow, approvalStatus);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLatestAsync(touristId, tripId);

        Assert.Equal(expectedStatus, result!.ReviewStatus);
        Assert.Equal(approvalStatus == ApprovalRequestStatus.Pending ? null : approvalStatus == ApprovalRequestStatus.Approved ? ApprovalDecisionType.Approved : ApprovalDecisionType.Rejected, result.Decision);
    }

    [Fact]
    public async Task NoHumanApprovalMapsToNotRequired()
    {
        await using var db = CreateDbContext();
        var touristId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        AddTrip(db, touristId, tripId);
        var execution = AddExecution(db, touristId, tripId, DateTime.UtcNow, null);
        execution.RequiresHumanApproval = false;
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLatestAsync(touristId, tripId);

        Assert.Equal(TouristReviewStatus.NotRequired, result!.ReviewStatus);
        Assert.Null(result.Decision);
    }

    [Fact]
    public void TouristDtoExcludesInternalAndSensitiveFields()
    {
        var names = typeof(TouristTravelIntelligenceOutcomeResponse).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain(names, name => name.Contains("Workflow") ||
            name.Contains("Agent") || name.Contains("Provider") || name.Contains("Model") ||
            name.Contains("Tool") || name.Contains("Fallback") || name.Contains("Prompt") ||
            name.Contains("Payload") || name.Contains("Token") || name.Contains("Secret") ||
            name.Contains("Reviewer") || name.Contains("Comment") || name.Contains("Identity"));
        Assert.DoesNotContain("workflowId", JsonSerializer.Serialize(new TouristTravelIntelligenceOutcomeResponse()));
    }

    private static TouristTravelIntelligenceOutcomeService CreateService(ApplicationDbContext db) =>
        new(db, NullLogger<TouristTravelIntelligenceOutcomeService>.Instance);

    private static void AddTrip(ApplicationDbContext db, Guid touristId, Guid tripId) =>
        db.Trips.Add(new Trip
        {
            Id = tripId,
            TouristId = touristId,
            Name = "Test trip",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 5),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

    private static TravelIntelligenceExecution AddExecution(
        ApplicationDbContext db,
        Guid touristId,
        Guid tripId,
        DateTime startedAt,
        ApprovalRequestStatus? approvalStatus)
    {
        var validationId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        db.ValidationResults.Add(new ValidationResult
        {
            Id = validationId,
            TripReference = tripId.ToString(),
            CreatedByUserId = touristId,
            CreatedAt = startedAt,
            RiskLevel = ValidationRiskLevel.High,
            IsFeasible = false
        });
        var execution = new TravelIntelligenceExecution
        {
            Id = executionId,
            WorkflowId = executionId.ToString(),
            ValidationResultId = validationId,
            RequestedByUserId = touristId,
            AgentName = "internal-agent",
            AgentVersion = "1",
            ObjectiveName = "internal objective",
            ObjectiveDescription = "internal description",
            ObjectiveSource = "validation",
            ExecutionStatus = "Completed",
            RiskLevel = ValidationRiskLevel.High,
            IsFeasible = false,
            RecommendedAction = ApprovalRecommendedAction.Reschedule,
            Summary = "Safe summary",
            RequiresHumanApproval = approvalStatus.HasValue,
            Provider = "internal-provider",
            ProviderName = "internal-provider",
            ModelName = "internal-model",
            ProviderAttempted = true,
            ProviderSucceeded = false,
            UsedFallback = true,
            FallbackReason = "internal provider detail",
            SelectedToolNamesJson = "[\"internal-tool\"]",
            RejectedToolNamesJson = "[]",
            RecommendationsJson = "[]",
            AffectedItemsJson = "[]",
            AlternativesJson = "[]",
            SafeWindowsJson = "[]",
            StartedAt = startedAt,
            CompletedAt = startedAt.AddSeconds(1),
            CreatedAt = startedAt,
            ResultSummary = "Safe result"
        };
        db.TravelIntelligenceExecutions.Add(execution);

        if (approvalStatus.HasValue)
        {
            var approvalId = Guid.NewGuid();
            db.ApprovalRequests.Add(new ApprovalRequest
            {
                Id = approvalId,
                ValidationResultId = validationId,
                TravelIntelligenceExecutionId = executionId,
                RequestedByUserId = touristId,
                Status = approvalStatus.Value,
                RecommendedAction = ApprovalRecommendedAction.Reschedule,
                RiskLevel = ValidationRiskLevel.High,
                Summary = "Safe summary",
                CreatedAt = startedAt,
                UpdatedAt = startedAt,
                Decision = approvalStatus == ApprovalRequestStatus.Pending
                    ? null
                    : new ApprovalDecision
                    {
                        Id = Guid.NewGuid(),
                        ApprovalRequestId = approvalId,
                        DecidedByUserId = Guid.NewGuid(),
                        Decision = approvalStatus == ApprovalRequestStatus.Approved
                            ? ApprovalDecisionType.Approved
                            : ApprovalDecisionType.Rejected,
                        DecidedAt = startedAt.AddMinutes(1),
                        Comment = "Reviewer-only comment"
                    }
            });
        }

        return execution;
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
