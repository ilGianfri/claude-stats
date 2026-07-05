namespace ClaudeStats.Services;

/// <summary>Information about an available update release.</summary>
/// <param name="LatestVersion">The version number of the latest release.</param>
/// <param name="ReleaseUrl">The GitHub release page URL.</param>
public sealed record UpdateInfo(Version LatestVersion, string ReleaseUrl);

/// <summary>Checks GitHub releases for a newer version of the application.</summary>
public interface IUpdateChecker
{
    /// <summary>
    /// Returns an <see cref="UpdateInfo"/> if a newer release exists on GitHub;
    /// otherwise <see langword="null"/>.
    /// </summary>
    Task<UpdateInfo?> CheckAsync(CancellationToken ct = default);
}
