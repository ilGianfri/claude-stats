using System.Net.Http;

namespace ClaudeStats.Services;

/// <summary>
/// Builds a compact, token-safe description of a failed HTTP response for diagnostic logging:
/// the request line, status, the response headers that discriminate an edge/WAF block from an
/// application-layer rate limit or OAuth error, and a short body snippet. Only the <em>response</em>
/// is inspected — never the request (whose <c>Authorization</c> header carries the bearer token) —
/// and the body is read only for failures, which carry no secrets.
/// </summary>
internal static class HttpDiagnostics
{
    /// <summary>Response headers most useful for telling apart a Cloudflare/WAF block, an application
    /// rate limit, and an OAuth error.</summary>
    private static readonly string[] NamedHeaders =
    [
        "retry-after", "x-should-retry", "request-id", "x-request-id",
        "cf-ray", "cf-mitigated", "cf-cache-status", "server", "via", "content-type",
    ];

    /// <summary>Header-name prefixes to always include (e.g. every <c>anthropic-ratelimit-*</c> header).</summary>
    private static readonly string[] IncludedPrefixes =
    [
        "anthropic-", "x-ratelimit", "ratelimit",
    ];

    /// <summary>Maximum number of body characters to include in the snippet.</summary>
    private const int MaxBodyChars = 500;

    /// <summary>Produces a single-line, token-safe summary of a failed HTTP response.</summary>
    /// <param name="response">The HTTP response to describe.</param>
    /// <param name="ct">Cancellation token for reading the body.</param>
    /// <returns>A diagnostic string containing the request line, status, selected headers, and a body snippet.</returns>
    public static async Task<string> DescribeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        HttpRequestMessage? request = response.RequestMessage;
        string method = request?.Method.Method ?? "?";
        string host = request?.RequestUri?.Host ?? "?";
        string path = request?.RequestUri?.AbsolutePath ?? "?";
        int status = (int)response.StatusCode;

        string headers = FormatHeaders(response);
        (string kind, string snippet) = await ReadBodyAsync(response, ct);

        return $"{method} {host}{path} -> {status} ({response.StatusCode}); "
            + $"headers[{headers}]; body[{kind}]=\"{snippet}\"";
    }

    /// <summary>Collects the discriminating response headers into a compact string.</summary>
    /// <param name="response">The response whose headers to format.</param>
    /// <returns>A semicolon-separated list of selected headers, or "(none)" when none matched.</returns>
    private static string FormatHeaders(HttpResponseMessage response)
    {
        List<string> parts = [];
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> all =
            response.Headers.Concat(response.Content.Headers);

        foreach (KeyValuePair<string, IEnumerable<string>> header in all)
        {
            string name = header.Key.ToLowerInvariant();
            bool wanted = Array.IndexOf(NamedHeaders, name) >= 0
                || IncludedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
            if (wanted)
            {
                parts.Add($"{name}={string.Join(",", header.Value)}");
            }
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "(none)";
    }

    /// <summary>Reads a short, sanitized snippet of the response body and classifies it as HTML/JSON/text.</summary>
    /// <param name="response">The response whose body to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of the detected body kind and a single-line, truncated snippet.</returns>
    private static async Task<(string Kind, string Snippet)> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception)
        {
            // A body we cannot read (network drop, decompression issue) is itself a useful signal.
            return ("unread", string.Empty);
        }

        if (string.IsNullOrEmpty(body))
        {
            return ("empty", string.Empty);
        }

        string trimmed = body.TrimStart();
        string kind =
            trimmed.StartsWith('<') || trimmed.Contains("<html", StringComparison.OrdinalIgnoreCase) ? "HTML"
            : trimmed.StartsWith('{') || trimmed.StartsWith('[') ? "JSON"
            : "text";

        string oneLine = body.Replace('\r', ' ').Replace('\n', ' ').Replace('"', '\'');
        if (oneLine.Length > MaxBodyChars)
        {
            oneLine = string.Concat(oneLine.AsSpan(0, MaxBodyChars), "…");
        }

        return (kind, oneLine);
    }
}
