using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class TravelIntelligenceService(
    HttpClient httpClient,
    ILogger<TravelIntelligenceService> logger,
    ApplicationDbContext? dbContext = null) : ITravelIntelligenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<TravelIntelligenceResponse> AnalyzeAsync(
        ItineraryValidationResponse validation,
        CancellationToken cancellationToken = default)
    {
        var request = await ToAgentRequestAsync(validation, cancellationToken);
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

            return EnforceInvariants(validation, request, recommendation);
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

    private async Task<TravelIntelligenceValidationRequest> ToAgentRequestAsync(
        ItineraryValidationResponse validation,
        CancellationToken cancellationToken)
    {
        var request = new TravelIntelligenceValidationRequest
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

        if (dbContext is null ||
            !Guid.TryParse(validation.TripReference, out var tripId) ||
            validation.CreatedByUserId == Guid.Empty)
        {
            return request;
        }

        var itinerary = await dbContext.Itineraries
            .AsNoTracking()
            .Where(candidate => candidate.TripId == tripId &&
                                candidate.Trip.TouristId == validation.CreatedByUserId)
            .Include(candidate => candidate.Days)
                .ThenInclude(day => day.Items)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (itinerary is null)
        {
            return request;
        }

        var attractionIds = itinerary.Days
            .SelectMany(day => day.Items)
            .Select(item => item.AttractionId)
            .Distinct()
            .ToList();
        var attractions = await dbContext.Attractions
            .AsNoTracking()
            .Where(attraction => attractionIds.Contains(attraction.Id))
            .ToDictionaryAsync(attraction => attraction.Id, cancellationToken);

        request.ItineraryItems = itinerary.Days
            .OrderBy(day => day.DayNumber)
            .SelectMany(day => day.Items.Select(item => new TravelIntelligenceItineraryItemRequest
            {
                ItemReference = item.Id.ToString(),
                Title = attractions.TryGetValue(item.AttractionId, out var attraction)
                    ? attraction.Name
                    : null,
                District = attractions.TryGetValue(item.AttractionId, out attraction)
                    ? attraction.District
                    : null,
                StartDateTime = DateTime.SpecifyKind(day.Date.ToDateTime(item.StartTime), DateTimeKind.Utc),
                EndDateTime = DateTime.SpecifyKind(day.Date.ToDateTime(item.EndTime), DateTimeKind.Utc),
                EstimatedCost = item.EstimatedCost
            }))
            .ToList();

        var districts = request.ItineraryItems
            .Where(item => !string.IsNullOrWhiteSpace(item.District))
            .Select(item => item.District!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var activeCriticalAlerts = await dbContext.TravelAlerts
            .AsNoTracking()
            .Where(alert => alert.Status == TravelAlertStatus.Active &&
                            alert.Severity == TravelAlertSeverity.Critical)
            .ToListAsync(cancellationToken);

        request.BlockingTravelAlertWindows = activeCriticalAlerts
            .Where(alert => districts.Any(district =>
                string.Equals(district, alert.District, StringComparison.OrdinalIgnoreCase)))
            .Select(alert => new TravelIntelligenceTravelAlertWindow
            {
                District = alert.District,
                StartDateTime = alert.StartDateTime,
                EndDateTime = alert.EndDateTime
            })
            .ToList();

        return request;
    }

    private static TravelIntelligenceResponse EnforceInvariants(
        ItineraryValidationResponse validation,
        TravelIntelligenceValidationRequest request,
        TravelIntelligenceResponse recommendation)
    {
        var trustedReferences = request.ItineraryItems
            .Select(item => item.ItemReference)
            .Concat(validation.Issues
                .SelectMany(issue => SplitReferences(issue.RelatedItemReference)))
            .ToHashSet(StringComparer.Ordinal);

        var hasInvalidAlternative = recommendation.Alternatives.Count > 3 ||
            recommendation.Alternatives.Any(alternative =>
                alternative.AffectedItemReferences.Any(reference => !trustedReferences.Contains(reference)) ||
                ((validation.BlockingIssueCount > 0 || !validation.IsFeasible) &&
                 alternative.Action == TravelIntelligenceAction.Proceed));

        var hasInvalidAffectedItem = recommendation.AffectedItems.Any(item =>
            item.DetailsAvailable && !request.ItineraryItems.Any(context =>
                context.ItemReference == item.ItemReference));

        var hasInvalidSafeWindow = recommendation.SafeWindows.Count > 20 ||
            recommendation.SafeWindows.Any(window =>
                window.ProposedEnd <= window.ProposedStart ||
                !trustedReferences.Contains(window.ItemReference) ||
                HasKnownConflict(window, request));

        if (recommendation.ValidationResultId != validation.Id ||
            recommendation.RiskLevel != validation.RiskLevel ||
            recommendation.IsFeasible != validation.IsFeasible ||
            (!validation.IsFeasible &&
             recommendation.RecommendedAction == TravelIntelligenceAction.Proceed) ||
            (validation.BlockingIssueCount > 0 && !recommendation.RequiresHumanApproval) ||
            hasInvalidAlternative ||
            hasInvalidAffectedItem ||
            hasInvalidSafeWindow)
        {
            return BuildFallback(validation, "AI response contradicted deterministic validation state.");
        }

        return recommendation;
    }

    private static bool HasKnownConflict(
        TravelIntelligenceSafeWindowSuggestion window,
        TravelIntelligenceValidationRequest request)
    {
        var item = request.ItineraryItems.SingleOrDefault(candidate =>
            candidate.ItemReference == window.ItemReference);
        if (item is null || item.StartDateTime is null || item.EndDateTime is null)
        {
            return true;
        }

        var overlapsItem = request.ItineraryItems.Any(candidate =>
            candidate.ItemReference != window.ItemReference &&
            candidate.StartDateTime is not null &&
            candidate.EndDateTime is not null &&
            window.ProposedStart < candidate.EndDateTime &&
            window.ProposedEnd > candidate.StartDateTime);

        var overlapsAlert = request.BlockingTravelAlertWindows.Any(alert =>
            string.Equals(alert.District, item.District, StringComparison.OrdinalIgnoreCase) &&
            window.ProposedStart < alert.EndDateTime &&
            window.ProposedEnd > alert.StartDateTime);

        return overlapsItem || overlapsAlert;
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
