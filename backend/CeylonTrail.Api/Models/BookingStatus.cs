namespace CeylonTrail.Api.Models;

public enum BookingStatus
{
    Draft,
    PendingAI,
    PendingHumanApproval,
    Confirmed,
    Cancelled,
    Rejected
}
