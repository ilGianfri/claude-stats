using System.Text.Json.Serialization;

namespace ClaudeStats.Json;

/// <summary>
/// Wire model for a single usage window. <c>utilization</c> is a percentage (0–100) and
/// <c>resets_at</c> is an ISO 8601 timestamp.
/// </summary>
public sealed class UsageWindowDto
{
    /// <summary>Utilization as a percentage (0–100).</summary>
    [JsonPropertyName("utilization")]
    public double? Utilization { get; set; }

    /// <summary>Reset time (ISO 8601).</summary>
    [JsonPropertyName("resets_at")]
    public DateTimeOffset? ResetsAt { get; set; }
}

/// <summary>Model scope for a scoped limit (e.g. a per-model weekly sub-limit).</summary>
public sealed class UsageScopeModelDto
{
    /// <summary>Human-readable model name (e.g. "Fable").</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}

/// <summary>Scope of a limit entry.</summary>
public sealed class UsageScopeDto
{
    /// <summary>Model the limit is scoped to, if any.</summary>
    [JsonPropertyName("model")]
    public UsageScopeModelDto? Model { get; set; }
}

/// <summary>Wire model for an entry in the <c>limits[]</c> array.</summary>
public sealed class UsageLimitDto
{
    /// <summary>Limit kind (e.g. "session", "weekly_all", "weekly_opus", "weekly_scoped").</summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    /// <summary>Percentage 0–100.</summary>
    [JsonPropertyName("percent")]
    public double? Percent { get; set; }

    /// <summary>Utilization as a percentage (0–100), when present instead of <c>percent</c>.</summary>
    [JsonPropertyName("utilization")]
    public double? Utilization { get; set; }

    /// <summary>Reset time (ISO 8601).</summary>
    [JsonPropertyName("resets_at")]
    public DateTimeOffset? ResetsAt { get; set; }

    /// <summary>Scope of the limit (present for scoped, per-model limits).</summary>
    [JsonPropertyName("scope")]
    public UsageScopeDto? Scope { get; set; }
}

/// <summary>
/// Wire model for the <c>GET /api/oauth/usage</c> response. Supports both the flat-map shape
/// (known window keys) and the alternate <c>limits[]</c> array; unknown keys are ignored.
/// </summary>
public sealed class UsageResponseDto
{
    /// <summary>Rolling ~5-hour window.</summary>
    [JsonPropertyName("five_hour")]
    public UsageWindowDto? FiveHour { get; set; }

    /// <summary>7-day / weekly window.</summary>
    [JsonPropertyName("seven_day")]
    public UsageWindowDto? SevenDay { get; set; }

    /// <summary>Weekly Opus sub-limit.</summary>
    [JsonPropertyName("seven_day_opus")]
    public UsageWindowDto? SevenDayOpus { get; set; }

    /// <summary>Alternate array form observed in some versions.</summary>
    [JsonPropertyName("limits")]
    public List<UsageLimitDto>? Limits { get; set; }
}
