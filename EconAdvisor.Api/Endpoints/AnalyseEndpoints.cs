namespace EconAdvisor.Api.Endpoints;

public static class AnalyseEndpoints
{
    public static IEndpointRouteBuilder MapAnalyseEndpoints(this IEndpointRouteBuilder app)
    {
        // Phase 4: POST /api/analyse
        app.MapPost("/api/analyse", () =>
            Results.Ok(new { message = "Phase 4 — not yet implemented" }))
            .WithName("Analyse")
            .WithOpenApi();

        return app;
    }
}
