using System.Text.Json.Serialization;

namespace ClaudeStats.Json;

/// <summary>Minimal projection of the GitHub Releases API "latest release" response.</summary>
public sealed class GitHubReleaseDto
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;
}
