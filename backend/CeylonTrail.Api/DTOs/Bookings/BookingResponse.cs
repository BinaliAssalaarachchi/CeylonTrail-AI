using CeylonTrail.Api.DTOs.TravelAlerts;

namespace CeylonTrail.Api.DTOs.Bookings;

public record BookingItemResponse(
    Guid Id,
    Guid AvailabilitySlotId,
    int NumberOfGuests,
    decimal UnitPrice,
    decimal SubTotal
);

public record BookingHistoryResponse(
    Guid Id,
    string PreviousStatus,
    string NewStatus,
    Guid? ChangedByUserId,
    DateTime Timestamp,
    string? Reason
);

public record CancellationRequestResponse(
    Guid Id,
    string Reason,
    string Status,
    decimal? RefundAmount,
    DateTime RequestedAt
);

public record BookingResponse(
    Guid Id,
    Guid? TripId,
    string CurrentStatus,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<BookingItemResponse> Items,
    List<BookingHistoryResponse>? StatusHistory,
    List<CancellationRequestResponse>? CancellationRequests,
    List<TravelAlertResponse>? ActiveAdvisories = null
);
