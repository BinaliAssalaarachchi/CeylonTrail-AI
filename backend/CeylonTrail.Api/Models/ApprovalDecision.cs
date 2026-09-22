namespace CeylonTrail.Api.Models;

public class ApprovalDecision
{
    public Guid Id { get; set; }

    public Guid ApprovalRequestId { get; set; }

    public ApprovalRequest? ApprovalRequest { get; set; }

    public Guid DecidedByUserId { get; set; }

    public User? DecidedByUser { get; set; }

    public ApprovalDecisionType Decision { get; set; }

    public string? Comment { get; set; }

    public DateTime DecidedAt { get; set; }
}
