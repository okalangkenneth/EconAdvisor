namespace EconAdvisor.Api.Models;

public class IndicatorObservation
{
    public long Id { get; set; }

    public int SeriesId { get; set; }
    public IndicatorSeries Series { get; set; } = null!;

    /// <summary>Observation date (day precision; use first-of-month for monthly series).</summary>
    public DateOnly ObservationDate { get; set; }

    public decimal Value { get; set; }

    /// <summary>UTC timestamp when this row was fetched from the source API.</summary>
    public DateTime FetchedAt { get; set; }
}
