using CeylonTrail.Api.DTOs.Bookings;
using CeylonTrail.Api.DTOs.Pagination;

namespace CeylonTrail.Api.Interfaces;

public interface IBookingService
{
    Task<(bool Succeeded, string? Error, BookingResponse? Response)> CreateBookingAsync(
        Guid touristId,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default);

    Task<List<BookingResponse>> GetTouristBookingsAsync(
        Guid touristId,
        CancellationToken cancellationToken = default);  

    Task<List<BookingResponse>> GetProviderBookingsAsync(
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<BookingResponse>> GetProviderBookingsPageAsync(
        Guid requestingUserId,
        string requestingRole,
        BookingQuery query,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, BookingResponse? Response)> GetBookingByIdAsync(
        Guid bookingId,
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, BookingResponse? Response)> AcceptBookingAsync(
        Guid bookingId,
        Guid changedBy,
        string? requestingRole = null,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, BookingResponse? Response)> RejectBookingAsync(
        Guid bookingId,
        Guid changedBy,
        RejectBookingRequest request,
        string? requestingRole = null,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, BookingResponse? Response)> CancelBookingAsync(
        Guid bookingId,
        Guid requestingUserId,
        string requestingRole,
        CancelBookingRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, List<BookingHistoryResponse>? Response)> GetBookingHistoryAsync(
        Guid bookingId,
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken = default);

    Task<List<AvailabilitySlotResponse>> GetAvailabilitySlotsAsync(
        Guid? attractionId = null,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, AvailabilitySlotResponse? Response)> CreateAvailabilitySlotAsync(
        CreateAvailabilitySlotRequest request,
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken = default);
}
