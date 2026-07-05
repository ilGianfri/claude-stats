using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using ClaudeStats.Json;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Checks for new ClaudeStats releases on GitHub.
/// Uses the <c>gh</c> CLI token (if available) to avoid anonymous rate limits.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private const string Owner = "ilGianfri";
    private const string Repo = "claude-stats";

    private readonly HttpClient _http;
    private readonly ILogger<GitHubUpdateChecker> _logger;

    /// <summary>Initializes the checker and attaches a GitHub token when the <c>gh</c> CLI is present.</summary>
    public GitHubUpdateChecker(HttpClient http, ILogger<GitHubUpdateChecker> logger)
    {
        _http = http;
        _logger = logger;

        string? token = TryGetGhToken();
        if (token is not null)
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <inheritdoc />
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(
                $"repos/{Owner}/{Repo}/releases/latest", ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Update check returned HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            await using Stream stream = await response.Content
                .ReadAsStreamAsync(ct).ConfigureAwait(false);

            GitHubReleaseDto? release = JsonSerializer.Deserialize(
                stream, AppJsonContext.Default.GitHubReleaseDto);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                return null;

            string tag = release.TagName.TrimStart('v');
            if (!Version.TryParse(tag, out Version? latest))
                return null;

            Version current = Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version(0, 0, 0, 0);

            _logger.LogDebug("Update check: current={Current} latest={Latest}", current, latest);

            return latest > current ? new UpdateInfo(latest, release.HtmlUrl) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Update check failed");
            return null;
        }
    }

    /// <summary>Runs <c>gh auth token</c> and returns the token if the CLI is installed and authenticated.</summary>
    private static string? TryGetGhToken()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = "auth token",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (proc is null) return null;

            string token = proc.StandardOutput.ReadLine()?.Trim() ?? string.Empty;
            proc.WaitForExit();

            return proc.ExitCode == 0 && token.Length > 0 ? token : null;
        }
        catch
        {
            return null;
        }
    }
}
