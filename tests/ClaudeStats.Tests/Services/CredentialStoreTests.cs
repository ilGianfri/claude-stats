using System.IO;
using System.Text.Json;
using ClaudeStats.Models;
using ClaudeStats.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>Unit tests for <see cref="CredentialStore"/> (real temp-file I/O).</summary>
public sealed class CredentialStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cs-test-{Guid.NewGuid():N}.json");

    private CredentialStore CreateStore() => new(NullLogger<CredentialStore>.Instance, _path);

    [Fact]
    public async Task ReadAsync_ReturnsNull_WhenFileMissing()
    {
        CredentialStore store = CreateStore();
        Assert.Null(await store.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_ParsesCredentials()
    {
        await File.WriteAllTextAsync(_path,
            """
            {"claudeAiOauth":{"accessToken":"acc","refreshToken":"ref","expiresAt":1751630400000,
             "subscriptionType":"max"},"organizationUuid":"org-9"}
            """,
            TestContext.Current.CancellationToken);
        CredentialStore store = CreateStore();

        OAuthCredentials? creds = await store.ReadAsync(CancellationToken.None);

        Assert.NotNull(creds);
        Assert.Equal("acc", creds!.AccessToken);
        Assert.Equal("ref", creds.RefreshToken);
        Assert.Equal(1751630400000, creds.ExpiresAt.ToUnixTimeMilliseconds());
        Assert.Equal("max", creds.SubscriptionType);
        Assert.Equal("org-9", creds.OrganizationUuid);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNull_WhenAccessTokenMissing()
    {
        await File.WriteAllTextAsync(_path, """{"organizationUuid":"org"}""", TestContext.Current.CancellationToken);
        CredentialStore store = CreateStore();
        Assert.Null(await store.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_UpdatesTokens_AndPreservesOtherKeys()
    {
        await File.WriteAllTextAsync(_path,
            """
            {"claudeAiOauth":{"accessToken":"old","refreshToken":"old-ref","expiresAt":1000,
             "scopes":["user:inference"],"rateLimitTier":"tier-1"},
             "mcpOAuth":{"srv|abc":{"accessToken":"m"}},"organizationUuid":"org-123"}
            """,
            TestContext.Current.CancellationToken);
        CredentialStore store = CreateStore();

        OAuthCredentials updated = new()
        {
            AccessToken = "new",
            RefreshToken = "new-ref",
            ExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(2000),
        };
        await store.WriteAsync(updated, CancellationToken.None);

        using JsonDocument doc = JsonDocument.Parse(
            await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken));
        JsonElement root = doc.RootElement;
        JsonElement oauth = root.GetProperty("claudeAiOauth");
        Assert.Equal("new", oauth.GetProperty("accessToken").GetString());
        Assert.Equal("new-ref", oauth.GetProperty("refreshToken").GetString());
        Assert.Equal(2000, oauth.GetProperty("expiresAt").GetInt64());
        // Preserved keys:
        Assert.Equal("tier-1", oauth.GetProperty("rateLimitTier").GetString());
        Assert.Equal("user:inference", oauth.GetProperty("scopes")[0].GetString());
        Assert.True(root.TryGetProperty("mcpOAuth", out _));
        Assert.Equal("org-123", root.GetProperty("organizationUuid").GetString());
    }

    /// <summary>Removes the temp credentials file.</summary>
    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
