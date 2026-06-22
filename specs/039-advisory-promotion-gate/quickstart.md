# Quickstart — Advisory-to-Blocking Promotion Gate (F039)

Validation guide for `FS.GG.Governance.AdvisoryPromotion`. See [data-model.md](./data-model.md) for the vocabulary
and [contracts/advisory-promotion-api.md](./contracts/advisory-promotion-api.md) for the signatures + laws.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- Restore is automatic on first build. No new third-party package; the only project reference is
  `FS.GG.Governance.EvidenceReuse` (F030), for the reused `EvidenceRef` token.

## Build

```bash
dotnet build src/FS.GG.Governance.AdvisoryPromotion
```

## FSI-exercise the surface (Principle I, design-first proof)

The design pass lives in a new F039 section of `scripts/prelude.fsx`. After building the library:

```bash
dotnet fsi scripts/prelude.fsx
```

Expected highlights (the worked examples from the contract):

```text
[F39] bare finding, no basis            ⇒ StaysAdvisory NoPermittedBasis
[F39] 2 of 3 confirmations, nothing else ⇒ StaysAdvisory (ConfidenceBelowThreshold (ConfirmationCount 2, ConfidenceThreshold 3))
[F39] backing evidence present          ⇒ EligibleToBlock (DeterministicBackingEvidence, [])
[F39] 3 of 3 confirmations               ⇒ EligibleToBlock (RepeatedReviewConfidence, [])
[F39] human sign-off present            ⇒ EligibleToBlock (HumanSignOff, [])
[F39] all three bases                   ⇒ EligibleToBlock (DeterministicBackingEvidence, [RepeatedReviewConfidence; HumanSignOff])
[F39] lone review never clears (1≥1?)   ⇒ StaysAdvisory (ConfidenceBelowThreshold …)   // the no-single-sample floor
```

## Test

```bash
dotnet test tests/FS.GG.Governance.AdvisoryPromotion.Tests
```

Covers, against the **public** surface with real literal values (no mocks, no clock, no model, no I/O):

- **US1 / SC-001** — advisory by default: no basis ⇒ `StaysAdvisory NoPermittedBasis`; below-threshold ⇒
  `ConfidenceBelowThreshold`; self-confidence never promotes (`AdvisoryDefaultTests`).
- **US2 / SC-002** — eligible on a permitted basis, every satisfied basis named in fixed order
  (`EligibilityTests`).
- **SC-003** — the confidence comparator across counts below / equal to / above the threshold, and the
  no-single-sample floor (`ConfidenceComparatorTests`).
- **US3 / SC-004** — totality across the full cross-product, never throws (`TotalityTests`).
- **US3 / SC-005** — determinism + purity under changed cwd / time / filesystem (`DeterminismTests`).
- **US3 / SC-006** — `EligibleToBlock` carries no blocking action / no calibration claim
  (`NecessaryNotSufficientTests`).
- **FR-001** — `EligibleToBlock` is unrepresentable with an empty basis set (`NonEmptyEligibilityTests`).
- **Principle II / SC-007** — surface baseline + `EvidenceReuse`-only scope guard (`SurfaceDriftTests`).

## Re-bless the surface baseline (only when the public surface intentionally changes)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.AdvisoryPromotion.Tests
```

Writes `surface/FS.GG.Governance.AdvisoryPromotion.surface.txt`. Commit the regenerated baseline alongside the
`.fsi` change (Tier-1 discipline).

## Confirm nothing else moved (SC-007)

```bash
dotnet build && dotnet test
```

Existing `src/`, `surface/`, and merged test projects are unchanged; the new project + test project are purely
additive.
