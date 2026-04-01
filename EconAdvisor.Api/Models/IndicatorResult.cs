namespace EconAdvisor.Api.Models;

/// <summary>API response DTO for GET /api/indicators/{country}/{series}.</summary>
public sealed record IndicatorResult(
    string Country,
    string Series,
    string Name,
    string Source,
    string Unit,
    IReadOnlyList<ObservationDto> Observations);

public sealed record ObservationDto(DateOnly Date, decimal Value);
