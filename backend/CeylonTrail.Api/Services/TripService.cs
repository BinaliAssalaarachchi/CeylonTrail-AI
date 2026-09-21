using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Trips;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class TripService(ApplicationDbContext dbContext) : ITripService
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
