namespace EconAdvisor.Api.Models;

public sealed record AnalyseResponse(
    string Question,
    string Country,
    Dictionary<string, decimal?> Indicators,
    string Analysis,
    string[] Citations,
    DateTime GeneratedAt);
