using ClaudeStats.Models;

namespace ClaudeStats.Services;

/// <summary>
/// Reads and (on refresh only) writes the local Claude OAuth credentials file.
/// </summary>
public interface ICredentialStore
{
    /// <summary>Reads the current credentials, or null if none are available.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed credentials, or null if the file/section is missing.</returns>
    Task<OAuthCredentials?> ReadAsync(CancellationToken ct);

    /// <summary>
    /// Writes updated tokens back to the credentials file, preserving all other keys.
    /// </summary>
    /// <param name="updated">The credentials with refreshed token values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the write has finished.</returns>
    Task WriteAsync(OAuthCredentials updated, CancellationToken ct);
}
