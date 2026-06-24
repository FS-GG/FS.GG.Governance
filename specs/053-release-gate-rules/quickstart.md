# Quickstart: Pure Release-Gate Readiness Rules Core (F053)

A validation/run guide for the pure release-gate core: given declared release rules and the typed facts they
govern, `evaluate` produces one finding per rule and `rollup` rolls them up into a release verdict — reusing
the F023 enforcement and F024 ship machinery verbatim, with no I/O, no process, and no document. For the
public contracts see [contracts/](./contracts/); for semantics see [data-model.md](./data-model.md) and
[research.md](./research.md).

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- The merged thread on graph: F014 `Config` (`Maturity`/`SurfaceId`/`ReleaseSurface`/`Release`), F023
  `Enforcement` (`Severity`/`RunMode.Release`/`Profile.Release`/`deriveEffectiveSeverity`), F024 `Ship`
  (`Verdict`/`ExitCodeBasis`). **No new third-party package.**

## Build

```bash
# The new pure library and its dependency graph (Config, Enforcement, Ship)
dotnet build src/FS.GG.Governance.ReleaseRules/FS.GG.Governance.ReleaseRules.fsproj
```

`ReleaseRules` compile order is `Model.fsi → Model.fs → Release.fsi → Release.fs`, with `ProjectReference`s to
`FS.GG.Governance.Config`, `FS.GG.Governance.Enforcement`, and `FS.GG.Governance.Ship` only.

## Exercise the pure core in FSI (the honest audience — Principle I)

The `scripts/prelude.fsx` F053 section drives the surface with no I/O at all:

```fsharp
// ── F053: ReleaseRules (pure release-gate core) ──
#r "src/FS.GG.Governance.ReleaseRules/bin/Debug/net10.0/FS.GG.Governance.ReleaseRules.dll"
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model

// One declared rule per kind, each blocking-at-release.
let blocking kind surface =
    { Kind = kind; Surface = SurfaceId surface; BaseSeverity = Blocking; Maturity = BlockOnRelease }
let rules =
    [ blocking VersionBump "pkg"; blocking PackageMetadata "pkg"; blocking TemplatePins "pkg"
      blocking PublishPlan "pkg"; blocking TrustedPublishing "pkg"; blocking Provenance "pkg" ]

// (US1) All facts met ⇒ one Satisfied finding per rule, count = rule count.
let allMet = { States = rules |> List.map (fun r -> r.Kind, Met) |> Map.ofList }
let f1 = Release.evaluate rules allMet
printfn "[F53] one finding per rule? %b" (f1.Length = rules.Length)                       // expect: true
printfn "[F53] all satisfied? %b" (f1 |> List.forall (fun f -> f.Outcome = Satisfied))     // expect: true

// (US1, FR-005) An ABSENT fact ⇒ Violated (fail-safe), never silently satisfied.
let missingProvenance = { States = allMet.States |> Map.remove Provenance }
let f2 = Release.evaluate rules missingProvenance
printfn "[F53] absent fact ⇒ violated? %b"
    (f2 |> List.exists (fun f -> f.Kind = Provenance && f.Outcome = Violated))             // expect: true

// (US2) A blocking violation ⇒ Fail / Blocked, the violation in Blockers.
let d2 = Release.rollup f2
printfn "[F53] blocking violation ⇒ Fail? %b" (d2.Verdict = Fail)                          // expect: true
printfn "[F53] exit basis Blocked? %b" (d2.ExitCodeBasis = Blocked)                        // expect: true

// (US2.3 / FR-010) Relax that rule to advisory via its maturity ⇒ Pass, but VISIBLE as a Warning.
let relaxed =
    rules |> List.map (fun r -> if r.Kind = Provenance then { r with Maturity = Warn } else r)
let d3 = Release.rollup (Release.evaluate relaxed missingProvenance)
printfn "[F53] relaxed ⇒ Pass? %b" (d3.Verdict = Pass)                                     // expect: true
printfn "[F53] relaxed violation visible as Warning? %b"
    (d3.Warnings |> List.exists (fun w -> w.Finding.Kind = Provenance))                    // expect: true

// (US2.2) All satisfied ⇒ Pass / Clean, no blockers.
let dClean = Release.evaluateRelease rules allMet
printfn "[F53] all-met ⇒ Pass/Clean/no-blockers? %b"
    (dClean.Verdict = Pass && dClean.ExitCodeBasis = Clean && dClean.Blockers.IsEmpty)     // expect: true

// (US3) Determinism — two evaluations are byte-identical.
printfn "[F53] deterministic? %b" (Release.evaluate rules missingProvenance = f2)          // expect: true

// (US3 / FR-006) No-hide — output rule-kind multiset = declared rule-kind multiset.
printfn "[F53] no drops/fabrications? %b"
    ((f2 |> List.map (fun f -> f.Kind) |> List.sort) = (rules |> List.map (fun r -> r.Kind) |> List.sort))
                                                                                          // expect: true

// (edge) Empty rule set ⇒ no findings, Pass, Clean.
let dEmpty = Release.evaluateRelease [] { States = Map.empty }
printfn "[F53] empty ⇒ Pass/Clean? %b" (dEmpty.Verdict = Pass && dEmpty.ExitCodeBasis = Clean) // expect: true
```

