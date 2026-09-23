using System.Text.Json.Serialization;
using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.ItineraryValidations;

public sealed class ItineraryValidationResponse
{
    public Guid Id { get; set; }

    public string? TripReference { get; set; }

    [JsonIgnore]
    public Guid CreatedByUserId { get; set; }

    public ValidationOverallStatus OverallStatus { get; set; }

    public ValidationRiskLevel RiskLevel { get; set; }

    public bool IsFeasible { get; set; }

    public int TotalIssueCount { get; set; }

    public int BlockingIssueCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public IReadOnlyList<ValidationIssueResponse> Issues { get; set; } = Array.Empty<ValidationIssueResponse>();
}

public sealed class ValidationIssueResponse
{
    public Guid Id { get; set; }

    public ValidationIssueType IssueType { get; set; }

    public ValidationIssueSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;

    public string RuleCode { get; set; } = string.Empty;

    public bool IsBlocking { get; set; }

    public string? RelatedDistrict { get; set; }

    public string? RelatedItemReference { get; set; }

    public DateTime CreatedAt { get; set; }
}
