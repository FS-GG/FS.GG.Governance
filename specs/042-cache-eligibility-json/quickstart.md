# Quickstart — Deterministic cache-eligibility.json Projection (F042)

Validation guide for `FS.GG.Governance.CacheEligibilityJson`. See [data-model.md](./data-model.md) for the
document shape, [contracts/cache-eligibility-json-document.md](./contracts/cache-eligibility-json-document.md)
for the wire contract (field order / tokens / samples), and
[contracts/cache-eligibility-json-api.md](./contracts/cache-eligibility-json-api.md) for the signatures +
laws.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- Restore is automatic on first build. No new third-party package — serialization is the net10.0
  shared-framework `System.Text.Json`. The library's only project reference is
  `FS.GG.Governance.CacheEligibility` (F041, for the `CacheEligibilityReport` it projects + the `entries`
  accessor). `EvidenceReuse` (F030 — `EvidenceRef` / `referenceValue`), `Gates` (F018 — `GateId` /
  `gateIdValue`), `FreshnessKey` (F029 — `InputCategory` / `categoryToken`), and `Config` (F014) arrive
  transitively.

## Build

```bash
dotnet build src/FS.GG.Governance.CacheEligibilityJson
```

## FSI-exercise the surface (Principle I, design-first proof)

The design pass lives in a new F042 section of `scripts/prelude.fsx`. After building the library:

```bash
dotnet fsi scripts/prelude.fsx
```

Expected highlights (the worked examples from the document contract — compact output shown indented for
readability):

```text
[F42] empty report                              ⇒ {"schemaVersion":"fsgg.cache-eligibility/v1","entries":[]}
[F42] exact-match candidate (docs:lint)         ⇒ entry verdict {"kind":"reusable","evidence":"ev-A"}
[F42] no prior evidence (security:scan)         ⇒ entry verdict {"kind":"mustRecompute","cause":{"kind":"noPriorEvidence"}}
[F42] ruleHash + head moved (build:tests)        ⇒ cause {"kind":"inputsChanged","categories":["ruleHash","headRevision"]}
[F42] candidates z:a, a:b, a:a                   ⇒ entries ordered a:a, a:b, z:a; byte-identical for any permutation
[F42] duplicate GateId, both unmatched           ⇒ two entries under the same gate, neither merged nor deduplicated
[F42] inputsChanged [] vs noPriorEvidence         ⇒ {"kind":"inputsChanged","categories":[]} distinct from {"kind":"noPriorEvidence"}
```

## Test

```bash
dotnet test tests/FS.GG.Governance.CacheEligibilityJson.Tests
```

Covers, against the **public** surface with real upstream-assembled reports (built by F041
`CacheEligibility.evaluate` over real `GateId` / `FreshnessInputs` / `ReuseStore` via `EvidenceReuse.record`)
and parsing the output back with `System.Text.Json` (no mocks, no clock, no hand-built JSON oracle, no real
cache lookup):

- **US1 / SC-001** — projection / carry: exactly one entry per report entry, each with its declared gate id
  and its verdict (reusable + evidence reference, or must-recompute + named cause) tracing back to the report
  (`ProjectionTests`).
- **US2 / SC-002 / SC-003** — determinism + versioned schema: byte-for-byte identical output for identical
  reports; identical output for value-equal reports assembled from differently-ordered candidate inputs; a
  present `schemaVersion`; stable field/collection order (`DeterminismTests`).
- **US3 / SC-005** — no-hide: every `mustRecompute` entry carries a named cause; `inputsChanged` categories
  in the report's order; `noPriorEvidence` distinct from `inputsChanged []` (`NoHideTests`).
- **US4 / SC-006** — totality: a document for empty / all-reusable / all-must-recompute / duplicate-`GateId`
  reports, never throws (`TotalityTests`).
- **SC-007** — exclusions: no timestamp / host path / raw freshness input / freshness key or hash / env
  value / numeric exit code / severity / ship verdict / provenance reference appears (`ExclusionsTests`).
- **Principle II** — surface baseline + `CacheEligibility`(+transitive cores)-only scope guard
  (`SurfaceDriftTests`).

## Re-bless the surface baseline (only when the public surface intentionally changes)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.CacheEligibilityJson.Tests
```

Writes `surface/FS.GG.Governance.CacheEligibilityJson.surface.txt`. Commit the regenerated baseline alongside
the `.fsi` change (Tier-1 discipline).

## Confirm nothing else moved

```bash
dotnet build && dotnet test
```

Existing `src/`, `surface/`, and merged test projects are unchanged; the new project + test project are
purely additive.
