namespace CeylonTrail.Api.Models;

public class ValidationResult
{
    public Guid Id { get; set; }

    public string? TripReference { get; set; }

    public ValidationOverallStatus OverallStatus { get; set; }

    public ValidationRiskLevel RiskLevel { get; set; }

    public bool IsFeasible { get; set; }

    public int TotalIssueCount { get; set; }

    public int BlockingIssueCount { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
}
