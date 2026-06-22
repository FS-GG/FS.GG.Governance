# Contract: On-Disk Artifacts & Summary

**Feature**: `044-cache-eligibility-command` | **Date**: 2026-06-22

The command writes **two** deterministic, byte-stable documents and prints **one** deterministic summary. This
contract fixes their shape and the honesty/determinism laws.

## A1 — `cache-eligibility.json` (F042, reused verbatim)

- Produced by `CacheEligibilityJson.ofReport cacheReport` **verbatim** — this row adds no field, reorders
  nothing, and does **not** modify the F042 core or its `surface/FS.GG.Governance.CacheEligibilityJson.surface.txt`
  baseline (FR-007/FR-011/SC-008).
- Schema id: `fsgg.cache-eligibility/v1` (`CacheEligibilityJson.schemaVersion`).
- Content: one entry per **resolved** selected gate, in `GateId` order, each `reusable` (opaque
  `EvidenceRef`) or `mustRecompute` (no-hide `RecomputeCause`: `noPriorEvidence` or the changed
  `InputCategory` list). **Unresolved gates never appear here** — they are not representable in F041's report
  (they carry no `FreshnessInputs`).
- Determinism: byte-identical for identical `cacheReport` (the F042 contract, inherited).

## A2 — `cache-eligibility.unresolved.json` (NEW sidecar, D7)

- Schema id: `fsgg.cache-eligibility.unresolved/v1` (NEW — owned by this row, additive; does not touch F042).
- Produced by a small deterministic renderer in `Loop` over `entries report |> filter (outcome = Unresolved)`,
  using the **public** F043 accessors `gateIdValue` and `missingFactToken` — it computes no freshness key, hash,
  or cache decision (FR-013).
- Shape (fixed field order; entries in `GateId` order):

```json
{
  "schemaVersion": "fsgg.cache-eligibility.unresolved/v1",
  "unresolved": [
    { "gate": "<GateId>", "missingFacts": ["ruleHash", "coveredArtifacts", …] }
  ]
}
```

- `missingFacts` uses the F043 `missingFactToken` vocabulary verbatim
  (`ruleHash`/`coveredArtifacts`/`commandVersion`/`generatorVersion`/`baseRevision`/`headRevision`), in
  `MissingFact` enum order, naming **exactly and only** the facts that were missing (no-hide, FR-005).
- **Always written**, even when empty (`"unresolved": []`) — its presence is unconditional so consumers can
  diff it and never mistake "file absent" for "no unresolved gates".
- Determinism: byte-identical for identical resolution; no clock, no cwd, no absolute paths.

## A3 — The stdout summary

- `render model Format`: a deterministic human or JSON summary partitioning the selected gates into
  **reusable** (with evidence reference), **must-recompute** (with `RecomputeCause`), and
  **recompute-by-default / unresolved** (with named missing facts).
- No wall-clock value is surfaced (D9); if one were ever added it MUST be F034-marked and excluded from the
  reproducible artifact content (FR-008). No absolute paths or cwd-dependent text.

## A4 — Cross-cutting laws

- **Honesty**: every selected gate appears in **exactly one** of `cache-eligibility.json` (resolved) or
  `cache-eligibility.unresolved.json` (unresolved) — none silently dropped (Edge); duplicates preserved.
- **No reusable-on-fabrication**: no gate is ever `reusable` on the strength of a sensed-but-fabricated input —
  an unsensed fact yields an unresolved-sidecar entry, never a `cache-eligibility.json` `reusable` (SC-003/L3).
- **Atomic, overwriting**: both files are written via temp-write-then-`File.Move(_, _, true)`; an existing file
  is overwritten atomically (Edge: no merge with stale content); on failure **no partial artifact** remains
  (FR-010).
- **Standalone**: nothing is written into `route.json` or `audit.json`; the F020/F025 cores and baselines are
  untouched (FR-011/SC-008).

## A5 — `evidence-reuse.json` (read-only input store format, NEW this row)

The reuse store F041 `evaluate` consults is loaded read-only from `RunRequest.StorePath`
(`--store`, default `<repo>/readiness/evidence-reuse.json`). **Reading** it is in scope; **writing / evicting /
expiring** it is not (FR-006, deferred to the cache-storage row). This row therefore fixes only the **read**
format the `StoreReader` deserializes into F030's `ReuseStore`.

- Schema id: `fsgg.evidence-reuse-store/v1` (NEW — read contract owned by this row; the writer is the later
  cache-storage row, which MUST emit this same shape).
- **Absent file** ⇒ `StoreReader` returns `Ok None` ⇒ `EvidenceReuse.empty` (FR-006); never an error.
- **Present but malformed** (bad JSON, unknown schema id, missing field) ⇒ `Error` ⇒ `ToolError` (exit 4), no
  artifact written (C2).
- Shape — a deterministic JSON array of recorded evidence, each entry carrying the full F029 `FreshnessInputs`
  field set plus its opaque `EvidenceRef`:

```json
{
  "schemaVersion": "fsgg.evidence-reuse-store/v1",
  "recorded": [
    { "check": "<CheckId>", "domain": "<DomainId>", "command": "<CommandId|null>",
      "environment": "<EnvironmentClass>", "ruleHash": "<RuleHash>",
      "coveredArtifacts": ["<ArtifactHash>", …], "commandVersion": "<CommandVersion|null>",
      "generatorVersion": "<GeneratorVersion>", "base": "<Revision>", "head": "<Revision>",
      "evidence": "<EvidenceRef>" }
  ]
}
```

- The `StoreReader` builds each `RecordedEvidence` via the **public** F029/F030 constructors
  (`FreshnessInputs(...)`, `EvidenceRef`, `ReuseStore`) — it **computes no hash, freshness key, or digest**
  (FR-013); the opaque newtype strings are taken verbatim from the document.
- Determinism: loading the same bytes yields the same `ReuseStore`; entry order is preserved as-read (F041
  `evaluate` is responsible for the matching, this row only deserializes).
