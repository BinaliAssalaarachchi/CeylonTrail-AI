namespace CeylonTrail.Api.Models;

public class ValidationIssue
{
    public Guid Id { get; set; }

    public Guid ValidationResultId { get; set; }

    public ValidationResult? ValidationResult { get; set; }

    public ValidationIssueType IssueType { get; set; }

    public ValidationIssueSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;

    public string RuleCode { get; set; } = string.Empty;

    public bool IsBlocking { get; set; }

    public string? RelatedDistrict { get; set; }

    public string? RelatedItemReference { get; set; }

    public DateTime CreatedAt { get; set; }
}
