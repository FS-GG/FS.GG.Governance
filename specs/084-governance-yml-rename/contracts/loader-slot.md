# Contract: Governance Config Loader Primary Slot

**Feature**: 084-governance-yml-rename | **Surface**: `FS.GG.Governance.Config.Loader`

This contract pins the loader's primary-slot behavior. It is the load-bearing contract of
the feature. No public type or member signature changes; the contract is *behavioral* (which
filename is read) plus comment-level `.fsi` text.

## Public surface (unchanged signatures)

```fsharp
// Loader.fsi — signatures UNCHANGED by this feature
module Loader =
    val fileSystemReader : fsggParentDir: string -> FileReader
    val readSource       : root: GovernedPath -> reader: FileReader -> Source
    val loadAndValidate  : fsggParentDir: string -> Validation
```

`FileReader = string -> Result<string option, string>` — the injected per-filename reader.
`Source` (in `Schema.fsi`) carries `Root` + four `FileSlot`s (`Project`, `Policy`,
`Capabilities`, `Tooling`). These names are **retained** (FR-009).

## Behavioral contract

| # | Given (input) | When | Then (result) | Req |
|---|---------------|------|---------------|-----|
| C1 | `.fsgg/` with well-formed `governance.yml` (+ optional files) | `loadAndValidate` | `Valid` with the same typed facts/gates/routing/diagnostics the byte-equivalent `project.yml` produced pre-rename | FR-001, FR-003 |
| C2 | reader: `Ok (Some content)` for `"project.yml"`, `Ok None` for `"governance.yml"` | `readSource` → `Schema.validate` | **Invalid** — missing/absent required primary slot; the `project.yml` content is NOT consumed | FR-002, SC-004 |
| C3 | `.fsgg/` with only optional files (no `governance.yml`, no `project.yml`) | `loadAndValidate` | primary slot absent ⇒ missing-required diagnostic (identical to pre-rename missing-primary) | FR-002 |
| C4 | malformed `governance.yml` (each existing malformed fixture) | `loadAndValidate` | same diagnostic outcome the corresponding `project.yml` malformed fixture produced pre-rename | FR-003 |
| C5 | reader returns `Error` for an optional file (e.g. `policy.yml`) | `readSource` → `Schema.validate` | error surfaced (never swallowed); not reported as absent | R4 (unchanged) |

## Filename constant

```fsharp
// Loader.fs — fileSystemReader
Project = slot "governance.yml"   // was: slot "project.yml"
```

This single string is the only functional change. There is **no fallback** to
`"project.yml"` (D1).

## `.fsi` doc-comment deltas (no signature change)

- `Model.fsi`: section comment `// ── project.yml ──` → `// ── governance.yml ──`.
- `Schema.fsi`: `Root` field doc — "the emitted `ProjectFacts.GovernedRoot` comes from
  `project.yml`" → "...comes from `governance.yml`".

## Verification

- C1/C3/C4 are covered by the existing real-fixture loader/schema tests now pointed at
  `governance.yml` fixtures (renamed on disk).
- **C2 is the new regression test** (research D7): injected reader, asserts Invalid, fails
  before the slot switch and passes after.
- C5 is the existing `erroringReader` test (key updated to `governance.yml`).
- Surface-drift baselines remain green with no re-bless (D3) — signatures unchanged.
