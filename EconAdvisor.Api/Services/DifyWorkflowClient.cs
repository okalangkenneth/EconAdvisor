using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EconAdvisor.Api.Services;

/// <summary>
/// Returned when the Dify workflow endpoint is unreachable.
/// Callers should surface this as 503 ProblemDetails.
/// </summary>
public sealed class DifyUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// The result of a successful Dify workflow run.
/// </summary>
public sealed record DifyAnalysisResult(string Analysis, string[] Citations);

/// <summary>
/// Typed HTTP client that invokes the EconAdvisor Dify workflow
/// in blocking mode and deserialises the result.
///
/// Registration (Program.cs):
///   builder.Services.AddHttpClient&lt;DifyWorkflowClient&gt;(…)
///
/// Dify blocking response envelope:
///   { "data": { "status": "succeeded", "outputs": { "analysis": "…", "citations": "[…]" } } }
/// </summary>
public sealed class DifyWorkflowClient(
    HttpClient http,
    ILogger<DifyWorkflowClient> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Runs the Dify workflow and returns analysis + citations.
    /// </summary>
    /// <param name="question">Natural-language question from the user.</param>
    /// <param name="indicatorsJson">Pre-serialised indicator snapshot (JSON string).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="DifyUnavailableException">Dify is unreachable.</exception>
    /// <exception cref="InvalidOperationException">Workflow ran but did not succeed.</exception>
    public async Task<DifyAnalysisResult> AnalyseAsync(
        string question,
        string indicatorsJson,
        CancellationToken ct = default)
    {
        var truncatedQuestion = question.Length > 100
            ? question[..100] + "…"
            : question;

        var requestBody = new
        {
            inputs = new
            {
                question,
                indicators = indicatorsJson,
            },
            response_mode = "blocking",
            user = "econdadvisor-api",
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var sw = Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync("workflows/run", content, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Dify unreachable | question=\"{Question}\"", truncatedQuestion);
            throw new DifyUnavailableException(
                "Dify workflow endpoint is unavailable.", ex);
        }

        sw.Stop();

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Dify HTTP error | status={Status} question=\"{Question}\" body={Body}",
                (int)response.StatusCode, truncatedQuestion, responseBody);
            throw new DifyUnavailableException(
                $"Dify returned HTTP {(int)response.StatusCode}.");
        }

        DifyWorkflowResponse? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<DifyWorkflowResponse>(
                responseBody, JsonOpts);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "Dify response parse error | question=\"{Question}\" body={Body}",
                truncatedQuestion, responseBody);
            throw new InvalidOperationException(
                "Failed to parse Dify workflow response.", ex);
        }

        var data = envelope?.Data;
        var status = data?.Status ?? "(null)";

        logger.LogInformation(
            "Dify call complete | question=\"{Question}\" status={Status} duration_ms={Duration}",
            truncatedQuestion, status, sw.ElapsedMilliseconds);

        if (status != "succeeded")
        {
            logger.LogError(
                "Dify workflow did not succeed | status={Status} error={Error}",
                status, data?.Error ?? "(none)");
            throw new InvalidOperationException(
                $"Dify workflow finished with status '{status}': {data?.Error}");
        }

        var outputs = data!.Outputs;
        var analysis = outputs?.Analysis ?? string.Empty;
        var citationsRaw = outputs?.Citations ?? "[]";

        string[] citations;
        try
        {
            citations = JsonSerializer.Deserialize<string[]>(citationsRaw, JsonOpts)
                        ?? [];
        }
        catch (JsonException)
        {
            logger.LogWarning(
                "Could not parse citations JSON — defaulting to empty | raw={Raw}", citationsRaw);
            citations = [];
        }

        return new DifyAnalysisResult(analysis, citations);
    }

    // ── Private response-shape DTOs ──────────────────────────────────────────

    private sealed class DifyWorkflowResponse
    {
        [JsonPropertyName("data")]
        public DifyRunData? Data { get; init; }
    }

    private sealed class DifyRunData
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("outputs")]
        public DifyOutputs? Outputs { get; init; }
    }

    private sealed class DifyOutputs
    {
        [JsonPropertyName("analysis")]
        public string? Analysis { get; init; }

        [JsonPropertyName("citations")]
        public string? Citations { get; init; }
    }
}
