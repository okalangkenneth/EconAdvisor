using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EconAdvisor.Api.Services;

/// <summary>
/// Typed HTTP client for World Bank API v2.
/// Base URL configured via IHttpClientFactory (name: "worldbank").
/// Verified response shape:
///   GET /country/{cc}/indicator/{code}?format=json&amp;mrv={n}
///   → [ {metadata}, [ {"date":"2024","value":0.82, ...}, ... ] ]
/// </summary>
public sealed class WorldBankClient(HttpClient http, ILogger<WorldBankClient> logger)
{
    public const string SwedenGdpGrowth = "NY.GDP.MKTP.KD.ZG";

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Returns annual GDP-growth observations for the given country + indicator.
    /// date "2024" is mapped to DateOnly(2024, 1, 1).
    /// Null values (not yet published years) are skipped.
    /// </summary>
    public async Task<IReadOnlyList<(DateOnly Date, decimal Value)>> GetObservationsAsync(
        string countryCode, string indicatorCode, int mostRecentYears = 15,
        CancellationToken ct = default)
    {
        var url = $"country/{countryCode}/indicator/{indicatorCode}" +
                  $"?format=json&mrv={mostRecentYears}";
        var sw  = Stopwatch.StartNew();

        try
        {
            var resp = await http.GetAsync(url, ct);
            sw.Stop();

            logger.LogInformation(
                "WorldBank API | country={Country} indicator={Indicator} " +
                "status={Status} duration={Duration}ms",
                countryCode, indicatorCode, (int)resp.StatusCode, sw.ElapsedMilliseconds);

            resp.EnsureSuccessStatusCode();

            var raw  = await resp.Content.ReadAsStringAsync(ct);
            // Outer array: [0] = pagination metadata, [1] = data array
            using var doc  = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 2)
                return [];

            var dataArray = root[1];
            if (dataArray.ValueKind != JsonValueKind.Array)
                return [];

            var results = new List<(DateOnly, decimal)>();

            foreach (var item in dataArray.EnumerateArray())
            {
                if (!item.TryGetProperty("date",  out var dateProp))  continue;
                if (!item.TryGetProperty("value", out var valueProp)) continue;

                if (valueProp.ValueKind == JsonValueKind.Null) continue;

                if (!int.TryParse(dateProp.GetString(), out var year)) continue;
                if (!valueProp.TryGetDecimal(out var value))           continue;

                results.Add((new DateOnly(year, 1, 1), value));
            }

            // Sort ascending by date
            results.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return results;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "WorldBank API error | country={Country} indicator={Indicator} duration={Duration}ms",
                countryCode, indicatorCode, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