Each `printfn` prints `true`. If `evaluate` ever dropped or fabricated a finding, miscounted, treated an
absent fact as satisfied, or produced a non-deterministic order, the matching line would print `false`.

## Run the semantic tests

```bash
dotnet test tests/FS.GG.Governance.ReleaseRules.Tests/FS.GG.Governance.ReleaseRules.Tests.fsproj
```

The suite drives the **packed public surface** (no private helpers, no mocks) over real in-memory declared
input — no network, no governed repository, no process (SC-004):

| Test group | Asserts | Maps to |
|------------|---------|---------|
| `EvaluateTests` | one finding per rule; correct `Satisfied`/`Violated` per `FactState` across all six kinds; reasons name the kind + surface | US1, FR-001/FR-002/FR-003 |
| `FailSafeTests` | an `Unrecoverable`/absent fact ⇒ `Violated`, never `Satisfied`, with a distinct "no recoverable evidence" reason | US1, FR-005, SC-005 |
| `RollupTests` | blocking violation ⇒ `Fail`/`Blocked`/non-empty `Blockers`; all-satisfied ⇒ `Pass`/`Clean`/empty `Blockers`; relaxed violation ⇒ `Pass` + visible `Warning` | US2, FR-004/FR-010, SC-002/SC-006 |
| `EdgeCaseTests` | empty rule set ⇒ no findings + `Pass`/`Clean`; duplicate same-kind rules each yield a finding; facts for an undeclared kind invent nothing | Edge cases |
| `DeterminismTests` | repeated `evaluate`/`rollup` byte-identical; output rule-kind multiset = declared multiset | US3, FR-006/FR-007, SC-001/SC-003 |
| `PropertyTests` (FsCheck) | `\|findings\| = \|rules\|`; `\|Blockers\|+\|Warnings\|+\|Passing\| = \|findings\|` over random rule/fact sets | SC-001, FR-006 |
| `SurfaceDriftTests` | the reflective public surface matches `surface/FS.GG.Governance.ReleaseRules.surface.txt` | Principle II |

## Bless the surface baseline

After the first green build of the library, generate the reflective baseline (the drift test reads it):

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.ReleaseRules.Tests/FS.GG.Governance.ReleaseRules.Tests.fsproj
```

Commit the generated `surface/FS.GG.Governance.ReleaseRules.surface.txt`. Subsequent runs (without the env var)
fail if the public surface drifts.

## What this row does NOT do (following rows)

- **Sense** the real version/package-metadata/template-pins/publish-plan/trusted-publishing/provenance state
  from a governed repository — the **next** row (it will produce the `ReleaseFacts` this core consumes).
- The **`fsgg release` host command** wiring sensing → `evaluateRelease` → exit code — a following row
  (it will honor Principle IV on the established command MVU boundary, like `RouteCommand`/`ShipCommand`).
- The additive **`release.json` projection** of the findings + verdict — a following projection row.
