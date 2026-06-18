# CLI Command Contract (F12)

Tool command:

```text
fsgg-governance <route|explain|contract|evidence> [options]
```

Development command:

```text
dotnet run --project src/FS.GG.Governance.Cli -- <route|explain|contract|evidence> [options]
```

## Options

| Option | Values | Default |
|---|---|---|
| `--root <path>` | readable repository root | `.` |
| `--mode <mode>` | `sandbox`, `inner`, `gate` | `inner` |
| `--format <format>` | `text`, `json` | `text` |
| `--json` | alias for `--format json` | off |
| `--scope <paths>` | comma-separated paths or repeated option | empty = full snapshot |
| `--review-budget <n>` | non-negative integer fresh-review budget | `0` (cache-only) |
| `--review-store <path>` | review cache path | user cache directory outside root |
| `--out <path>` | report output file | stdout |
| `--judge-model <id>` | judge model id for cache key | CLI default |
| `--judge-version <version>` | judge model version for cache key | CLI default |

## Exit Codes

| Code | Category | Meaning |
|---:|---|---|
| `0` | `success` | Command ran; no governed blocking failure. Advisory findings may exist. |
| `2` | `governedBlocking` | Governance result contains a failing blocking gate in `Gate` mode. |
| `64` | `usageError` | Malformed command, unknown option, invalid mode/format/budget. |
| `66` | `inputUnavailable` | Root or required snapshot input unavailable before a governed report can be produced. |
| `70` | `toolError` | Unexpected tool defect or output-write failure. |

## JSON Envelope

All JSON commands return one object with stable field order:

```json
{
  "schemaVersion": 1,
  "command": "route",
  "mode": "inner",
  "format": "json",
  "root": "/absolute/root",
  "scope": [],
  "exit": { "category": "success", "code": 0 },
  "budget": {
    "requested": [],
    "cacheHits": [],
    "cacheMisses": [],
    "freshDispatches": [],
    "pending": [],
    "budgetExhausted": []
  },
  "failures": [],
  "payload": {}
}
```

Payload by command:

- `route`: route facts from the F07 `Route` value: stakes, reason, blocking entries, advisory entries.
- `explain`: ordered explanation proof trees; each tree uses `Json.ofExplanation`.
- `contract`: ordered contract entries; uses `Json.ofContract`.
- `evidence`: evidence nodes, dependencies, freshness, disclosures, Host failures, and review-budget state.

No field contains an implicit wall-clock timestamp. If a caller wants time-sensitive freshness
evidence, the instant must be supplied as sensed input and appears as data.
