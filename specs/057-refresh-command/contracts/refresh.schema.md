# Contract: `refresh.json` Deterministic Projection

**Status**: Tier 1 contract | **Schema version**: `fsgg.refresh/v1` | **Projection**:
`FS.GG.Governance.RefreshJson.ofRefreshDecision : RefreshDecision -> string`

`refresh.json` is the deterministic, machine-readable projection of one `fsgg refresh` run. It is emitted by
a pure `Utf8JsonWriter` walk (the `AuditJson`/`ReleaseJson`/`VerifyJson` precedent — no reflection-based
serializer). For identical repository state and identical regeneration outcomes it is **byte-for-byte
identical** (FR-007, SC-004); the `--json` stdout is the verbatim bytes of the persisted file (one source of
truth).

## Document shape

```jsonc
{
  "schemaVersion": "fsgg.refresh/v1",
  "outcome": "nothing-to-refresh | views-regenerated | stale-unresolved",  // the success/blocking shade (D5)
  "dryRun": false,
  "summary": {
    "regenerated": 1,        // count brought current (or "would" under dryRun)
    "current": 4,            // count already current
    "unresolved": 0,         // count stale-and-unresolved
    "notEvaluated": 0        // count out-of-scope (FR-015)
  },
  "views": [                 // one per view, in declared manifest order (deterministic)
    {
      "id": "gate-metadata",
      "kind": "gate-metadata",
      "output": "docs/generated/gates.json",
      "status": "current | regenerated | would-regenerate | stale-unresolved | not-evaluated",
      "drifted": ["coveredArtifacts"],   // changed FreshnessKey categories (omitted/empty when current)
      "reason": "<source>: <why>"        // present only for stale-unresolved (FR-016)
    }
  ]
}
```

## Field rules

| Field | Rule | FR |
|---|---|---|
| `schemaVersion` | constant `"fsgg.refresh/v1"`; versioned schema identifier | FR-008 |
| `outcome` | the run's category — drives the exit code (D5) | FR-009 |
| `views[].status` | the per-view `CurrencyStatus` token; `stale-unresolved` is never coerced to `current` | FR-010 |
| `views[].drifted` | the `FreshnessKey.diff` categories (`categoryToken`) that drove staleness | FR-016 |
| `views[].reason` | only for `stale-unresolved`; names the offending source and why | FR-016 |
| order | `views` in declared manifest order; all keys/arrays in fixed order | FR-007 |

**Determinism (FR-007, SC-004)**: no timestamp, no absolute path, no username, no machine-specific content;
stable ordering; byte-identical across runs over identical state. **One source of truth (FR-007)**: the
printed machine output equals the persisted file verbatim. **No fabrication (FR-010)**: a view that could
not be brought current is `stale-unresolved` with a `reason`, never reported `current`.
