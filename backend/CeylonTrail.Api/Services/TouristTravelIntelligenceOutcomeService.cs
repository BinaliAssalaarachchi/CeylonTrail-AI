using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.DTOs.TravelIntelligence;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class TouristTravelIntelligenceOutcomeService(
    ApplicationDbContext dbContext,
    ILogger<TouristTravelIntelligenceOutcomeService> logger) : ITouristTravelIntelligenceOutcomeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<TouristTravelIntelligenceOutcomeResponse?> GetLatestAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        if (touristId == Guid.Empty || tripId == Guid.Empty ||
            !await dbContext.Trips.AsNoTracking().AnyAsync(
                trip => trip.Id == tripId && trip.TouristId == touristId,
                cancellationToken))
        {
            return null;
        }

        // TripReference is stored from the validation request. For integrated trip
        // workflows it is the canonical Guid string representation of Trip.Id.
        var tripReference = tripId.ToString();
        var execution = await dbContext.TravelIntelligenceExecutions
            .AsNoTracking()
            .Include(candidate => candidate.ApprovalRequest)
                .ThenInclude(approval => approval!.Decision)
            .Where(candidate => candidate.RequestedByUserId == touristId &&
                                candidate.ValidationResult != null &&
                                candidate.ValidationResult.CreatedByUserId == touristId &&
                                candidate.ValidationResult.TripReference == tripReference)
            .OrderByDescending(candidate => candidate.StartedAt)
            .ThenByDescending(candidate => candidate.CreatedAt)
            .ThenByDescending(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return execution is null ? null : ToResponse(tripId, execution);
    }

    private TouristTravelIntelligenceOutcomeResponse ToResponse(
        Guid tripId,
        TravelIntelligenceExecution execution)
    {
        var approval = execution.ApprovalRequest;
        return new TouristTravelIntelligenceOutcomeResponse
        {
            TripId = tripId,
            ExecutionId = execution.Id,
            RiskLevel = execution.RiskLevel,
            IsFeasible = execution.IsFeasible,
            RecommendedAction = execution.RecommendedAction,
            Summary = execution.Summary,
            RequiresHumanApproval = execution.RequiresHumanApproval,
            Recommendations = ReadJson(execution.RecommendationsJson, Array.Empty<TravelIntelligenceRecommendation>(), "recommendations", execution.Id).ToList(),
            AffectedItems = ReadJson(execution.AffectedItemsJson, Array.Empty<TravelIntelligenceAffectedItem>(), "affected items", execution.Id).ToList(),
            Alternatives = ReadJson(execution.AlternativesJson, Array.Empty<TravelIntelligenceAlternativeRecommendation>(), "alternatives", execution.Id).ToList(),
            SafeWindows = ReadJson(execution.SafeWindowsJson, Array.Empty<TravelIntelligenceSafeWindowSuggestion>(), "safe windows", execution.Id).ToList(),
            ReviewStatus = ToReviewStatus(execution),
            Decision = approval?.Decision?.Decision,
            RequestedAt = approval?.CreatedAt,
            DecidedAt = approval?.Decision?.DecidedAt,
            AssessedAt = execution.CompletedAt ?? execution.StartedAt
        };
    }

    private static TouristReviewStatus ToReviewStatus(TravelIntelligenceExecution execution)
    {
        if (execution.ApprovalRequest is null)
        {
            return execution.RequiresHumanApproval
                ? TouristReviewStatus.ApprovalRequired
                : TouristReviewStatus.NotRequired;
        }

        return execution.ApprovalRequest.Status switch
        {
            ApprovalRequestStatus.Pending => TouristReviewStatus.Pending,
            ApprovalRequestStatus.Approved => TouristReviewStatus.Approved,
            ApprovalRequestStatus.Rejected => TouristReviewStatus.Rejected,
            _ => TouristReviewStatus.ApprovalRequired
        };
    }

    private T ReadJson<T>(string json, T fallback, string field, Guid executionId)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Malformed optional tourist {Field} snapshot for Travel Intelligence execution {ExecutionId}.",
                field,
                executionId);
            return fallback;
        }
    }
}
