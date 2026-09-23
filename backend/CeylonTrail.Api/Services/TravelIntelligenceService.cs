using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.Services;

public sealed class TravelIntelligenceService(
    HttpClient httpClient,
    ILogger<TravelIntelligenceService> logger) : ITravelIntelligenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<TravelIntelligenceResponse> AnalyzeAsync(
        ItineraryValidationResponse validation,
        CancellationToken cancellationToken = default)
    {
        var request = ToAgentRequest(validation);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "travel-intelligence/analyze",
                request,
                JsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Travel Intelligence service returned HTTP {StatusCode} for validation {ValidationResultId}.",
                    (int)response.StatusCode,
                    validation.Id);
                return BuildFallback(validation, $"AI service returned {(int)response.StatusCode}.");
            }

            var recommendation = await response.Content.ReadFromJsonAsync<TravelIntelligenceResponse>(
                JsonOptions,
                cancellationToken);

            if (recommendation is null)
            {
                return BuildFallback(validation, "AI service returned an empty response.");
            }

            return EnforceInvariants(validation, recommendation);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Travel Intelligence service timed out for validation {ValidationResultId}.",
                validation.Id);
            return BuildFallback(validation, "AI service request timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Travel Intelligence service was unavailable for validation {ValidationResultId}.",
                validation.Id);
            return BuildFallback(validation, "AI service was unavailable.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Travel Intelligence service returned malformed JSON for validation {ValidationResultId}.",
                validation.Id);
            return BuildFallback(validation, "AI service returned malformed JSON.");
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Travel Intelligence invocation completed for validation {ValidationResultId} in {ElapsedMilliseconds} ms.",
                validation.Id,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static TravelIntelligenceValidationRequest ToAgentRequest(
        ItineraryValidationResponse validation) => new()
        {
            ValidationResultId = validation.Id,
            TripReference = validation.TripReference,
            OverallStatus = validation.OverallStatus,
            RiskLevel = validation.RiskLevel,
            IsFeasible = validation.IsFeasible,
            TotalIssueCount = validation.TotalIssueCount,
            BlockingIssueCount = validation.BlockingIssueCount,
            Issues = validation.Issues.Select(issue => new TravelIntelligenceIssueRequest
            {
                IssueType = issue.IssueType,
                Severity = issue.Severity,
                RuleCode = issue.RuleCode,
                Message = issue.Message,
                IsBlocking = issue.IsBlocking,
                RelatedDistrict = issue.RelatedDistrict,
                RelatedItemReference = issue.RelatedItemReference
            }).ToList()
        };

    private static TravelIntelligenceResponse EnforceInvariants(
        ItineraryValidationResponse validation,
        TravelIntelligenceResponse recommendation)
    {
        if (recommendation.ValidationResultId != validation.Id ||
            recommendation.RiskLevel != validation.RiskLevel ||
            recommendation.IsFeasible != validation.IsFeasible ||
            (!validation.IsFeasible &&
             recommendation.RecommendedAction == TravelIntelligenceAction.Proceed) ||
            (validation.BlockingIssueCount > 0 && !recommendation.RequiresHumanApproval))
        {
            return BuildFallback(validation, "AI response contradicted deterministic validation state.");
        }

        return recommendation;
    }

    private static TravelIntelligenceResponse BuildFallback(
        ItineraryValidationResponse validation,
        string reason)
    {
        var action = ChooseFallbackAction(validation);
        var affected = validation.Issues
            .SelectMany(issue => (issue.RelatedItemReference ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new TravelIntelligenceResponse
        {
            Summary = $"Deterministic validation is {validation.OverallStatus} with {validation.TotalIssueCount} issue(s), including {validation.BlockingIssueCount} blocking issue(s).",
            RiskLevel = validation.RiskLevel,
            RecommendedAction = action,
            Recommendations = validation.Issues.Select(issue => new TravelIntelligenceRecommendation
            {
                Action = FallbackIssueAction(issue),
                Explanation = FallbackIssueExplanation(issue),
                AffectedItemReferences = SplitReferences(issue.RelatedItemReference)
            }).ToList(),
            RequiresHumanApproval = validation.BlockingIssueCount > 0 ||
                action is TravelIntelligenceAction.Reschedule or
                    TravelIntelligenceAction.Reroute or
                    TravelIntelligenceAction.ReviewBudget or
                    TravelIntelligenceAction.ResolveScheduleConflict or
                    TravelIntelligenceAction.ManualReview,
            AffectedItemReferences = affected,
            ValidationResultId = validation.Id,
            IsFeasible = validation.IsFeasible,
            Execution = new TravelIntelligenceExecutionMetadata
            {
                AgentName = "TravelIntelligenceValidationAgent",
                AgentVersion = "1.0",
                ValidationResultId = validation.Id,
                Provider = "aspnet-deterministic-fallback",
                UsedFallback = true,
                ExecutionStatus = "Fallback",
                FallbackReason = reason
            }
        };
    }

    private static TravelIntelligenceAction ChooseFallbackAction(
        ItineraryValidationResponse validation)
    {
        if (validation.IsFeasible && validation.Issues.Count == 0)
        {
            return TravelIntelligenceAction.Proceed;
        }

        if (validation.Issues.Any(issue =>
                issue.IssueType == ValidationIssueType.TravelAlert &&
                issue.Severity == ValidationIssueSeverity.Critical &&
                issue.IsBlocking))
        {
            return TravelIntelligenceAction.Reschedule;
        }

        if (validation.Issues.Any(issue =>
                issue.IssueType is ValidationIssueType.ScheduleConflict or ValidationIssueType.InvalidTimeRange &&
                issue.IsBlocking))
        {
            return TravelIntelligenceAction.ResolveScheduleConflict;
        }

        if (validation.Issues.Any(issue => issue.IssueType == ValidationIssueType.BudgetExceeded))
        {
            return TravelIntelligenceAction.ReviewBudget;
        }

        if (validation.IsFeasible &&
            validation.Issues.Any(issue => issue.IssueType == ValidationIssueType.TravelAlert))
        {
            return TravelIntelligenceAction.ProceedWithCaution;
        }

        return TravelIntelligenceAction.ManualReview;
    }

    private static TravelIntelligenceAction FallbackIssueAction(ValidationIssueResponse issue) =>
        issue.IssueType switch
        {
            ValidationIssueType.TravelAlert when issue.Severity == ValidationIssueSeverity.Critical
                => TravelIntelligenceAction.Reschedule,
            ValidationIssueType.TravelAlert => TravelIntelligenceAction.ProceedWithCaution,
            ValidationIssueType.BudgetExceeded => TravelIntelligenceAction.ReviewBudget,
            ValidationIssueType.ScheduleConflict or ValidationIssueType.InvalidTimeRange
                => TravelIntelligenceAction.ResolveScheduleConflict,
            _ => TravelIntelligenceAction.ManualReview
        };

    private static string FallbackIssueExplanation(ValidationIssueResponse issue) =>
        issue.IssueType switch
        {
            ValidationIssueType.TravelAlert
                => $"{issue.Severity} travel alert requires review before the affected item is undertaken.",
            ValidationIssueType.BudgetExceeded => "Review the budget or adjust itinerary costs.",
            ValidationIssueType.ScheduleConflict => "Adjust the conflicting activity times.",
            ValidationIssueType.InvalidTimeRange => "Correct the activity start and end times.",
            _ => "Review this validation issue before proceeding."
        };

    private static List<string> SplitReferences(string? references) =>
        (references ?? string.Empty)
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal)
        .ToList();
}
