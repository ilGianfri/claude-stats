using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeStats.Configuration;

namespace ClaudeStats.Json;

/// <summary>
/// System.Text.Json source-generation context for all serialized types in the app.
/// DTOs use explicit <see cref="JsonPropertyNameAttribute"/> where their wire names differ
/// (the usage endpoint uses snake_case, the credentials file uses camelCase).
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(UsageResponseDto))]
[JsonSerializable(typeof(CredentialsFileDto))]
[JsonSerializable(typeof(TokenRefreshRequestDto))]
[JsonSerializable(typeof(TokenRefreshResponseDto))]
[JsonSerializable(typeof(TranscriptRecordDto))]
public partial class AppJsonContext : JsonSerializerContext
{
}
