using CeylonTrail.Api.DTOs.BookingAction;

namespace CeylonTrail.Api.Interfaces;

public sealed record BookingActionAgentServiceResult(
    BookingActionAgentResponse? Value = null,
    string? Error = null,
    bool ServiceUnavailable = false)
{
    public bool Succeeded => Value is not null && Error is null;
}

public interface IBookingActionAgentService
{
    Task<BookingActionAgentServiceResult> PrepareAsync(
        BookingActionAgentRequest request,
        CancellationToken cancellationToken = default);
}
