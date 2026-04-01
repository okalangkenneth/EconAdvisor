using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EconAdvisor.Api.Services;

/// <summary>
/// Typed HTTP client for the SCB PxWeb v1 API.
/// Base URL configured via IHttpClientFactory (name: "scb").
///
/// Uses POST with a PxWeb selection query and requests json-stat format.
///
/// Verified table paths and variable codes (queried from SCB API directly):
///
///   CPIF (12-month change %):
///     POST PR/PR0101/PR0101G/KPIF2020
///     ContentsCode = "000007ZM" (KPIF, 12-månadsförändring, 2020=100)
///
///   Unemployment rate (%):
///     POST AM/AM0401/AM0401A/AKURLBefM
///     Arbetskraftstillh = "ALÖSP"   (arbetslöshetstal, procent)
///     TypData           = "O_DATA"  (icke säsongrensad)
///     Kon               = "1+2"     (totalt — NOTE: code is "Kon", not "Kön")
///     Alder             = "tot15-74"
///     ContentsCode      = "000007L9"
/// </summary>
public sealed class ScbClient(HttpClient http, ILogger<ScbClient> logger)
{
    // ── Table paths ──────────────────────────────────────────────────────────
    public const string CpifTablePath          = "PR/PR0101/PR0101G/KPIF2020";
    public const string UnemploymentTablePath  = "AM/AM0401/AM0401A/AKURLBefM";

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── CPIF ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetch the CPIF 12-month change (%) — last <paramref name="periods"/> months.
    /// Returns (DateOnly = first-of-month, decimal value).
    /// </summary>
    public async Task<IReadOnlyList<(DateOnly Date, decimal Value)>> GetCpifAsync(
        int periods = 24, CancellationToken ct = default)
    {
        var query = BuildQuery(
            new PxSelection("ContentsCode", "item", ["000007ZM"]),
            new PxSelection("Tid",          "top",  [$"{periods}"]));

        return await PostAndParseAsync(CpifTablePath, query, "cpif",
            timeDimName: "Tid", ct: ct);
    }

    // ── Unemployment ─────────────────────────────────────────────────────────

    /// <summary>
    /// Fetch the unemployment rate 15-74, total, non-seasonally adjusted
    /// — last <paramref name="periods"/> months.
    /// </summary>
    public async Task<IReadOnlyList<(DateOnly Date, decimal Value)>> GetUnemploymentAsync(
        int periods = 24, CancellationToken ct = default)
    {
        // Variable codes verified from SCB API metadata (GET table path):
        //   "Kön"                    → code = "Kon"
        //   "Arbetskraftstillhörighet" → code = "Arbetskraftstillh"
        var query = BuildQuery(
            new PxSelection("ContentsCode",     "item", ["000007L9"]),
            new PxSelection("Arbetskraftstillh","item", ["ALÖSP"]),
            new PxSelection("TypData",          "item", ["O_DATA"]),
            new PxSelection("Kon",              "item", ["1+2"]),
            new PxSelection("Alder",            "item", ["tot15-74"]),
            new PxSelection("Tid",              "top",  [$"{periods}"]));

        return await PostAndParseAsync(UnemploymentTablePath, query, "unemployment",
            timeDimName: "Tid", ct: ct);
    }

    // ── Core: POST + JSON-stat parse ─────────────────────────────────────────

