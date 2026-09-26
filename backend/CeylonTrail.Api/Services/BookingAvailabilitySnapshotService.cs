using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.BookingAction;
using CeylonTrail.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class BookingAvailabilitySnapshotService(ApplicationDbContext dbContext)
    : IBookingAvailabilitySnapshotService
{
    public async Task<IReadOnlyList<TrustedBookingAvailabilitySlot>> GetTrustedFutureSlotsAsync(
        IReadOnlyCollection<Guid> attractionIds,
        CancellationToken cancellationToken = default)
    {
        var ids = attractionIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var slots = await dbContext.AvailabilitySlots
            .AsNoTracking()
            .Include(slot => slot.Attraction)
            .Where(slot =>
                ids.Contains(slot.AttractionId) &&
                slot.Attraction != null &&
                slot.Attraction.IsActive &&
                slot.Attraction.Status == "Approved" &&
                slot.EndTime > now &&
                slot.MaxCapacity > 0 &&
                slot.PricePerPerson >= 0 &&
                slot.BookedCapacity >= 0 &&
                slot.BookedCapacity < slot.MaxCapacity)
            .OrderBy(slot => slot.StartTime)
            .ThenBy(slot => slot.Id)
            .ToListAsync(cancellationToken);

        return slots.Select(slot => new TrustedBookingAvailabilitySlot(
            slot.Id,
            slot.AttractionId,
            slot.Attraction!.Name,
            slot.StartTime,
            slot.EndTime,
            slot.PricePerPerson,
            slot.MaxCapacity,
            slot.BookedCapacity,
            slot.AvailableCapacity,
            slot.Attraction.IsActive,
            string.Equals(slot.Attraction.Status, "Approved", StringComparison.Ordinal))).ToList();
    }
}
