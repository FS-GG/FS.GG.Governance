# Quickstart — Per-Gate Cache-Eligibility Verdict Core (F041)

Validation guide for `FS.GG.Governance.CacheEligibility`. See [data-model.md](./data-model.md) for the vocabulary
and [contracts/cache-eligibility-api.md](./contracts/cache-eligibility-api.md) for the signatures + laws.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- Restore is automatic on first build. No new third-party package; the only project references are
  `FS.GG.Governance.EvidenceReuse` (F030, for `decide` / `ReuseStore` / `ReuseDecision` / `RecomputeCause` /
  `EvidenceRef`) and `FS.GG.Governance.Gates` (F018, for the reused `GateId` / `gateIdValue`). `FreshnessInputs` /
  `InputCategory` (F029) and `CheckId` / `DomainId` (F014, `Config`) arrive transitively.

## Build

```bash
dotnet build src/FS.GG.Governance.CacheEligibility
```

## FSI-exercise the surface (Principle I, design-first proof)

The design pass lives in a new F041 section of `scripts/prelude.fsx`. After building the library:

```bash
dotnet fsi scripts/prelude.fsx
```

Expected highlights (the worked examples from the contract):

```text
[F41] empty store, one candidate             ⇒ MustRecompute NoPriorEvidence
[F41] recorded, exact-match candidate         ⇒ Reusable (EvidenceRef "ev-A")
[F41] recorded, RuleHash differs              ⇒ MustRecompute (InputsChanged [RuleHashCat])
[F41] recorded, RuleHash + Head differ        ⇒ MustRecompute (InputsChanged [RuleHashCat; HeadRevisionCat])
[F41] 3 candidates supplied z:a, a:b, a:a     ⇒ report ordered a:a, a:b, z:a (ordinal); same for any permutation
[F41] duplicate GateId, different inputs       ⇒ two entries under the same gate, deterministically ordered
[F41] no candidates                           ⇒ empty report (total, not an error)
```

## Test

```bash
dotnet test tests/FS.GG.Governance.CacheEligibility.Tests
```

Covers, against the **public** surface with real literal values (no mocks, no clock, no I/O, no JSON, no real cache
lookup):

- **US1 / SC-001 / SC-003** — recompute by default: empty store ⇒ `MustRecompute NoPriorEvidence`; changed inputs ⇒
  `MustRecompute (InputsChanged …)` naming exactly the changed categories; no candidate yields `Reusable` without a
  defensible match (`RecomputeByDefaultTests`).
- **US2 / SC-002** — reusable when prior evidence matches, carrying the F030 evidence reference, with the same
  most-recent-wins choice F030 makes (`ReusableTests`).
- **US3 / SC-006** — one attributed verdict per candidate, ordered by `GateId` ordinal, every gate preserved,
  duplicates kept, independent of supply order (`AttributionAndOrderTests`).
- **US3 / SC-004** — totality across the full cross-product of candidate counts and store states, never throws
  (`TotalityTests`).
- **US3 / SC-005** — determinism + purity under changed cwd / time / filesystem; no key computed
  (`DeterminismTests`).
- **FR-010 / SC-007** — `CacheEligibilityVerdict` carries no skip action / severity / ship verdict / exit-code basis
  (`NecessaryNotSufficientTests`).
- **Principle II / SC-008** — surface baseline + `EvidenceReuse`/`Gates`(+transitive cores)-only scope guard
  (`SurfaceDriftTests`).

## Re-bless the surface baseline (only when the public surface intentionally changes)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.CacheEligibility.Tests
```

Writes `surface/FS.GG.Governance.CacheEligibility.surface.txt`. Commit the regenerated baseline alongside the `.fsi`
change (Tier-1 discipline).

## Confirm nothing else moved (SC-008)

```bash
dotnet build && dotnet test
```

Existing `src/`, `surface/`, and merged test projects are unchanged; the new project + test project are purely
additive.
