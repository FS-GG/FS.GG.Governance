# Quickstart & Validation: Enforcement Levers and Effective Severity (F023)

A runnable validation guide for the first Phase-5 pure core. It proves the typed enforcement vocabulary and
the total `deriveEffectiveSeverity` derivation work end-to-end through the **public** surface. No I/O, no
fixtures on disk — every input is a literal typed lever value.

## Prerequisites

- .NET SDK for `net10.0` (per `Directory.Build.props`).
- The new projects added to the solution:
  - `src/FS.GG.Governance.Enforcement/FS.GG.Governance.Enforcement.fsproj` (references
    `FS.GG.Governance.Config`).
  - `tests/FS.GG.Governance.Enforcement.Tests/FS.GG.Governance.Enforcement.Tests.fsproj` (references
    `Enforcement` + `Config`).

## Build & test

```bash
# Build just this leaf and its test project
dotnet build src/FS.GG.Governance.Enforcement/FS.GG.Governance.Enforcement.fsproj
dotnet test  tests/FS.GG.Governance.Enforcement.Tests/FS.GG.Governance.Enforcement.Tests.fsproj

# Full-suite regression (no other project should change)
dotnet test FS.GG.Governance.sln
```

## FSI smoke (the design-first surface — Principle I)

Exercise the packed surface the way `scripts/prelude.fsx` does, before trusting any `.fs` body:

```fsharp
open FS.GG.Governance.Config.Model        // Maturity
open FS.GG.Governance.Enforcement

// The design's worked example — base blocking, block-on-ship, inner, light  ⇒  advisory
let example =
    Enforcement.deriveEffectiveSeverity
        { BaseSeverity = Blocking; Maturity = BlockOnShip; Mode = Inner; Profile = Light }
// example.EffectiveSeverity = Advisory
// example.BaseSeverity      = Blocking          // carried unchanged
// example.Reason            = "'light' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'inner')"

// Same finding at the gate ⇒ blocking
let atGate =
    Enforcement.deriveEffectiveSeverity
        { example with Mode = Gate }
// atGate.EffectiveSeverity = Blocking

// observe/warn withhold blocking under any mode/profile
let observed =
    Enforcement.deriveEffectiveSeverity
        { BaseSeverity = Blocking; Maturity = Observe; Mode = Release; Profile = Release }
// observed.EffectiveSeverity = Advisory

// Total recognition — canonical maps, unknown is carried, never throws
Enforcement.recognizeMode "gate"      // Recognized Gate
Enforcement.recognizeMode "ship"      // Unrecognized "ship"
Enforcement.recognizeProfile "strict" // Recognized Strict
```

## Acceptance → evidence map

| Spec item | What proves it | Test file |
|---|---|---|
| US1 / SC-002 — worked example reproduces (blocking→advisory, reason names levers) | `deriveEffectiveSeverity` on the example asserts `Advisory` + exact reason; same finding at `gate`/`release` asserts `Blocking` | `DerivationTests` |
| US1 acceptance 3 / FR-007 — observe/warn always advisory | sweep observe/warn × every mode × profile ⇒ all `Advisory` | `DerivationTests` |
| US1 acceptance 4 / FR-010 — all six fields, non-empty reason | every decision carries base/mode/profile/maturity/effective + non-empty reason | `TotalityTests` |
| FR-008 — base-blocking blocks iff mode ≥ effective floor | per-maturity/profile floor boundary asserted against the truth table | `DerivationTests` |
| US2 / SC-005 — recognition: canonical maps, invalid ⇒ Unrecognized, exactly six modes/four profiles | recognize all canonical tokens + representative invalid strings; assert recognized set size | `RecognitionTests` |
| US3 / SC-003 — base severity byte-identical out=in | FsCheck: `decision.BaseSeverity = input.BaseSeverity` over the full sweep | `CarryTests` |
| US3 / SC-006 — no finding dropped (count out = count in) | mapping the derivation over an N-finding list yields N decisions | `CarryTests` |
| SC-001 — totality over the full cross-product, never throws | FsCheck/enumeration over all base × maturity × mode × profile | `TotalityTests` |
| SC-004 — determinism: twice-run byte-identical | evaluate each input twice; assert equal effective severity + equal reason string | `DeterminismTests` |
| FR-013/FR-014 exclusions | the surface exposes no rollup/verdict/exit-code/IO/CLI member (surface baseline) | `SurfaceDriftTests` |

## What "done" looks like

- `dotnet test` green for the new test project and the full solution (no regression across the existing
  projects).
- `surface/FS.GG.Governance.Enforcement.surface.txt` matches the drift test; the public surface is exactly
  the members in [`contracts/Enforcement.fsi`](./contracts/Enforcement.fsi).
- The worked example and the recognition sets pass against **real typed lever inputs** — no synthetic
  evidence; if any `Synthetic`-tokened test exists it is disclosed at the use site and in the PR (Principle
  V).
