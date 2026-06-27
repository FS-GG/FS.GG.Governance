# Quickstart / Validation Guide: SDD→Governance Handoff Consumer

**Feature**: `081-sdd-handoff-consumer`

Runnable scenarios that prove the consumer enforces a produced handoff end-to-end. References
[contracts/](./contracts/) and [data-model.md](./data-model.md) instead of duplicating shapes.

## Prerequisites

- .NET `net10.0` SDK; repo builds with `dotnet build FS.GG.Governance.sln`.
- New project `FS.GG.Governance.Adapters.SddHandoff` + tests registered in the `.sln`.
- Committed fixture handoffs under
  `tests/FS.GG.Governance.Adapters.SddHandoff.Tests/fixtures/` (satisfied / failing / v2-major /
  malformed / autoSynthetic / stale / deferred / readiness-blocking / readiness-clean).

## Build & test

```bash
dotnet build FS.GG.Governance.sln
dotnet test  tests/FS.GG.Governance.Adapters.SddHandoff.Tests
dotnet test  tests/FS.GG.Governance.ShipCommand.Tests
dotnet test  tests/FS.GG.Governance.VerifyCommand.Tests
dotnet test  tests/FS.GG.Governance.RouteCommand.Tests
# regenerate the new + re-blessed surface baselines when surface intentionally changes:
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.Adapters.SddHandoff.Tests
```

## Scenario 1 — A produced handoff drives the verdict (US1 / SC-001) ✅ headline

1. Two temp products identical except their `readiness/<id>/governance-handoff.json`: one
   declares all evidence `real`/`skipped`; the other declares a `failed` test node.
2. Run `fsgg ship` (or the `ShipCommand` loop) over each through the **real** pipeline.
3. **Expect**: the failing-evidence product yields `Verdict = Fail` (a blocking handoff evidence
   gate in `Blockers`); the satisfied product yields `Verdict = Pass`. The delta is traceable to
   the declared evidence — the handoff is no longer inert.

## Scenario 2 — Safe read + version check (US2 / SC-004)

Feed `Reader.parse` (a) a well-formed `v1.x` handoff, (b) `contractVersion: "2.0.0"`, (c) a
malformed/garbage file.
- **Expect**: (a) `Ok handoff`; (b) `Error { Cause = VersionMismatch }`; (c)
  `Error { Cause = Malformed }`. Distinct messages; never a throw; no partial mapping enforced.
- Also: a node declaring `state: "autoSynthetic"` ⇒ `AutoSyntheticDeclared` diagnostic and no
  mapped result (FR-005).

## Scenario 3 — ADR-0002 mapping rows (SC-002)

Each `MappingTests.fs` case is named for / commented with its ADR-0002 row:
straight-through states · `deferred → skipped` · `autoSynthetic` rejected · `stale` → state +
`staleEvidence` · `governedReferences` optional · `readiness.*` as a gate · unknown major →
version-mismatch. **Expect**: 100% of rows exercised, each traceable to its row.

## Scenario 4 — Readiness as a first-class gate (US3 / SC-005)

1. A handoff whose `readiness` declares a non-shippable `shipDisposition` and a non-empty
   `blockingDiagnosticIds`; another declaring a clean shippable state.
2. Run `fsgg verify` over each.
- **Expect**: blocking readiness ⇒ a **selected** gate in `Blockers` contributing to `Fail`;
  clean readiness ⇒ a **present, non-blocking** gate (`Passing`/`Warnings`).

## Scenario 5 — Absence is a true no-op (SC-003)

Run `route`/`ship`/`verify` over a product with **no** handoff.
- **Expect**: text + JSON output **byte-identical** to a run on the same product before this
  feature (golden/diff test). The `Handoffs` port returns `[]`; the fold is identity.

## Scenario 6 — Determinism over multiple handoffs (FR-012)

A product with several `readiness/<id>/...` documents.
- **Expect**: deterministic gate set (loaded in `<id>` order, gates sorted by `GateId`);
  re-running yields identical output.

## Scenario 7 — Contract/docs lockstep (FR-014 / FR-015 / research D9)

- **Expect**: ADR 0002's readiness row + queue item #4 and the tutorial's readiness mapping row
  read "first-class gate-registry entry" — updated in the same change as the code. A doc-vs-code
  consistency check (or review) confirms no silent divergence.

## Scope reminder (SC-006)

The consumer references **no** SDD source and edits **no** SDD-owned contract file (verifiable:
no SDD `ProjectReference`/dependency; the `governance-handoff@1` registry entry unchanged).
Production wiring of the seam into `fsgg-sdd init` remains sibling-owned (`FS.GG.SDD`).
