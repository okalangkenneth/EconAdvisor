namespace EconAdvisor.Api.Models;

public class IndicatorSeries
{
    public int Id { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code, e.g. "SE".</summary>
    public required string CountryCode { get; set; }

    /// <summary>Slug identifier, e.g. "policy_rate", "cpif", "unemployment", "gdp_growth".</summary>
    public required string SeriesKey { get; set; }

    /// <summary>Human-readable name for display.</summary>
    public required string Name { get; set; }

    /// <summary>Source API: "riksbank" | "scb" | "worldbank".</summary>
    public required string Source { get; set; }

    /// <summary>Unit string, e.g. "percent", "percent_yoy".</summary>
    public required string Unit { get; set; }

    public ICollection<IndicatorObservation> Observations { get; set; } = [];
}
