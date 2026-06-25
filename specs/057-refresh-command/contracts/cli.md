# Contract: `fsgg refresh` Command-Line Interface

**Status**: Tier 1 contract (new command) | **Command**: `refresh` | **Executable**:
`FS.GG.Governance.RefreshCommand`

`fsgg refresh` brings a governed repository's Governance-owned generated views back into currency from their
declared sources, and reports what it did. It **mutates the working tree by design** (only the stale
declared views and their recorded provenance); `--dry-run` evaluates and reports without writing. It is
**not** the protected merge-boundary authority (FR-017).

## Invocation

```
fsgg refresh [--dry-run] [--view-kind <kind> | --view <id>] [--text | --json | --text-and-json] [--refresh-out <path>]
```

A leading bare `refresh` token is tolerated (no central dispatcher exists — command precedent).

| Flag | Meaning | Default |
|---|---|---|
| `--dry-run` | evaluate currency and report; write **nothing** (FR-004, FR-013) | off (write mode) |
| `--view-kind <kind>` | scope to one declared view kind (FR-015) | — |
| `--view <id>` | scope to one declared view id (FR-015) | — |
| `--text` / `--json` / `--text-and-json` | output format; `--json` is authoritative machine output | `--text` |
| `--refresh-out <path>` | also persist `refresh.json` to `<path>` (FR-006) | not written |

**Scope rules (FR-015)**: no selector ⇒ all declared views (default scope). `--view-kind` and `--view`
together, or an unparseable selector, ⇒ `UsageError` (exit 2). Out-of-scope views are reported
**not-evaluated**, never assumed current.

## Behavior (MVU workflow)

1. **LoadManifest** — read `.fsgg/refresh.yml`; absent/malformed/unreadable ⇒ `InputUnavailable` (exit 3)
   with a diagnostic naming the offending file. An **empty** manifest (no entries) ⇒ `NothingToRefresh`
   (exit 0) — an empty manifest is not an error (FR-012).
2. **SenseSources** — for each in-scope entry, digest its declared source(s) and sense the generator
   version (research D2). A source that is absent/unreadable ⇒ that view is `StaleUnresolved` (FR-010).
3. **ReadRecorded** — read each view's recorded provenance (research D4); absent ⇒ the view has no prior
   provenance and is treated as stale (first generation).
4. **DecideCurrency** — per view, `stale = not (FreshnessKey.matches recorded current)` with revisions held
   equal (research D1); `FreshnessKey.diff` names the drifted categories.
5. **Regenerate** *(write mode only)* — run each stale view's declared generator through the execution port
   (research D3); on success record refreshed provenance; a non-zero generator exit or a write failure ⇒
   `ToolError` (exit 4) with **no partial view** left behind. In `--dry-run`, no generator runs and nothing
   is written.
6. **BuildDecision + Render** — assemble the `RefreshDecision`, render human text (and/or `refresh.json`),
   write the artifact if requested, and map the outcome to the exit code.

Refresh always brings current the views that *can* be refreshed even when others are unresolved (US1).

## Exit-code contract (research D5)

| Code | `categoryToken` | Outcome |
|---|---|---|
| `0` | `ok` | **nothing to refresh** — every in-scope view current (or empty manifest) (SC-002) |
| `1` | `stale-unresolved` | ≥1 view stale and could **not** be brought current (FR-010) — CI fails on it |
| `2` | `usage` | invalid arguments (unknown flag, missing value, mutually-exclusive selectors) |
| `3` | `input-unavailable` | absent/malformed manifest or declared source — currency cannot be evaluated |
| `4` | `tool-error` | a generator/IO defect during regeneration or write |
| `5` | `regenerated` | success; ≥1 stale view was brought current (write) **or** would be (`--dry-run`) |

Diagnostics print to stderr tagged `fsgg refresh [<categoryToken>]: <message>`, naming the offending
source/view (FR-016). `0` and `5` are both success; `5` is the deliberate "something changed" signal a CI
`--dry-run` check blocks on to require committing a refresh; `1` is the genuinely failing outcome.

## Guarantees

- **Fail-safe (FR-010)**: a missing/unreadable/unexpected input or an unevaluable view resolves to
  `StaleUnresolved` or a distinct non-success exit — never a fabricated "all current," never a silently
  skipped stale view, never a crash.
- **No-mutation in `--dry-run` (FR-013, SC-003)**: the working tree is byte-for-byte identical afterward.
- **Atomic, no-partial (FR-013)**: views, recorded provenance, and `refresh.json` are written
  temp-then-rename; a tool error leaves no partial view and no partial artifact.
- **Product-neutral (FR-011, SC-006)**: no product/view/path/generator/renderer identity in code — all
  from `.fsgg/refresh.yml`.
- **Network-free own logic (FR-014, SC-007)**: verified by a scope guard over the command's assemblies.
- **Not the merge authority (FR-017)**: a clean `fsgg refresh` does not authorize a merge.
