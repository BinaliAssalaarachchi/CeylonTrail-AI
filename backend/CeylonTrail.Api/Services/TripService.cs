using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.DTOs.Planner;
using CeylonTrail.Api.DTOs.Pagination;
using CeylonTrail.Api.DTOs.Trips;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class TripService(
    ApplicationDbContext dbContext,
    IPlannerAgentService? plannerAgent = null,
    IAttractionService? attractionService = null,
    IItineraryTravelIntelligenceWorkflowService? travelIntelligenceWorkflow = null,
    ILogger<TripService>? logger = null,
    IAgentTripWorkflowOrchestrator? agentWorkflowOrchestrator = null) : ITripService
{
    public async Task<TripServiceResult<TripResponse>> CreateTripAsync(
        Guid touristId,
        CreateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateTrip(request.Name, request.StartDate, request.EndDate, request.Budget);
        if (validationError is not null)
        {
            return new TripServiceResult<TripResponse>(Error: validationError);
        }

        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            TouristId = touristId,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Budget = request.Budget,
            Status = TripStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Trips.Add(trip);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TripServiceResult<TripResponse>(ToTripResponse(trip));
    }

    public async Task<IReadOnlyList<TripResponse>> GetTripsAsync(
        Guid touristId,
        CancellationToken cancellationToken = default)
    {
        var trips = await dbContext.Trips
            .AsNoTracking()
            .Where(trip => trip.TouristId == touristId)
            .Include(trip => trip.Preferences)
            .OrderByDescending(trip => trip.CreatedAt)
            .ToListAsync(cancellationToken);

        return trips.Select(ToTripResponse).ToList();
    }

    public async Task<TripServiceResult<TripResponse>> GetTripAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
        {
            return new TripServiceResult<TripResponse>(Error: "Trip ID must not be empty.", NotFound: true);
        }

        var trip = await dbContext.Trips
            .AsNoTracking()
            .Include(candidate => candidate.Preferences)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == tripId && candidate.TouristId == touristId,
                cancellationToken);

        return trip is null
            ? new TripServiceResult<TripResponse>(NotFound: true)
            : new TripServiceResult<TripResponse>(ToTripResponse(trip));
    }

    public async Task<TripServiceResult<TripResponse>> UpdateTripAsync(
        Guid touristId,
        Guid tripId,
        UpdateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
        {
            return new TripServiceResult<TripResponse>(Error: "Trip ID must not be empty.", NotFound: true);
        }

        var trip = await dbContext.Trips
            .Include(candidate => candidate.Preferences)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == tripId && candidate.TouristId == touristId,
                cancellationToken);

        if (trip is null)
        {
            return new TripServiceResult<TripResponse>(NotFound: true);
        }

        var validationError = ValidateTrip(request.Name, request.StartDate, request.EndDate, request.Budget);
        if (validationError is not null)
        {
            return new TripServiceResult<TripResponse>(Error: validationError);
        }

        if (request.Status is not null)
        {
            if (!Enum.IsDefined(request.Status.Value))
            {
                return new TripServiceResult<TripResponse>(Error: "The requested trip status is not valid.");
            }

            if (!IsAllowedStatusTransition(trip.Status, request.Status.Value))
            {
                return new TripServiceResult<TripResponse>(
                    Error: $"A trip cannot transition from {trip.Status} to {request.Status.Value}.");
            }

            trip.Status = request.Status.Value;
        }

        trip.Name = request.Name.Trim();
        trip.StartDate = request.StartDate;
        trip.EndDate = request.EndDate;
        trip.Budget = request.Budget;
        trip.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TripServiceResult<TripResponse>(ToTripResponse(trip));
    }

    public async Task<bool> DeleteTripAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
        {
            return false;
        }

        var trip = await dbContext.Trips
            .SingleOrDefaultAsync(
                candidate => candidate.Id == tripId && candidate.TouristId == touristId,
                cancellationToken);

        if (trip is null)
        {
            return false;
        }

        dbContext.Trips.Remove(trip);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TripServiceResult<TripPreferenceResponse>> AddPreferenceAsync(
        Guid touristId,
        Guid tripId,
        AddTripPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
        {
            return new TripServiceResult<TripPreferenceResponse>(Error: "Trip ID must not be empty.", NotFound: true);
        }

        var preferenceType = request.PreferenceType.Trim();
        var value = request.Value.Trim();
        if (string.IsNullOrWhiteSpace(preferenceType) || string.IsNullOrWhiteSpace(value))
        {
            return new TripServiceResult<TripPreferenceResponse>(
                Error: "Preference type and value are required.");
        }

        var tripExists = await dbContext.Trips.AnyAsync(
            trip => trip.Id == tripId && trip.TouristId == touristId,
            cancellationToken);
        if (!tripExists)
        {
            return new TripServiceResult<TripPreferenceResponse>(NotFound: true);
        }

        var preference = new TripPreference
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            PreferenceType = preferenceType,
            Value = value
        };

        dbContext.TripPreferences.Add(preference);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TripServiceResult<TripPreferenceResponse>(ToPreferenceResponse(preference));
    }

    public async Task<TripServiceResult<ItineraryResponse>> GetLatestItineraryAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
        {
            return new TripServiceResult<ItineraryResponse>(Error: "Trip ID must not be empty.", NotFound: true);
        }

        var itinerary = await dbContext.Itineraries
            .AsNoTracking()
            .Where(itinerary => itinerary.TripId == tripId && itinerary.Trip.TouristId == touristId)
            .Include(itinerary => itinerary.Days)
                .ThenInclude(day => day.Items)
            .OrderByDescending(itinerary => itinerary.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return itinerary is null
            ? new TripServiceResult<ItineraryResponse>(NotFound: true)
            : new TripServiceResult<ItineraryResponse>(ToItineraryResponse(itinerary));
    }

    public async Task<TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>> GetItineraryHistoryAsync(
        Guid touristId, Guid tripId, CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty || !await dbContext.Trips.AnyAsync(t => t.Id == tripId && t.TouristId == touristId, cancellationToken))
            return new TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>(NotFound: true);

        var history = await dbContext.Itineraries.AsNoTracking()
            .Where(i => i.TripId == tripId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ItineraryHistoryItemResponse(i.Id, i.Status, i.TotalEstimatedCost, i.CreatedAt, i.UpdatedAt, i.Days.Count))
            .ToListAsync(cancellationToken);
        return new TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>(history);
    }

    public async Task<TripServiceResult<ItineraryResponse>> GetItineraryAsync(
        Guid touristId, Guid tripId, Guid itineraryId, CancellationToken cancellationToken = default)
    {
        var itinerary = await dbContext.Itineraries.AsNoTracking()
            .Where(i => i.Id == itineraryId && i.TripId == tripId && i.Trip.TouristId == touristId)
            .Include(i => i.Days).ThenInclude(d => d.Items)
            .SingleOrDefaultAsync(cancellationToken);
        return itinerary is null ? new TripServiceResult<ItineraryResponse>(NotFound: true) : new TripServiceResult<ItineraryResponse>(ToItineraryResponse(itinerary));
    }

    public async Task<TripServiceResult<ItineraryResponse>> GenerateItineraryAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        if (agentWorkflowOrchestrator is not null)
        {
            return await agentWorkflowOrchestrator.ExecuteAsync(touristId, tripId, cancellationToken);
        }

        if (tripId == Guid.Empty)
        {
            return new TripServiceResult<ItineraryResponse>(Error: "Trip ID must not be empty.", NotFound: true);
        }

        var trip = await dbContext.Trips
            .Include(candidate => candidate.Preferences)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == tripId && candidate.TouristId == touristId,
                cancellationToken);

        if (trip is null)
        {
            return new TripServiceResult<ItineraryResponse>(NotFound: true);
        }

        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled)
        {
            return new TripServiceResult<ItineraryResponse>(
                Error: $"A {trip.Status} trip cannot generate a new itinerary.");
        }

        if (plannerAgent is null || attractionService is null)
        {
            return new TripServiceResult<ItineraryResponse>(Error: "Planner Agent integration is not configured.");
        }

        var candidates = await attractionService.SearchAsync(
            new AttractionSearchRequest { Page = 1, PageSize = 100, Sort = "name_asc" },
            touristId,
            cancellationToken);
        if (!candidates.Succeeded || candidates.Value is null)
        {
            return new TripServiceResult<ItineraryResponse>(
                Error: candidates.Error ?? "Controlled attraction candidates could not be loaded.");
        }

        if (candidates.Value.Items.Count == 0)
        {
            return new TripServiceResult<ItineraryResponse>(
                Error: "No approved attraction candidates are available for itinerary planning.");
        }

        var plannerRequest = new PlannerAgentRequest(
            trip.Id.ToString(),
            trip.StartDate,
            trip.EndDate,
            trip.EndDate.DayNumber - trip.StartDate.DayNumber + 1,
            trip.Budget,
            trip.Preferences
                .Where(preference => string.Equals(preference.PreferenceType, "Interest", StringComparison.OrdinalIgnoreCase))
                .Select(preference => preference.Value)
                .ToList(),
            trip.Preferences
                .Where(preference => string.Equals(preference.PreferenceType, "Region", StringComparison.OrdinalIgnoreCase))
                .Select(preference => preference.Value)
                .ToList(),
            trip.Preferences.Select(preference => new PlannerPreference(
                preference.PreferenceType,
                preference.Value)).ToList(),
            candidates.Value.Items.Select(ToPlannerCandidate).ToList());

        PlannerAgentServiceResult plannerResult;
        try
        {
            plannerResult = await plannerAgent.GenerateAsync(plannerRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new TripServiceResult<ItineraryResponse>(Error: "Planner Agent request timed out.", ServiceUnavailable: true);
        }
        catch (Exception)
        {
            return new TripServiceResult<ItineraryResponse>(Error: "Planner Agent failed to generate an itinerary.", ServiceUnavailable: true);
        }
        if (!plannerResult.Succeeded)
        {
            return new TripServiceResult<ItineraryResponse>(
                Error: plannerResult.Error ?? "Planner Agent failed to generate an itinerary.",
                ServiceUnavailable: plannerResult.ServiceUnavailable);
        }

        var plannerOutput = plannerResult.Value!;

        var validationError = ValidatePlannerOutput(
            plannerOutput,
            trip,
            candidates.Value.Items.ToDictionary(candidate => candidate.Id, candidate => candidate.Price));
        if (validationError is not null)
        {
            return new TripServiceResult<ItineraryResponse>(Error: validationError);
        }

        var now = DateTime.UtcNow;
        foreach (var previous in await dbContext.Itineraries
                     .Where(itinerary => itinerary.TripId == trip.Id && itinerary.Status == ItineraryStatus.Active)
                     .ToListAsync(cancellationToken))
        {
            previous.Status = ItineraryStatus.Superseded;
            previous.UpdatedAt = now;
        }

        var itinerary = new Itinerary
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Status = ItineraryStatus.Active,
            TotalEstimatedCost = plannerOutput.EstimatedCost,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var plannerDay in plannerOutput.Days)
        {
            var day = new ItineraryDay
            {
                Id = Guid.NewGuid(),
                DayNumber = plannerDay.DayNumber,
                Date = plannerDay.Date
            };
            foreach (var plannerItem in plannerDay.Items)
            {
                day.Items.Add(new ItineraryItem
                {
                    Id = Guid.NewGuid(),
                    AttractionId = plannerItem.AttractionId,
                    StartTime = plannerItem.StartTime,
                    EndTime = plannerItem.EndTime,
                    EstimatedCost = plannerItem.EstimatedCost,
                    Notes = plannerItem.Notes
                });
            }
            itinerary.Days.Add(day);
        }

        dbContext.Itineraries.Add(itinerary);
        if (trip.Status == TripStatus.Draft)
        {
            trip.Status = TripStatus.Planned;
        }
        trip.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (travelIntelligenceWorkflow is not null)
        {
            try
            {
                var workflow = await travelIntelligenceWorkflow.ProcessAsync(
                    trip.Id,
                    touristId,
                    cancellationToken);
                if (!workflow.Succeeded)
                {
                    logger?.LogWarning(
                        "Itinerary {ItineraryId} was persisted, but the Travel Intelligence workflow did not complete: {Error}",
                        itinerary.Id,
                        workflow.Error);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger?.LogWarning(
                    "Travel Intelligence workflow timed out after itinerary {ItineraryId} was persisted.",
                    itinerary.Id);
            }
            catch (Exception exception)
            {
                logger?.LogError(
                    exception,
                    "Travel Intelligence workflow failed after itinerary {ItineraryId} was persisted.",
                    itinerary.Id);
            }
        }

        return new TripServiceResult<ItineraryResponse>(ToItineraryResponse(itinerary));
    }

    public async Task<IReadOnlyList<StaffTripResponse>> GetStaffTripsAsync(
        CancellationToken cancellationToken = default)
    {
        var trips = await dbContext.Trips
            .AsNoTracking()
            .Include(trip => trip.Itineraries)
            .OrderByDescending(trip => trip.UpdatedAt)
            .ToListAsync(cancellationToken);

        return trips.Select(ToStaffTripResponse).ToList();
    }

    public async Task<PagedResponse<StaffTripResponse>> GetStaffTripsPageAsync(
        StaffTripQuery request,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Trips.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(trip =>
                trip.Name.Contains(search) ||
                trip.Preferences.Any(preference =>
                    preference.PreferenceType == "Objective" && preference.Value.Contains(search)) ||
                trip.Tourist.FirstName.Contains(search) ||
                trip.Tourist.LastName.Contains(search) ||
                trip.Tourist.Email.Contains(search));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(trip => trip.Status == request.Status.Value);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(trip => trip.EndDate >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(trip => trip.StartDate <= request.DateTo.Value);
        }

        query = ApplyTripOrdering(query, request.SortBy, request.SortDirection);
        var totalCount = await query.CountAsync(cancellationToken);
        var page = request.Page;
        var pageSize = request.PageSize;
        var trips = await query
            .Include(trip => trip.Itineraries)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<StaffTripResponse>(
            trips.Select(ToStaffTripResponse).ToList(),
            totalCount,
            page,
            pageSize,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    private static IQueryable<Trip> ApplyTripOrdering(
        IQueryable<Trip> query,
        string sortBy,
        string sortDirection)
    {
        var ascending = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy.ToLowerInvariant(), ascending) switch
        {
            ("name", true) => query.OrderBy(trip => trip.Name).ThenBy(trip => trip.Id),
            ("name", false) => query.OrderByDescending(trip => trip.Name).ThenByDescending(trip => trip.Id),
            ("startdate", true) => query.OrderBy(trip => trip.StartDate).ThenBy(trip => trip.Id),
            ("startdate", false) => query.OrderByDescending(trip => trip.StartDate).ThenByDescending(trip => trip.Id),
            ("enddate", true) => query.OrderBy(trip => trip.EndDate).ThenBy(trip => trip.Id),
            ("enddate", false) => query.OrderByDescending(trip => trip.EndDate).ThenByDescending(trip => trip.Id),
            ("status", true) => query.OrderBy(trip => trip.Status).ThenBy(trip => trip.Id),
            ("status", false) => query.OrderByDescending(trip => trip.Status).ThenByDescending(trip => trip.Id),
            ("createdat", true) => query.OrderBy(trip => trip.CreatedAt).ThenBy(trip => trip.Id),
            ("createdat", false) => query.OrderByDescending(trip => trip.CreatedAt).ThenByDescending(trip => trip.Id),
            ("updatedat", true) => query.OrderBy(trip => trip.UpdatedAt).ThenBy(trip => trip.Id),
            _ => query.OrderByDescending(trip => trip.UpdatedAt).ThenByDescending(trip => trip.Id),
        };
    }

    public async Task<TripServiceResult<StaffTripResponse>> GetStaffTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
        {
            return new TripServiceResult<StaffTripResponse>(Error: "Trip ID must not be empty.", NotFound: true);
        }

        var trip = await dbContext.Trips
            .AsNoTracking()
            .Include(candidate => candidate.Itineraries)
            .SingleOrDefaultAsync(candidate => candidate.Id == tripId, cancellationToken);

        return trip is null
            ? new TripServiceResult<StaffTripResponse>(NotFound: true)
            : new TripServiceResult<StaffTripResponse>(ToStaffTripResponse(trip));
    }

    public async Task<TripServiceResult<ItineraryResponse>> GetStaffItineraryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
        {
            return new TripServiceResult<ItineraryResponse>(Error: "Trip ID must not be empty.", NotFound: true);
        }

        var itinerary = await dbContext.Itineraries
            .AsNoTracking()
            .Where(candidate => candidate.TripId == tripId)
            .Include(candidate => candidate.Days)
                .ThenInclude(day => day.Items)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return itinerary is null
            ? new TripServiceResult<ItineraryResponse>(NotFound: true)
            : new TripServiceResult<ItineraryResponse>(ToItineraryResponse(itinerary));
    }

    private static string? ValidateTrip(string name, DateOnly startDate, DateOnly endDate, decimal budget)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Trip name is required.";
        }

        if (startDate == default || endDate == default)
        {
            return "Start date and end date are required.";
        }

        if (endDate < startDate)
        {
            return "End date must be greater than or equal to start date.";
        }

        return budget < 0 ? "Budget must be greater than or equal to zero." : null;
    }

    public async Task<TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>> GetStaffItineraryHistoryAsync(
        Guid tripId, CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty || !await dbContext.Trips.AnyAsync(t => t.Id == tripId, cancellationToken))
            return new TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>(NotFound: true);

        var history = await dbContext.Itineraries.AsNoTracking()
            .Where(i => i.TripId == tripId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ItineraryHistoryItemResponse(i.Id, i.Status, i.TotalEstimatedCost, i.CreatedAt, i.UpdatedAt, i.Days.Count))
            .ToListAsync(cancellationToken);
        return new TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>(history);
    }

    public async Task<TripServiceResult<ItineraryResponse>> GetStaffItineraryAsync(
        Guid tripId, Guid itineraryId, CancellationToken cancellationToken = default)
    {
        var itinerary = await dbContext.Itineraries.AsNoTracking()
            .Where(i => i.Id == itineraryId && i.TripId == tripId)
            .Include(i => i.Days).ThenInclude(d => d.Items)
            .SingleOrDefaultAsync(cancellationToken);
        return itinerary is null ? new TripServiceResult<ItineraryResponse>(NotFound: true) : new TripServiceResult<ItineraryResponse>(ToItineraryResponse(itinerary));
    }

    private static PlannerCandidateAttraction ToPlannerCandidate(AttractionResponse attraction) => new(
        attraction.Id.ToString(),
        attraction.Name,
        attraction.Category?.Name,
        attraction.District,
        attraction.Price,
        attraction.Description,
        null);

    private static string? ValidatePlannerOutput(
        PlannerAgentResponse output,
        Trip trip,
        IReadOnlyDictionary<Guid, decimal> candidatePrices)
    {
        if (!string.Equals(output.Status, "Generated", StringComparison.OrdinalIgnoreCase))
        {
            return output.Message ?? "Planner Agent did not produce an itinerary.";
        }

        if (output.Days.Count == 0)
        {
            return "Planner Agent returned an empty itinerary.";
        }

        var dayNumbers = new HashSet<int>();
        var attractionIds = new HashSet<Guid>();
        decimal total = 0m;
        foreach (var day in output.Days)
        {
            if (day.DayNumber <= 0 || !dayNumbers.Add(day.DayNumber))
            {
                return "Planner Agent returned invalid or duplicate day numbers.";
            }
            if (day.Date < trip.StartDate || day.Date > trip.EndDate)
            {
                return "Planner Agent returned a day outside the trip dates.";
            }

            TimeOnly? previousEnd = null;
            foreach (var item in day.Items.OrderBy(item => item.StartTime))
            {
                if (!candidatePrices.TryGetValue(item.AttractionId, out var candidatePrice))
                {
                    return "Planner Agent returned an unknown attraction.";
                }
                if (item.EstimatedCost != candidatePrice)
                {
                    return "Planner Agent returned a cost different from the supplied attraction.";
                }
                if (!attractionIds.Add(item.AttractionId))
                {
                    return "Planner Agent scheduled an attraction more than once.";
                }
                if (item.EndTime <= item.StartTime || (previousEnd.HasValue && item.StartTime < previousEnd.Value))
                {
                    return "Planner Agent returned an invalid or overlapping time range.";
                }
                if (item.EstimatedCost < 0)
                {
                    return "Planner Agent returned a negative item cost.";
                }
                previousEnd = item.EndTime;
                total += item.EstimatedCost;
            }
        }

        if (output.EstimatedCost < 0 || output.EstimatedCost != total)
        {
            return "Planner Agent returned an invalid total estimated cost.";
        }
        return total > trip.Budget ? "Planner Agent returned an itinerary over budget." : null;
    }

    private static bool IsAllowedStatusTransition(TripStatus current, TripStatus requested) =>
        current == requested || (current, requested) switch
        {
            (TripStatus.Draft, TripStatus.Planned) => true,
            (TripStatus.Draft, TripStatus.Cancelled) => true,
            (TripStatus.Planned, TripStatus.Completed) => true,
            (TripStatus.Planned, TripStatus.Cancelled) => true,
            _ => false
        };

    private static TripResponse ToTripResponse(Trip trip) => new(
        trip.Id,
        trip.Name,
        trip.StartDate,
        trip.EndDate,
        trip.Budget,
        trip.Status,
        trip.CreatedAt,
        trip.UpdatedAt,
        trip.Preferences.Select(ToPreferenceResponse).ToList());

    private static TripPreferenceResponse ToPreferenceResponse(TripPreference preference) => new(
        preference.Id,
        preference.PreferenceType,
        preference.Value);

    private static StaffTripResponse ToStaffTripResponse(Trip trip) => new(
        trip.Id,
        trip.TouristId,
        trip.Name,
        trip.StartDate,
        trip.EndDate,
        trip.Budget,
        trip.Status,
        trip.CreatedAt,
        trip.UpdatedAt,
        trip.Itineraries.Count > 0);

    private static ItineraryResponse ToItineraryResponse(Itinerary itinerary) => new(
        itinerary.Id,
        itinerary.TripId,
        itinerary.Status,
        itinerary.TotalEstimatedCost,
        itinerary.CreatedAt,
        itinerary.UpdatedAt,
        itinerary.Days
            .OrderBy(day => day.DayNumber)
            .Select(day => new ItineraryDayResponse(
                day.Id,
                day.DayNumber,
                day.Date,
                day.Items
                    .OrderBy(item => item.StartTime)
                    .Select(item => new ItineraryItemResponse(
                        item.Id,
                        item.AttractionId,
                        item.StartTime,
                        item.EndTime,
                        item.EstimatedCost,
                        item.Notes))
                    .ToList()))
            .ToList());
}
