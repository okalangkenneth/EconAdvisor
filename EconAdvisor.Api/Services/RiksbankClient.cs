using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EconAdvisor.Api.Services;

/// <summary>
/// Typed HTTP client for the Riksbank SWEA v1 REST API.
/// Base URL configured via IHttpClientFactory (name: "riksbank").
/// Verified response shape: GET /observations/{seriesId}/{from}/{to}
///   → [ {"date":"YYYY-MM-DD","value":4.0}, ... ]
/// </summary>
public sealed class RiksbankClient(HttpClient http, ILogger<RiksbankClient> logger)
{
    // ── Series IDs ──────────────────────────────────────────────────────────
    public const string PolicyRateSeries  = "SECBREPOEFF";   // Effective repo rate (daily)
    public const string Yield2ySeries     = "SEGVB2YC";      // 2yr gov bond yield (daily)
    public const string Yield10ySeries    = "SEGVB10YC";     // 10yr gov bond yield (daily)

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Fetch daily observations for a Riksbank series over the given date range.
    /// Null/missing values are skipped.
    /// </summary>
    public async Task<IReadOnlyList<(DateOnly Date, decimal Value)>> GetObservationsAsync(
        string seriesId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var url = $"observations/{seriesId}/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}";
        var sw  = Stopwatch.StartNew();

        try
        {
            var resp = await http.GetAsync(url, ct);
            sw.Stop();

            logger.LogInformation(
                "Riksbank API | series={Series} from={From} to={To} " +
                "status={Status} duration={Duration}ms",
                seriesId, from, to, (int)resp.StatusCode, sw.ElapsedMilliseconds);

            resp.EnsureSuccessStatusCode();

            var raw = await resp.Content.ReadAsStringAsync(ct);
            var obs = JsonSerializer.Deserialize<List<RiksbankObs>>(raw, JsonOpts) ?? [];

            return obs
                .Where(o => o.Value.HasValue)
                .Select(o => (DateOnly.ParseExact(o.Date, "yyyy-MM-dd",
                              System.Globalization.CultureInfo.InvariantCulture),
                              o.Value!.Value))
                .ToList();
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "Riksbank API error | series={Series} duration={Duration}ms",
                seriesId, sw.ElapsedMilliseconds);
            throw;
        }
    }

    // ── Private DTO ──────────────────────────────────────────────────────────
    private sealed record RiksbankObs(
        [property: JsonPropertyName("date")]  string  Date,
        [property: JsonPropertyName("value")] decimal? Value);
}
