namespace CeylonTrail.Api.DTOs.Bookings;

public record BookingItemResponse(
    Guid Id,
    Guid AttractionId,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);

public record BookingHistoryResponse(
    Guid Id,
    string PreviousStatus,
    string NewStatus,
    Guid ChangedBy,
    DateTime ChangedAt,
    string? Reason
);

public record CancellationResponse(
    Guid Id,
    string Reason,
    Guid CancelledBy,
    DateTime CancelledAt
);

public record BookingResponse(
    Guid Id,
    Guid TouristId,
    Guid? TripId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<BookingItemResponse> Items,
    List<BookingHistoryResponse>? StatusHistory,
    CancellationResponse? Cancellation
);
