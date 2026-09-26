using CeylonTrail.Api.DTOs.BookingAction;

namespace CeylonTrail.Api.Interfaces;

public interface IBookingAvailabilitySnapshotService
{
    Task<IReadOnlyList<TrustedBookingAvailabilitySlot>> GetTrustedFutureSlotsAsync(
        IReadOnlyCollection<Guid> attractionIds,
        CancellationToken cancellationToken = default);
}
