using ClaudeStats.Services;

namespace ClaudeStats.Tests.TestSupport;

/// <summary>Deterministic <see cref="IClock"/> for tests.</summary>
public sealed class FakeClock : IClock
{
    /// <summary>Gets or sets the fixed current instant.</summary>
    public DateTimeOffset Now { get; set; } = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);
}
