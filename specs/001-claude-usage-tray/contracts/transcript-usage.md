# Contract (consumed): Transcript Usage (local JSON-lines)

**Path glob**: `%USERPROFILE%\.claude\projects\**\*.jsonl` (145 files on the target machine at
planning time; grows over time). One JSON object per line. Behind `IUsageStatsReader`. Read with
`FileShare.ReadWrite`, streamed line-by-line, off the UI thread.

## Relevant record shape

Only `type == "assistant"` records carry token usage. Fields used:

```jsonc
{
  "type": "assistant",
  "timestamp": "2026-07-04T18:31:00.000Z",   // ISO 8601 → bucket by LOCAL calendar day
  "message": {
    "role": "assistant",
    "model": "claude-opus-4-8",              // → IPricingProvider key
    "usage": {
      "input_tokens": 15006,
      "output_tokens": 822,
      "cache_creation_input_tokens": 9050,
      "cache_read_input_tokens": 24714
      // (nested "cache_creation", "iterations", "server_tool_use" exist but are not required)
    }
  }
}
```

Other line `type`s observed and ignored for stats: `user`, `attachment`, `system`,
`file-history-snapshot`, `last-prompt`, `ai-title`, `queue-operation`.

## Aggregation → `DailyUsage`
For each assistant record within the last ~30 local days:
- bucket by `timestamp` local date;
- sum `input_tokens`, `output_tokens`, `cache_creation_input_tokens`, `cache_read_input_tokens`;
- `MessageCount += 1`;
- `EstimatedCost += price(model) · tokensByType` (see [internal-services.md](./internal-services.md) `IPricingProvider`).

## Edge handling
| Condition | Behavior |
|-----------|----------|
| No files / directory missing | `UsageStatistics.IsAvailable = false` (FR-007/FR-016) |
| Line not JSON / not assistant / no `usage` | skip line |
| Unknown `model` | cost via default/`n/a`; tokens still counted |
| Very large corpus | stream per line; optional incremental cache by file mtime (deferred) |
| File locked/rewritten mid-read | `FileShare.ReadWrite`; skip unreadable file this pass |
