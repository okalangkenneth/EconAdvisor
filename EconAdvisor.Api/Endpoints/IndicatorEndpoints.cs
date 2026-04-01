using EconAdvisor.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EconAdvisor.Api.Endpoints;

public static class IndicatorEndpoints
{
    public static IEndpointRouteBuilder MapIndicatorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/indicators/{country}/{series}",
            async (string country, string series,
                   IndicatorService svc, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(series))
                    return Results.Problem(
                        detail: "country and series are required.",
                        statusCode: 400, title: "Bad Request");

                try
                {
                    var result = await svc.GetIndicatorAsync(
                        country.ToUpperInvariant(), series.ToLowerInvariant(), ct);

                    if (result is null)
                        return Results.Problem(
                            detail: $"Unknown series '{series}'. " +
                                    "Supported: policy_rate, yield_2y, yield_10y, cpif, " +
                                    "unemployment, gdp_growth, real_interest_rate, yield_curve_slope.",
                            statusCode: 404, title: "Series Not Found");

                    return Results.Ok(result);
                }
                catch (HttpRequestException ex)
                {
                    return Results.Problem(
                        detail: $"Upstream data source unavailable: {ex.Message}",
                        statusCode: 503, title: "Service Unavailable");
                }
            })
            .WithName("GetIndicator")
            .WithOpenApi(op =>
            {
                op.Summary     = "Fetch economic indicator observations";
                op.Description =
                    "Returns cached + live observations for one indicator series. " +
                    "Cache TTL is 1 hour; a cache miss triggers a live source fetch. " +
                    "Supported series: policy_rate | yield_2y | yield_10y | cpif | " +
                    "unemployment | gdp_growth | real_interest_rate | yield_curve_slope";
                return op;
            });

        return app;
    }
}
