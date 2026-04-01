using EconAdvisor.Api.Data;
using EconAdvisor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EconAdvisor.Api.Services;

/// <summary>
/// Orchestrates fetch-or-cache for all economic indicators.
///
/// Cache strategy (TTL = 1 hour per series):
///   1. Look up IndicatorSeries record (create if missing).
///   2. Check MAX(fetched_at) of its observations.
///   3. If MAX(fetched_at) &lt; NOW() − 1h → re-fetch from source and upsert.
///   4. Return observations from PostgreSQL.
///
/// Derived indicators (real_interest_rate, yield_curve_slope) are computed
/// on the fly from cached base series — never stored as rows.
/// </summary>
public sealed class IndicatorService(
    EconContext db,
    RiksbankClient riksbank,
    ScbClient scb,
    WorldBankClient worldBank,
    ILogger<IndicatorService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    // ── Series catalogue ─────────────────────────────────────────────────────
    // Maps seriesKey → metadata used when inserting an IndicatorSeries row.
    private static readonly Dictionary<string, SeriesMeta> Catalogue = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["policy_rate"]        = new("Policy Rate (Riksbank)",            "riksbank",  "percent"),
        ["yield_2y"]           = new("Sweden 2yr Gov Bond Yield",          "riksbank",  "percent"),
        ["yield_10y"]          = new("Sweden 10yr Gov Bond Yield",         "riksbank",  "percent"),
        ["cpif"]               = new("CPIF Inflation (12m change)",        "scb",       "percent_yoy"),
        ["unemployment"]       = new("Unemployment Rate 15-74",            "scb",       "percent"),
        ["gdp_growth"]         = new("Sweden GDP Growth (annual %)",       "worldbank", "percent_yoy"),
        ["real_interest_rate"] = new("Real Interest Rate (policy − CPIF)", "derived",   "percent"),
        ["yield_curve_slope"]  = new("Yield Curve Slope (10y − 2y)",       "derived",   "percent"),
    };

    // Derived series — computed from others, not stored in DB.
    private static readonly HashSet<string> DerivedKeys = new(StringComparer.OrdinalIgnoreCase)
        { "real_interest_rate", "yield_curve_slope" };

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<IndicatorResult?> GetIndicatorAsync(
        string country, string seriesKey, CancellationToken ct = default)
    {
        if (!Catalogue.TryGetValue(seriesKey, out var meta))
            return null;

        if (DerivedKeys.Contains(seriesKey))
            return await GetDerivedAsync(country, seriesKey, meta, ct);

        // ── Stored series ────────────────────────────────────────────────────
        var series = await EnsureSeriesRowAsync(country, seriesKey, meta, ct);

        var cacheExpiry = DateTime.UtcNow - CacheTtl;
        var latestFetch = await db.IndicatorObservations
            .Where(o => o.SeriesId == series.Id)
            .MaxAsync(o => (DateTime?)o.FetchedAt, ct);

        if (latestFetch == null || latestFetch < cacheExpiry)
        {
            logger.LogInformation(
                "Cache miss | country={Country} series={Series} — fetching from {Source}",
                country, seriesKey, meta.Source);

            var fetched = await FetchFromSourceAsync(country, seriesKey, ct);
            await UpsertObservationsAsync(series.Id, fetched, ct);
        }
        else
        {
            logger.LogInformation(
                "Cache hit | country={Country} series={Series} fetchedAt={FetchedAt}",
                country, seriesKey, latestFetch);
        }

        var observations = await db.IndicatorObservations
            .Where(o => o.SeriesId == series.Id)
            .OrderBy(o => o.ObservationDate)
            .Select(o => new ObservationDto(o.ObservationDate, o.Value))
            .ToListAsync(ct);

        return new IndicatorResult(country, seriesKey, meta.Name,
            meta.Source, meta.Unit, observations);
    }

    // ── Derived series ────────────────────────────────────────────────────────

    private async Task<IndicatorResult> GetDerivedAsync(
        string country, string seriesKey, SeriesMeta meta, CancellationToken ct)
    {
        IReadOnlyList<ObservationDto> observations = seriesKey.ToLowerInvariant() switch
        {
            "real_interest_rate" => await ComputeRealRateAsync(country, ct),
            "yield_curve_slope"  => await ComputeYieldSlopeAsync(country, ct),
            _ => []
        };

        return new IndicatorResult(country, seriesKey, meta.Name,
            meta.Source, meta.Unit, observations);
    }

    /// <summary>real_interest_rate = monthly_avg(policy_rate) − cpif_yoy  per month.</summary>
    private async Task<IReadOnlyList<ObservationDto>> ComputeRealRateAsync(
        string country, CancellationToken ct)
    {
        var policyResult      = await GetIndicatorAsync(country, "policy_rate",  ct);
        var cpifResult        = await GetIndicatorAsync(country, "cpif",         ct);

        if (policyResult is null || cpifResult is null) return [];

        // Average policy rate per calendar month
        var avgRateByMonth = policyResult.Observations
            .GroupBy(o => new YearMonth(o.Date.Year, o.Date.Month))
            .ToDictionary(
                g => g.Key,
                g => g.Average(o => (double)o.Value));

        // CPIF observations are already monthly (first-of-month date)
        var results = new List<ObservationDto>();
        foreach (var cpif in cpifResult.Observations)
        {
            var ym = new YearMonth(cpif.Date.Year, cpif.Date.Month);
            if (!avgRateByMonth.TryGetValue(ym, out var avgRate)) continue;

            results.Add(new ObservationDto(cpif.Date,
                Math.Round((decimal)avgRate - cpif.Value, 4)));
        }
        return results;
    }

    /// <summary>yield_curve_slope = yield_10y − yield_2y  per day.</summary>
    private async Task<IReadOnlyList<ObservationDto>> ComputeYieldSlopeAsync(
        string country, CancellationToken ct)
    {
        var y10Result = await GetIndicatorAsync(country, "yield_10y", ct);
        var y2Result  = await GetIndicatorAsync(country, "yield_2y",  ct);

        if (y10Result is null || y2Result is null) return [];

        var y2ByDate = y2Result.Observations.ToDictionary(o => o.Date, o => o.Value);

        return y10Result.Observations
            .Where(o => y2ByDate.ContainsKey(o.Date))
            .Select(o => new ObservationDto(o.Date,
                Math.Round(o.Value - y2ByDate[o.Date], 4)))
            .ToList();
    }

    // ── Source fetchers ───────────────────────────────────────────────────────

    private async Task<IReadOnlyList<(DateOnly Date, decimal Value)>> FetchFromSourceAsync(
        string country, string seriesKey, CancellationToken ct)
    {
        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
        var from2Yrs = today.AddYears(-2);

        return seriesKey.ToLowerInvariant() switch
        {
            "policy_rate"  => await riksbank.GetObservationsAsync(
                                  RiksbankClient.PolicyRateSeries, from2Yrs, today, ct),
            "yield_2y"     => await riksbank.GetObservationsAsync(
                                  RiksbankClient.Yield2ySeries,    from2Yrs, today, ct),
            "yield_10y"    => await riksbank.GetObservationsAsync(
                                  RiksbankClient.Yield10ySeries,   from2Yrs, today, ct),
            "cpif"         => await scb.GetCpifAsync(periods: 24, ct: ct),
            "unemployment" => await scb.GetUnemploymentAsync(periods: 24, ct: ct),
            "gdp_growth"   => await worldBank.GetObservationsAsync(
                                  country, WorldBankClient.SwedenGdpGrowth, 15, ct),
            _ => throw new ArgumentException($"Unknown series key: {seriesKey}")
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<IndicatorSeries> EnsureSeriesRowAsync(
        string country, string seriesKey, SeriesMeta meta, CancellationToken ct)
    {
        var series = await db.IndicatorSeries
            .FirstOrDefaultAsync(
                s => s.CountryCode == country && s.SeriesKey == seriesKey, ct);

        if (series is null)
        {
            series = new IndicatorSeries
            {
                CountryCode = country,
                SeriesKey   = seriesKey,
                Name        = meta.Name,
                Source      = meta.Source,
                Unit        = meta.Unit,
            };
            db.IndicatorSeries.Add(series);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Created IndicatorSeries | country={Country} series={Series}",
                country, seriesKey);
        }
        return series;
    }

    /// <summary>
    /// Batch upsert: INSERT … ON CONFLICT DO UPDATE in a single transaction.
    /// </summary>
    private async Task UpsertObservationsAsync(
        int seriesId,
        IReadOnlyList<(DateOnly Date, decimal Value)> observations,
        CancellationToken ct)
    {
        if (observations.Count == 0) return;

        var fetchedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        foreach (var (date, value) in observations)
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO indicator_observations
                    (series_id, observation_date, value, fetched_at)
                VALUES
                    ({seriesId}, {date}, {value}, {fetchedAt})
                ON CONFLICT (series_id, observation_date)
                DO UPDATE SET
                    value      = EXCLUDED.value,
                    fetched_at = EXCLUDED.fetched_at
                """, ct);
        }

        await tx.CommitAsync(ct);

        logger.LogInformation(
            "Upserted {Count} observations for series_id={SeriesId}",
            observations.Count, seriesId);
    }

    // ── Value types ──────────────────────────────────────────────────────────
    private sealed record SeriesMeta(string Name, string Source, string Unit);
    private readonly record struct YearMonth(int Year, int Month);
}
