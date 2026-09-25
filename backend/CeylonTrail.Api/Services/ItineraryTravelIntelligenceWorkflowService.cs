using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class ItineraryTravelIntelligenceWorkflowService(
    ApplicationDbContext dbContext,
    IItineraryValidationService validationService,
    ITravelIntelligenceService travelIntelligenceService,
    ITravelIntelligenceExecutionPersistenceService executionPersistenceService) : IItineraryTravelIntelligenceWorkflowService
{
    public async Task<ItineraryTravelIntelligenceWorkflowResult> ProcessAsync(
        Guid tripId,
        Guid touristId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(tripId, touristId, cancellationToken);
        if (snapshot is null)
        {
            return ItineraryTravelIntelligenceWorkflowResult.Failure(
                "The persisted trip or itinerary could not be loaded for validation.");
        }

        var (trip, itinerary, attractions) = snapshot.Value;
        var itemRequests = itinerary.Days
            .OrderBy(day => day.DayNumber)
            .SelectMany(day => day.Items.Select(item =>
            {
                if (!attractions.TryGetValue(item.AttractionId, out var attraction))
                {
                    throw new InvalidOperationException(
                        $"The persisted itinerary references missing attraction {item.AttractionId}.");
                }

                return new ItineraryItemRequest
                {
                    Reference = item.Id.ToString(),
                    Title = attraction.Name,
                    District = attraction.District,
                    StartDateTime = DateTime.SpecifyKind(
                        day.Date.ToDateTime(item.StartTime),
                        DateTimeKind.Utc),
                    EndDateTime = DateTime.SpecifyKind(
                        day.Date.ToDateTime(item.EndTime),
                        DateTimeKind.Utc)
                };
            }))
            .ToList();

        var validation = await validationService.ValidateAsync(
            new ItineraryValidationRequest
            {
                TripReference = trip.Id.ToString(),
                Budget = trip.Budget,
                EstimatedCost = itinerary.TotalEstimatedCost,
                Items = itemRequests
            },
            touristId,
            cancellationToken);

        if (!validation.Succeeded || validation.Response is null)
        {
            return ItineraryTravelIntelligenceWorkflowResult.Failure(
                validation.Error ?? "Deterministic itinerary validation failed.");
        }

        var recommendation = await travelIntelligenceService.AnalyzeAsync(
            validation.Response,
            cancellationToken);

        // A stable workflow ID makes retries for the same persisted validation idempotent.
        if (recommendation.Execution.WorkflowId == Guid.Empty)
        {
            recommendation.Execution.WorkflowId = validation.Response.Id;
        }

        var persistence = await executionPersistenceService.PersistAsync(
            validation.Response,
            touristId,
            recommendation,
            cancellationToken);
        if (!persistence.Succeeded)
        {
            return ItineraryTravelIntelligenceWorkflowResult.Failure(
                persistence.Error ?? "Travel Intelligence execution persistence failed.");
        }

        return new(
            true,
            null,
            validation.Response.Id,
            persistence.ExecutionId,
            persistence.ApprovalRequest);
    }

    private async Task<(Models.Trip Trip, Models.Itinerary Itinerary, Dictionary<Guid, Models.Attraction> Attractions)?> LoadSnapshotAsync(
        Guid tripId,
        Guid touristId,
        CancellationToken cancellationToken)
    {
        var trip = await dbContext.Trips
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == tripId && candidate.TouristId == touristId,
                cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var itinerary = await dbContext.Itineraries
            .AsNoTracking()
            .Where(candidate => candidate.TripId == tripId && candidate.Status == Models.ItineraryStatus.Active)
            .Include(candidate => candidate.Days)
                .ThenInclude(day => day.Items)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (itinerary is null)
        {
            return null;
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

        return (trip, itinerary, attractions);
    }
}
