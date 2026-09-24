using System.Text.Json;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class TravelIntelligenceExecutionPersistenceServiceTests
{
    [Fact]
    public async Task PersistAsync_PersistsExecutionStepsMetadataAndSnapshots()
    {
        await using var dbContext = CreateContext();
        var (validation, userId) = SeedTrustedInputs(dbContext);
        var response = CreateResponse(validation, Guid.NewGuid(), requiresApproval: false);
        var service = CreateService(dbContext);

        var result = await service.PersistAsync(validation, userId, response);

        Assert.True(result.Succeeded);
        var execution = await dbContext.TravelIntelligenceExecutions
            .Include(candidate => candidate.Steps)
            .SingleAsync();
        Assert.Equal(response.Execution.WorkflowId.ToString(), execution.WorkflowId);
        Assert.Equal("travel_intelligence_assessment", execution.ObjectiveName);
        Assert.Equal("gemini", execution.ProviderName);
        Assert.Equal("gemini-test", execution.ModelName);
        Assert.True(execution.ProviderAttempted);
        Assert.True(execution.ProviderSucceeded);
        Assert.True(execution.ToolSelectionProviderAttempted);
        Assert.Equal(["summarize_validation"], Deserialize<string[]>(execution.SelectedToolNamesJson));
        Assert.Equal(["delete_booking"], Deserialize<string[]>(execution.RejectedToolNamesJson));
        Assert.Equal("Provider explanation", Deserialize<TravelIntelligenceRecommendation[]>(execution.RecommendationsJson)[0].Explanation);
        Assert.Equal(2, execution.Steps.Count);
        Assert.Equal([1, 2], execution.Steps.OrderBy(step => step.Sequence).Select(step => step.Sequence));
        Assert.Equal(TravelIntelligenceExecutionStepStatus.Completed, execution.Steps.Single(step => step.Sequence == 1).Status);
        Assert.Equal(TravelIntelligenceExecutionStepStatus.Pending, execution.Steps.Single(step => step.Sequence == 2).Status);
    }

    [Fact]
    public async Task PersistAsync_PersistsFallbackAndLinksRequiredApproval()
    {
        await using var dbContext = CreateContext();
        var (validation, userId) = SeedTrustedInputs(dbContext, critical: true);
        var response = CreateResponse(validation, Guid.NewGuid(), requiresApproval: true);
        response.Execution.Provider = "deterministic-fallback";
        response.Execution.ProviderSucceeded = false;
        response.Execution.UsedFallback = true;
        response.Execution.FallbackReason = "api key secret must not be persisted";
        response.RecommendedAction = TravelIntelligenceAction.Reschedule;
        var service = CreateService(dbContext);

        var result = await service.PersistAsync(validation, userId, response);

        Assert.True(result.Succeeded);
        var execution = await dbContext.TravelIntelligenceExecutions.SingleAsync();
        Assert.True(execution.UsedFallback);
        Assert.False(execution.ProviderSucceeded);
        Assert.Equal("Provider failure", execution.FallbackReason);
        var approval = await dbContext.ApprovalRequests.SingleAsync();
        Assert.Equal(execution.Id, approval.TravelIntelligenceExecutionId);
        Assert.True(result.ApprovalRequest is not null);
    }

    [Fact]
    public async Task PersistAsync_DoesNotCreateApprovalWhenApprovalIsNotRequired()
    {
        await using var dbContext = CreateContext();
        var (validation, userId) = SeedTrustedInputs(dbContext);
        var service = CreateService(dbContext);

        var result = await service.PersistAsync(
            validation,
            userId,
            CreateResponse(validation, Guid.NewGuid(), requiresApproval: false));

        Assert.True(result.Succeeded);
        Assert.Empty(await dbContext.ApprovalRequests.ToListAsync());
        Assert.Null(result.ApprovalRequest);
    }

    [Fact]
    public async Task PersistAsync_ReusesPendingApprovalAndPointsItToLatestExecution()
    {
        await using var dbContext = CreateContext();
        var (validation, userId) = SeedTrustedInputs(dbContext, critical: true);
        var service = CreateService(dbContext);

        var first = await service.PersistAsync(
            validation,
            userId,
            CreateResponse(validation, Guid.NewGuid(), requiresApproval: true));
        var second = await service.PersistAsync(
            validation,
            userId,
            CreateResponse(validation, Guid.NewGuid(), requiresApproval: true));

        var approval = await dbContext.ApprovalRequests.SingleAsync();
        Assert.Equal(second.ExecutionId, approval.TravelIntelligenceExecutionId);
        Assert.NotEqual(first.ExecutionId, second.ExecutionId);
    }

    [Fact]
    public async Task PersistAsync_DoesNotDuplicateSameWorkflowAndKeepsDistinctWorkflows()
    {
        await using var dbContext = CreateContext();
        var (validation, userId) = SeedTrustedInputs(dbContext);
        var service = CreateService(dbContext);
        var workflowId = Guid.NewGuid();
        var response = CreateResponse(validation, workflowId, requiresApproval: false);

        var first = await service.PersistAsync(validation, userId, response);
        var duplicate = await service.PersistAsync(validation, userId, response);
        var independent = await service.PersistAsync(
            validation,
            userId,
            CreateResponse(validation, Guid.NewGuid(), requiresApproval: false));

        Assert.Equal(first.ExecutionId, duplicate.ExecutionId);
        Assert.NotEqual(first.ExecutionId, independent.ExecutionId);
        Assert.Equal(2, await dbContext.TravelIntelligenceExecutions.CountAsync());
    }

    [Fact]
    public async Task PersistAsync_RejectsUnboundedExecutionCollections()
    {
        await using var dbContext = CreateContext();
        var (validation, userId) = SeedTrustedInputs(dbContext);
        var response = CreateResponse(validation, Guid.NewGuid(), requiresApproval: false);
        response.Alternatives = Enumerable.Range(0, 4)
            .Select(index => new TravelIntelligenceAlternativeRecommendation
            {
                AlternativeId = $"alternative-{index}",
                Action = TravelIntelligenceAction.ManualReview,
                Rationale = "Review manually.",
                SafetyStatus = TravelIntelligenceSafetyStatus.ManualReviewRequired,
                RequiresHumanApproval = true
            })
            .ToList();
        var service = CreateService(dbContext);

        var result = await service.PersistAsync(validation, userId, response);

        Assert.False(result.Succeeded);
        Assert.Empty(await dbContext.TravelIntelligenceExecutions.ToListAsync());
    }

    [Fact]
    public void ExecutionPersistenceEntitiesDoNotExposeRawProviderOrSecretFields()
    {
        var names = typeof(TravelIntelligenceExecution)
            .GetProperties()
            .Select(property => property.Name)
            .Concat(typeof(TravelIntelligenceExecutionStep)
                .GetProperties()
                .Select(property => property.Name))
            .ToArray();

        Assert.DoesNotContain(names, name => name.Contains("Prompt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("RequestBody", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("ResponseBody", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Jwt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    private static TravelIntelligenceExecutionPersistenceService CreateService(
        ApplicationDbContext dbContext) =>
        new(
            dbContext,
            new ApprovalRequestService(dbContext),
            NullLogger<TravelIntelligenceExecutionPersistenceService>.Instance);

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (ItineraryValidationResponse Validation, Guid UserId) SeedTrustedInputs(
        ApplicationDbContext dbContext,
        bool critical = false)
    {
        var userId = Guid.NewGuid();
        var validationId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            FirstName = "Test",
            LastName = "Tourist",
            Email = $"{userId}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        dbContext.ValidationResults.Add(new ValidationResult
        {
            Id = validationId,
            CreatedByUserId = userId,
            OverallStatus = critical ? ValidationOverallStatus.Invalid : ValidationOverallStatus.Valid,
            RiskLevel = critical ? ValidationRiskLevel.Critical : ValidationRiskLevel.Low,
            IsFeasible = !critical,
            TotalIssueCount = critical ? 1 : 0,
            BlockingIssueCount = critical ? 1 : 0,
            CreatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();

        return (new ItineraryValidationResponse
        {
            Id = validationId,
            CreatedByUserId = userId,
            OverallStatus = critical ? ValidationOverallStatus.Invalid : ValidationOverallStatus.Valid,
            RiskLevel = critical ? ValidationRiskLevel.Critical : ValidationRiskLevel.Low,
            IsFeasible = !critical,
            TotalIssueCount = critical ? 1 : 0,
            BlockingIssueCount = critical ? 1 : 0,
            Issues = critical
                ? [new ValidationIssueResponse
                {
                    Id = Guid.NewGuid(),
                    IssueType = ValidationIssueType.TravelAlert,
                    Severity = ValidationIssueSeverity.Critical,
                    RuleCode = "TRAVEL_ALERT_AFFECTS_ITINERARY",
                    Message = "Critical alert",
                    IsBlocking = true,
                    RelatedItemReference = "item-1"
                }]
                : []
        }, userId);
    }

    private static TravelIntelligenceResponse CreateResponse(
        ItineraryValidationResponse validation,
        Guid workflowId,
        bool requiresApproval) => new()
        {
            Summary = "Safe normalized recommendation.",
            RiskLevel = validation.RiskLevel,
            RecommendedAction = requiresApproval
                ? TravelIntelligenceAction.Reschedule
                : TravelIntelligenceAction.Proceed,
            RequiresHumanApproval = requiresApproval,
            ValidationResultId = validation.Id,
            IsFeasible = validation.IsFeasible,
            Recommendations =
            [
                new TravelIntelligenceRecommendation
                {
                    Action = requiresApproval
                        ? TravelIntelligenceAction.Reschedule
                        : TravelIntelligenceAction.Proceed,
                    Explanation = "Provider explanation",
                    AffectedItemReferences = ["item-1"]
                }
            ],
            Execution = new TravelIntelligenceExecutionMetadata
            {
                AgentName = "TravelIntelligenceValidationAgent",
                AgentVersion = "1.1",
                ValidationResultId = validation.Id,
                Provider = "gemini",
                ExecutionStatus = "Completed",
                WorkflowId = workflowId,
                Objective = new TravelIntelligenceObjective
                {
                    Name = "travel_intelligence_assessment",
                    Description = "Assess validated travel risks.",
                    Source = "system"
                },
                InvestigationPlan = new TravelIntelligenceInvestigationPlan
                {
                    Steps =
                    [
                        new TravelIntelligenceInvestigationStep
                        {
                            StepId = "review_validation",
                            Name = "Review validation",
                            Purpose = "Read authoritative validation state.",
                            ToolName = "summarize_validation",
                            Status = TravelIntelligenceStepStatus.Completed
                        },
                        new TravelIntelligenceInvestigationStep
                        {
                            StepId = "finalize_recommendation",
                            Name = "Finalize recommendation",
                            Purpose = "Prepare safe advisory output.",
                            Status = TravelIntelligenceStepStatus.Pending
                        }
                    ]
                },
                ExecutedSteps =
                [
                    new TravelIntelligenceExecutedStep
                    {
                        StepId = "review_validation",
                        ToolName = "summarize_validation",
                        Status = TravelIntelligenceStepStatus.Completed,
                        DurationMs = 12,
                        ResultSummary = "Validation reviewed safely."
                    }
                ],
                DurationMs = 34,
                ResultSummary = "Investigation completed.",
                ModelName = "gemini-test",
                ProviderAttempted = true,
                ProviderSucceeded = true,
                ProviderName = "gemini",
                ProviderLatencyMs = 20,
                ProviderAttemptCount = 1,
                ToolSelectionProviderAttempted = true,
                SelectedToolNames = ["summarize_validation"],
                RejectedToolNames = ["delete_booking"],
                ToolSelectionFallbackUsed = true,
                ToolSelectionFallbackReason = "provider requested an unknown tool",
                SelectionAttemptCount = 1
            }
        };

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        })!;
}
