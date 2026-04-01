namespace EconAdvisor.Api.Endpoints;

public static class IndicatorEndpoints
{
    public static IEndpointRouteBuilder MapIndicatorEndpoints(this IEndpointRouteBuilder app)
    {
        // Phase 2: GET /api/indicators/{country}/{series}
        app.MapGet("/api/indicators/{country}/{series}", (string country, string series) =>
            Results.Ok(new { country, series, message = "Phase 2 — not yet implemented" }))
            .WithName("GetIndicator")
            .WithOpenApi();

        return app;
    }
}
