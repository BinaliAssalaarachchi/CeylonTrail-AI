using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class TravelIntelligenceServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_MapsTrustedValidationStateToAgentRequest()
    {
        var validation = CreateValidation();
        string? requestJson = null;
        var handler = new RecordingHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse(CreateAgentResponse(validation));
        });
        var service = CreateService(handler);

        var result = await service.AnalyzeAsync(validation);

        using var document = JsonDocument.Parse(requestJson!);
        Assert.Equal(validation.Id.ToString(), document.RootElement.GetProperty("validationResultId").GetString());
        Assert.Equal("Invalid", document.RootElement.GetProperty("overallStatus").GetString());
        Assert.Equal("Critical", document.RootElement.GetProperty("riskLevel").GetString());
        Assert.Equal("TRAVEL_ALERT_AFFECTS_ITINERARY", document.RootElement
            .GetProperty("issues")[0].GetProperty("ruleCode").GetString());
        Assert.Equal(validation.Id, result.ValidationResultId);
    }

    [Fact]
    public async Task AnalyzeAsync_MapsOwnerScopedItineraryContextToAgentRequest()
    {
        await using var dbContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var ownerId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var attractionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = ownerId,
            FirstName = "Test",
            LastName = "Tourist",
            Email = $"{ownerId}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        dbContext.Categories.Add(new Category { Id = categoryId, Name = $"Category-{categoryId}" });
        dbContext.Attractions.Add(new Attraction
        {
            Id = attractionId,
            ProviderId = Guid.NewGuid(),
            CategoryId = categoryId,
            Name = "Kandy Lake",
            Description = "Lake",
            District = "Kandy",
            Address = "Kandy",
            Price = 10,
            Status = "Approved",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        dbContext.Trips.Add(new Trip
        {
            Id = tripId,
            TouristId = ownerId,
            Name = "Kandy Trip",
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 22),
            Budget = 100,
            Status = TripStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        dbContext.ItineraryItems.Add(new ItineraryItem
        {
            Id = itemId,
            AttractionId = attractionId,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            EstimatedCost = 25,
            ItineraryDay = new ItineraryDay
            {
                DayNumber = 1,
                Date = new DateOnly(2026, 9, 21),
                Itinerary = new Itinerary
                {
                    TripId = tripId,
                    Status = ItineraryStatus.Generated,
                    TotalEstimatedCost = 25,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            }
        });
        await dbContext.SaveChangesAsync();

        var validation = CreateValidation();
        validation.CreatedByUserId = ownerId;
        validation.TripReference = tripId.ToString();
        string? requestJson = null;
        var handler = new RecordingHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse(CreateAgentResponse(validation));
        });

        var service = new TravelIntelligenceService(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8001/") },
            NullLogger<TravelIntelligenceService>.Instance,
            dbContext);

        await service.AnalyzeAsync(validation);

        using var document = JsonDocument.Parse(requestJson!);
        var item = Assert.Single(document.RootElement.GetProperty("itineraryItems").EnumerateArray());
        Assert.Equal(itemId.ToString(), item.GetProperty("itemReference").GetString());
        Assert.Equal("Kandy Lake", item.GetProperty("title").GetString());
        Assert.Equal("Kandy", item.GetProperty("district").GetString());
        Assert.Equal(25, item.GetProperty("estimatedCost").GetDecimal());
    }

    [Fact]
    public async Task AnalyzeAsync_WhenInvalidResponseSaysProceed_UsesSafeFallback()
    {
        var validation = CreateValidation();
        var unsafeResponse = CreateAgentResponse(validation);
        unsafeResponse.RecommendedAction = TravelIntelligenceAction.Proceed;
        unsafeResponse.IsFeasible = true;
        var service = CreateService(new RecordingHandler(_ => Task.FromResult(JsonResponse(unsafeResponse))));

        var result = await service.AnalyzeAsync(validation);

        Assert.Equal(TravelIntelligenceAction.Reschedule, result.RecommendedAction);
        Assert.False(result.IsFeasible);
        Assert.True(result.RequiresHumanApproval);
        Assert.True(result.Execution.UsedFallback);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenResponseIdDoesNotMatch_UsesSafeFallback()
    {
        var validation = CreateValidation();
        var mismatched = CreateAgentResponse(validation);
        mismatched.ValidationResultId = Guid.NewGuid();
        var service = CreateService(new RecordingHandler(_ => Task.FromResult(JsonResponse(mismatched))));

        var result = await service.AnalyzeAsync(validation);

        Assert.Equal(validation.Id, result.ValidationResultId);
        Assert.True(result.Execution.UsedFallback);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenBlockingResponseDoesNotRequireApproval_UsesSafeFallback()
    {
        var validation = CreateValidation();
        var unsafeResponse = CreateAgentResponse(validation);
        unsafeResponse.RequiresHumanApproval = false;
        var service = CreateService(new RecordingHandler(_ => Task.FromResult(JsonResponse(unsafeResponse))));

        var result = await service.AnalyzeAsync(validation);

        Assert.True(result.RequiresHumanApproval);
        Assert.True(result.Execution.UsedFallback);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenBlockingResponseContainsProceedAlternative_UsesSafeFallback()
    {
        var validation = CreateValidation();
        var unsafeResponse = CreateAgentResponse(validation);
        unsafeResponse.Alternatives =
        [
            new TravelIntelligenceAlternativeRecommendation
            {
                AlternativeId = "unsafe",
                Action = TravelIntelligenceAction.Proceed,
                AffectedItemReferences = ["item-1"],
                Rationale = "Unsafe test alternative",
                SafetyStatus = TravelIntelligenceSafetyStatus.ConditionallySafe,
                RequiresHumanApproval = false
            }
        ];
        var service = CreateService(new RecordingHandler(_ => Task.FromResult(JsonResponse(unsafeResponse))));

        var result = await service.AnalyzeAsync(validation);

        Assert.True(result.Execution.UsedFallback);
        Assert.DoesNotContain(result.Alternatives, alternative =>
            alternative.Action == TravelIntelligenceAction.Proceed);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenPythonServiceUnavailable_ReturnsSafeFallback()
    {
        var validation = CreateValidation();
        var handler = new RecordingHandler(_ => throw new HttpRequestException("offline"));
        var service = CreateService(handler);

        var result = await service.AnalyzeAsync(validation);

        Assert.Equal(TravelIntelligenceAction.Reschedule, result.RecommendedAction);
        Assert.True(result.Execution.UsedFallback);
        Assert.Contains("unavailable", result.Execution.FallbackReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_DeserializesRichExecutionMetadata()
    {
        var validation = CreateValidation();
        var workflowId = Guid.NewGuid();
        var response = CreateAgentResponse(validation);
        response.Execution = new TravelIntelligenceExecutionMetadata
        {
            AgentName = "TravelIntelligenceValidationAgent",
            AgentVersion = "1.1",
            ValidationResultId = validation.Id,
            Provider = "gemini",
            UsedFallback = false,
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
            ExecutedStepId = "review_validation",
            ExecutedToolName = "summarize_validation",
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
        };
        var service = CreateService(new RecordingHandler(_ => Task.FromResult(JsonResponse(response))));

        var result = await service.AnalyzeAsync(validation);

        Assert.Equal(workflowId, result.Execution.WorkflowId);
        Assert.Equal("travel_intelligence_assessment", result.Execution.Objective.Name);
        Assert.Equal("system", result.Execution.Objective.Source);
        var planStep = Assert.Single(result.Execution.InvestigationPlan.Steps);
        Assert.Equal("review_validation", planStep.StepId);
        Assert.Equal("summarize_validation", planStep.ToolName);
        var executedStep = Assert.Single(result.Execution.ExecutedSteps);
        Assert.Equal(12, executedStep.DurationMs);
        Assert.Equal("Validation reviewed safely.", executedStep.ResultSummary);
        Assert.True(result.Execution.ProviderAttempted);
        Assert.True(result.Execution.ProviderSucceeded);
        Assert.Equal("gemini", result.Execution.ProviderName);
        Assert.Equal("gemini-test", result.Execution.ModelName);
        Assert.Equal(20, result.Execution.ProviderLatencyMs);
        Assert.Equal(1, result.Execution.ProviderAttemptCount);
        Assert.True(result.Execution.ToolSelectionProviderAttempted);
        Assert.Equal(["summarize_validation"], result.Execution.SelectedToolNames);
        Assert.Equal(["delete_booking"], result.Execution.RejectedToolNames);
        Assert.True(result.Execution.ToolSelectionFallbackUsed);
        Assert.Equal("provider requested an unknown tool", result.Execution.ToolSelectionFallbackReason);
        Assert.Equal(1, result.Execution.SelectionAttemptCount);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenResponseIsMalformed_ReturnsSafeFallback()
    {
        var validation = CreateValidation();
        var handler = new RecordingHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{not-json")
            }));
        var service = CreateService(handler);

        var result = await service.AnalyzeAsync(validation);

        Assert.Equal(validation.Id, result.ValidationResultId);
        Assert.True(result.Execution.UsedFallback);
        Assert.Contains("malformed", result.Execution.FallbackReason, StringComparison.OrdinalIgnoreCase);
    }

    private static TravelIntelligenceService CreateService(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8001/") },
            NullLogger<TravelIntelligenceService>.Instance);

    private static ItineraryValidationResponse CreateValidation()
    {
        var id = Guid.NewGuid();
        return new ItineraryValidationResponse
        {
            Id = id,
            TripReference = "trip-001",
            OverallStatus = ValidationOverallStatus.Invalid,
            RiskLevel = ValidationRiskLevel.Critical,
            IsFeasible = false,
            TotalIssueCount = 1,
            BlockingIssueCount = 1,
            Issues =
            [
                new ValidationIssueResponse
                {
                    Id = Guid.NewGuid(),
                    IssueType = ValidationIssueType.TravelAlert,
                    Severity = ValidationIssueSeverity.Critical,
                    Message = "Critical alert",
                    RuleCode = "TRAVEL_ALERT_AFFECTS_ITINERARY",
                    IsBlocking = true,
                    RelatedDistrict = "Kandy",
                    RelatedItemReference = "item-1",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
    }

    private static TravelIntelligenceResponse CreateAgentResponse(
        ItineraryValidationResponse validation) => new()
        {
            Summary = "Deterministic validation is Invalid.",
            RiskLevel = validation.RiskLevel,
            RecommendedAction = TravelIntelligenceAction.Reschedule,
            RequiresHumanApproval = true,
            AffectedItemReferences = ["item-1"],
            ValidationResultId = validation.Id,
            IsFeasible = validation.IsFeasible,
            Recommendations =
            [
                new TravelIntelligenceRecommendation
                {
                    Action = TravelIntelligenceAction.Reschedule,
                    Explanation = "Review the critical alert.",
                    AffectedItemReferences = ["item-1"]
                }
            ],
            Execution = new TravelIntelligenceExecutionMetadata
            {
                AgentName = "TravelIntelligenceValidationAgent",
                AgentVersion = "1.0",
                ValidationResultId = validation.Id,
                Provider = "deterministic-fallback",
                UsedFallback = true,
                ExecutionStatus = "Fallback"
            }
        };

    private static HttpResponseMessage JsonResponse(TravelIntelligenceResponse response) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                }))
        };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
