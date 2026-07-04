using System.Text.Json.Serialization;

namespace ClaudeStats.Json;

/// <summary>Token usage block within a transcript assistant message.</summary>
public sealed class TranscriptUsageDto
{
    /// <summary>Input tokens.</summary>
    [JsonPropertyName("input_tokens")]
    public long InputTokens { get; set; }

    /// <summary>Output tokens.</summary>
    [JsonPropertyName("output_tokens")]
    public long OutputTokens { get; set; }

    /// <summary>Cache-creation (write) tokens.</summary>
    [JsonPropertyName("cache_creation_input_tokens")]
    public long CacheCreationInputTokens { get; set; }

    /// <summary>Cache-read tokens.</summary>
    [JsonPropertyName("cache_read_input_tokens")]
    public long CacheReadInputTokens { get; set; }
}

/// <summary>The <c>message</c> object within a transcript record.</summary>
public sealed class TranscriptMessageDto
{
    /// <summary>Model id that produced the message.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Token usage for the message.</summary>
    [JsonPropertyName("usage")]
    public TranscriptUsageDto? Usage { get; set; }
}

/// <summary>A single line/record in a Claude transcript JSON-lines file.</summary>
public sealed class TranscriptRecordDto
{
    /// <summary>Record type (only <c>assistant</c> carries usage).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Record timestamp (ISO 8601).</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>The message payload.</summary>
    [JsonPropertyName("message")]
    public TranscriptMessageDto? Message { get; set; }
}
