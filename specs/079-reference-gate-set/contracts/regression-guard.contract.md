# Contract: Reference Gate-Set Regression Guard

**Feature**: `079-reference-gate-set` | **Maps**: FR-010, SC-007

The guard is an automated test in a new project
`tests/FS.GG.Governance.ReferenceGateSet.Tests/` (xUnit, `IsPackable=false`,
referencing `Config`, `Gates`, `Routing`, `Route`, `Enforcement`, `Tests.Common`). It
loads the **on-disk** reference (real evidence — Constitution Principle V) and freezes
the contract in `reference-gate-set.contract.md` so the reference cannot rot back to
empty or flip to blocking.

## Required assertions

| # | Assertion | Guards against | FR / SC |
|---|-----------|----------------|---------|
| G1 | `loadAndValidate` returns `Valid` with empty diagnostics | reference becoming unloadable / gaining unknown fields | FR-007, SC-002 |
| G2 | Registry has exactly 4 gates: `build:build`, `test:test`, `evidence:evidence`, `gameplay:fr-covered` (one `Gate` per declared `Check`; surfaces are NOT projected into the registry — confirmed against `Gates.buildRegistry`, which reads only `Capabilities.Checks`). The `gameplay:fr-covered` floor is the ADR-0049 / WI-8 per-FR gameplay-obligation gate | check set emptied / a kind dropped ("rots to empty"), or an unexpected extra gate | FR-002, FR-003, FR-010, SC-001, SC-007 |
| G3 | Every command-bound gate's prerequisite resolves to a declared `tooling.yml` command; the `gameplay:fr-covered` floor is command-free by design (ADR-0049 / WI-8) and carries no prerequisite | a command reference broken ("dangling") | FR-004, SC-001, SC-007 |
| G4 | Each of build/test/evidence/gameplay selected by its candidate path (gameplay via `specs/**`); 0 orphan checks/commands/unreachable domains | orphan check / dead command / unreachable domain | FR-005, FR-008, SC-004 |
| G5 | `policy.defaultProfile == light` | default profile changed away from `light` ("drift to blocking") | FR-006, SC-007 |
| G6 | Under `Light` @ `Verify`, all selected gates derive `Advisory` (BaseSeverity=Blocking) | populated gates flipping to blocking-by-default | FR-006, SC-003 |
| G7 | Under `Strict` @ `Verify`, ≥1 selected gate derives `Blocking` on the same change | gates becoming unable to block (proves `light` is a deliberate default) | SC-006 |

## Properties

- **Real evidence**: loads the actual `samples/sdd-reference-gate-set/.fsgg/` via the
  config edge and exercises the real `Gates`/`Routing`/`Route`/`Enforcement` cores. No
  synthetic facts, no mocked domain logic.
- **Fails before, passes after**: before the artifact + project exist the guard cannot
  compile/load; after, it is green (Constitution Principle V).
- **Run mode rationale**: `Verify` is the run mode at which `block-on-ship` is
  Light-advisory yet Strict-blocking — see research D5. The guard documents this at the
  use site so the chosen mode is not read as arbitrary.
- **No new public surface**: the guard adds no `.fsi` and no surface-area baseline (Tier
  2); it lives entirely under `tests/`.
