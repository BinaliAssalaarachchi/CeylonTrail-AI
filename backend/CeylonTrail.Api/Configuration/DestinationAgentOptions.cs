namespace CeylonTrail.Api.Configuration;

public sealed class DestinationAgentOptions
{
    public const string SectionName = "DestinationAgent";
    public string BaseUrl { get; set; } = "http://localhost:8003";
    public int TimeoutSeconds { get; set; } = 60;
}
