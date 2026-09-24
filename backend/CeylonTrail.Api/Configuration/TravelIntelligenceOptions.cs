namespace CeylonTrail.Api.Configuration;

public sealed class TravelIntelligenceOptions
{
    public const string SectionName = "TravelIntelligence";

    public string BaseUrl { get; set; } = "http://localhost:8001";

    public int TimeoutSeconds { get; set; } = 20;
}