    private async Task<IReadOnlyList<(DateOnly Date, decimal Value)>> PostAndParseAsync(
        string tablePath, string queryJson, string seriesLabel,
        string timeDimName, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var content = new StringContent(queryJson, Encoding.UTF8, "application/json");
            var resp = await http.PostAsync(tablePath, content, ct);
            sw.Stop();

            logger.LogInformation(
                "SCB API | series={Series} table={Table} status={Status} duration={Duration}ms",
                seriesLabel, tablePath, (int)resp.StatusCode, sw.ElapsedMilliseconds);

            resp.EnsureSuccessStatusCode();

            var raw = await resp.Content.ReadAsStringAsync(ct);
            return ParseJsonStat(raw, timeDimName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "SCB API error | series={Series} table={Table} duration={Duration}ms",
                seriesLabel, tablePath, sw.ElapsedMilliseconds);
            throw;
        }
    }

    // ── JSON-stat parser ─────────────────────────────────────────────────────
    // JSON-stat structure (abbreviated):
    // {
    //   "dataset": {
    //     "dimension": {
    //       "id": ["ContentsCode", "Tid"],
    //       "size": [1, 24],
    //       "Tid": {
    //         "label": "månad",
    //         "category": {
    //           "index": {"2024M01":0, "2024M02":1, ...},
    //           "label": {...}
    //         }
    //       },
    //       ...
    //     },
    //     "value": [2.3, 1.8, ...]   // flat row-major array
    //   }
    // }
    //
    // Since all non-time dimensions are fixed to a single value (size=1),
    // the flat index directly equals the Tid position index.

    private static IReadOnlyList<(DateOnly Date, decimal Value)> ParseJsonStat(
        string raw, string timeDimName)
    {
        using var doc  = JsonDocument.Parse(raw);
        var dataset    = doc.RootElement.GetProperty("dataset");
        var dimension  = dataset.GetProperty("dimension");
        var dimIds     = dimension.GetProperty("id");
        var dimSizes   = dimension.GetProperty("size");
        var valueArray = dataset.GetProperty("value");

        // Build size array for stride computation
        int nDims  = dimIds.GetArrayLength();
        var sizes  = new int[nDims];
        for (int i = 0; i < nDims; i++)
            sizes[i] = dimSizes[i].GetInt32();

        // Find the time dimension index
        int timeDimIdx = -1;
        for (int i = 0; i < nDims; i++)
        {
            if (dimIds[i].GetString() == timeDimName)
            { timeDimIdx = i; break; }
        }
        if (timeDimIdx < 0)
            throw new InvalidOperationException(
                $"Time dimension '{timeDimName}' not found in JSON-stat response.");

        // Compute stride for the time dimension
        // flat_index = sum(idx[d] * stride[d]);  stride[d] = product(size[d+1..n-1])
        int stride = 1;
        for (int d = timeDimIdx + 1; d < nDims; d++)
            stride *= sizes[d];

        // Build Tid code → (periodCode, flatOffset) map
        var timeDim  = dimension.GetProperty(timeDimName);
        var catIndex = timeDim.GetProperty("category").GetProperty("index");

        // catIndex is a JSON object: {"2024M01": 0, "2024M02": 1, ...}
        var periods  = new List<(string Code, int Position)>();
        foreach (var prop in catIndex.EnumerateObject())
            periods.Add((prop.Name, prop.Value.GetInt32()));

        // Extract results
        var results = new List<(DateOnly, decimal)>(periods.Count);
        foreach (var (code, pos) in periods)
        {
            int flatIdx = pos * stride; // all other dims are idx=0
            var elem    = valueArray[flatIdx];
            if (elem.ValueKind == JsonValueKind.Null) continue;
            if (!elem.TryGetDecimal(out var val))      continue;

            var date = ParseScbPeriod(code);
            if (date.HasValue)
                results.Add((date.Value, val));
        }

        results.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return results;
    }

    /// <summary>Parse "2024M01" → DateOnly(2024, 1, 1).</summary>
    private static DateOnly? ParseScbPeriod(string code)
    {
        // Format: YYYYMmm  (e.g. "2024M01")
        if (code.Length >= 7 &&
            int.TryParse(code[..4],    out int year) &&
            code[4] == 'M' &&
            int.TryParse(code[5..],    out int month) &&
            month >= 1 && month <= 12)
        {
            return new DateOnly(year, month, 1);
        }
        return null;
    }

    // ── PxWeb query builder ──────────────────────────────────────────────────

    private static string BuildQuery(params PxSelection[] selections)
    {
        var obj = new
        {
            query = selections.Select(s => new
            {
                code      = s.Code,
                selection = new { filter = s.Filter, values = s.Values }
            }).ToArray(),
            response = new { format = "json-stat" }
        };
        return JsonSerializer.Serialize(obj);
    }

    private sealed record PxSelection(string Code, string Filter, string[] Values);
}
