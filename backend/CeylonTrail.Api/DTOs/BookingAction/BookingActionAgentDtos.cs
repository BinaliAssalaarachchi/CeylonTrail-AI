namespace CeylonTrail.Api.DTOs.BookingAction;

public sealed record BookingActionAgentRequest(
    Guid WorkflowId,
    Guid TripId,
    int GuestCount,
    decimal? RemainingBudget,
    IReadOnlyList<Guid> SelectedAttractionIds,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record BookingActionExecutionRequest(
    Guid WorkflowId,
    Guid TripId,
    int GuestCount,
    decimal? RemainingBudget,
    IReadOnlyList<Guid> SelectedAttractionIds,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<TrustedBookingAvailabilitySlot> TrustedAvailabilitySlots);

public sealed record TrustedBookingAvailabilitySlot(
    Guid AvailabilitySlotId,
    Guid AttractionId,
    string AttractionName,
    DateTime StartTime,
    DateTime EndTime,
    decimal PricePerPerson,
    int MaxCapacity,
    int BookedCapacity,
    int AvailableCapacity,
    bool IsActive,
    bool IsApproved);

public sealed record BookingActionIssue(
    string Code,
    string Message,
    Guid? AvailabilitySlotId);

public sealed record BookingActionProposal(
    Guid AttractionId,
    Guid AvailabilitySlotId,
    int GuestCount,
    decimal UnitPrice,
    decimal TotalPrice,
    DateTime StartTime,
    DateTime EndTime,
    string Reason);

public sealed record BookingActionAgentResponse(
    Guid WorkflowId,
    Guid TripId,
    string Status,
    IReadOnlyList<BookingActionProposal> Proposals,
    IReadOnlyList<BookingActionIssue> Issues,
    bool RequiresApproval,
    string Summary);
