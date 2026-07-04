using System.IO;
using System.Text.Json;
using ClaudeStats.Json;
using ClaudeStats.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Reads and atomically writes <c>%USERPROFILE%\.claude\.credentials.json</c>, preserving all
/// keys other than the refreshed <c>claudeAiOauth</c> token fields. Token values are never logged.
/// </summary>
public sealed class CredentialStore : ICredentialStore
{
    private readonly ILogger<CredentialStore> _logger;
    private readonly string _path;

    /// <summary>Initializes the store using the default credentials path under the user profile.</summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public CredentialStore(ILogger<CredentialStore> logger)
        : this(logger, DefaultPath())
    {
    }

    /// <summary>Initializes the store with an explicit credentials file path (used by tests).</summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="path">Absolute path to the credentials file.</param>
    public CredentialStore(ILogger<CredentialStore> logger, string path)
    {
        _logger = logger;
        _path = path;
    }

    /// <summary>Computes the default credentials file path.</summary>
    /// <returns>The absolute path to <c>~/.claude/.credentials.json</c>.</returns>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude",
        ".credentials.json");

    /// <inheritdoc />
    public async Task<OAuthCredentials?> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            await using FileStream fs = new(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            CredentialsFileDto? dto = await JsonSerializer.DeserializeAsync(
                fs, AppJsonContext.Default.CredentialsFileDto, ct);

            ClaudeAiOAuthDto? oauth = dto?.ClaudeAiOauth;
            if (oauth?.AccessToken is not { Length: > 0 } accessToken)
            {
                return null;
            }

            return new OAuthCredentials
            {
                AccessToken = accessToken,
                RefreshToken = oauth.RefreshToken ?? string.Empty,
                ExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(oauth.ExpiresAt),
                SubscriptionType = oauth.SubscriptionType,
                OrganizationUuid = dto?.OrganizationUuid,
            };
        }
        catch (JsonException ex)
        {
            // A mid-write partial read can fail to parse; treat as transiently unavailable.
            _logger.LogWarning(ex, "Credentials file could not be parsed (possibly mid-write).");
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Credentials file could not be read.");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(OAuthCredentials updated, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(updated);

        // Re-read immediately before writing so we preserve the latest non-token keys.
        CredentialsFileDto dto = await ReadRawAsync(ct) ?? new CredentialsFileDto();
        dto.ClaudeAiOauth ??= new ClaudeAiOAuthDto();
        dto.ClaudeAiOauth.AccessToken = updated.AccessToken;
        dto.ClaudeAiOauth.RefreshToken = updated.RefreshToken;
        dto.ClaudeAiOauth.ExpiresAt = updated.ExpiresAt.ToUnixTimeMilliseconds();
        if (updated.SubscriptionType is not null)
        {
            dto.ClaudeAiOauth.SubscriptionType = updated.SubscriptionType;
        }

        string json = JsonSerializer.Serialize(dto, AppJsonContext.Default.CredentialsFileDto);

        string? dir = Path.GetDirectoryName(_path);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }

        // Atomic replace: write to a temp file in the same directory, then move over the target.
        string tempPath = _path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _path, overwrite: true);
    }

    /// <summary>Reads the raw credentials DTO (including preserved unknown members).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized DTO, or null when absent/unreadable.</returns>
    private async Task<CredentialsFileDto?> ReadRawAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            await using FileStream fs = new(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync(fs, AppJsonContext.Default.CredentialsFileDto, ct);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Could not re-read credentials before write; preserving may be incomplete.");
            return null;
        }
    }
}
