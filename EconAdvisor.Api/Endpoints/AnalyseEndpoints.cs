using System.Text.Json;
using EconAdvisor.Api.Models;
using EconAdvisor.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace EconAdvisor.Api.Endpoints;

public static class AnalyseEndpoints
{
    private static readonly string[] SnapshotSeries =
    [
        "policy_rate", "cpif", "unemployment",
        "gdp_growth", "real_interest_rate", "yield_curve_slope",
    ];

    public static IEndpointRouteBuilder MapAnalyseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/analyse", async (
                AnalyseRequest request,
                IValidator<AnalyseRequest> validator,
                IndicatorService indicatorService,
                DifyWorkflowClient dify,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("EconAdvisor.Api.Endpoints.Analyse");

                // ── 1. Validate ──────────────────────────────────────────────────
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(validation.ToDictionary());

                // ── 2. Fetch latest value for each series ────────────────────────
                var snapshot = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

                foreach (var key in SnapshotSeries)
                {
                    try
                    {
                        var result = await indicatorService.GetIndicatorAsync(request.Country, key, ct);
                        snapshot[key] = result?.Observations.Count > 0
                            ? result.Observations[^1].Value
                            : null;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Could not fetch indicator | series={Series} country={Country} — using null",
                            key, request.Country);
                        snapshot[key] = null;
                    }
                }

                // ── 3. Serialise snapshot → JSON string for Dify input ───────────
                var indicatorsJson = JsonSerializer.Serialize(snapshot);

                // ── 4. Call Dify workflow ────────────────────────────────────────
                try
                {
                    var difyResult = await dify.AnalyseAsync(request.Question, indicatorsJson, ct);

                    return Results.Ok(new AnalyseResponse(
                        Question:    request.Question,
                        Country:     request.Country,
                        Indicators:  snapshot,
                        Analysis:    difyResult.Analysis,
                        Citations:   difyResult.Citations,
                        GeneratedAt: DateTime.UtcNow));
                }
                catch (DifyUnavailableException ex)
                {
                    logger.LogError(ex, "Dify unavailable during /api/analyse");
                    return Results.Problem(
                        detail:     "The analysis service is temporarily unavailable. Try again shortly.",
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title:      "Analysis service unavailable");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error during /api/analyse");
                    return Results.Problem(
                        detail:     "An unexpected error occurred while generating the analysis.",
                        statusCode: StatusCodes.Status500InternalServerError,
                        title:      "Internal server error");
                }
            })
            .WithName("Analyse")
            .WithOpenApi()
            .Produces<AnalyseResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return app;
    }
}
