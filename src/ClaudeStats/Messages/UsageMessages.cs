using ClaudeStats.Models;

namespace ClaudeStats.Messages;

/// <summary>Broadcast after a successful usage read.</summary>
/// <param name="Snapshot">The fresh usage snapshot.</param>
public sealed record UsageUpdatedMessage(UsageSnapshot Snapshot);

/// <summary>Broadcast when a usage read fails.</summary>
/// <param name="State">The resulting state (<see cref="LimitState.Unavailable"/> or <see cref="LimitState.Stale"/>).</param>
/// <param name="Reason">A short, user-facing explanation.</param>
public sealed record UsageErrorMessage(LimitState State, string Reason);
