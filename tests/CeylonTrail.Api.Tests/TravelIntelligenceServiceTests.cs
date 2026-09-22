using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
