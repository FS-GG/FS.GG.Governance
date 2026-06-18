# Phase 1 — Quickstart (F10 · 010-adapter-speckit)

A run/validation guide for the Spec Kit adapter. It exercises the **public** surface
([`contracts/SpecKit.fsi`](./contracts/SpecKit.fsi) + [`contracts/Catalog.fsi`](./contracts/Catalog.fsi))
exactly as the F08 effects shell / F12 CLI would — through the built
`FS.GG.Governance.Adapters.SpecKit` library and `scripts/prelude.fsx` (Principle I, SC-001/SC-008). It
does not duplicate implementation; see [data-model.md](./data-model.md) and the `.fsi` for the contract.

## Prerequisites
- .NET `net10.0` SDK; repo restored.
- F01–F07 (the pure kernel) and **F09** (`FS.GG.Governance.Adapters.Spi`, the SPI + `Lift`/`Composition`)
  already merged. This feature adds the **new** pure `FS.GG.Governance.Adapters.SpecKit` project that
  depends on the SPI.

## Build & run

```bash
dotnet build src/FS.GG.Governance.Adapters.SpecKit
dotnet fsi scripts/prelude.fsx        # includes the F10 sketch below
dotnet test                            # runs SpecKitTests + CatalogTests + LiftTests + the SpecKit surface drift test
# Bless the NEW SpecKit surface baseline after the F10 surface lands:
BLESS_SURFACE=1 dotnet test
```

## FSI sketch (added to `scripts/prelude.fsx`)
Drafted against the contracts before any `.fs` body exists; it uses `failwith`-stubs / `Unchecked` where a
body is not yet present — the point of the pass is that the SHAPES typecheck against the two `.fsi` and
that authoring the adapter, governing synthetic facts, and routing inner-loop vs merge read naturally.

```fsharp
// ── Spec Kit adapter sketch (F10) — governance dogfoods this repo's own workflow as data ──
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.SpecKit

let judge : JudgeId = { ModelId = "speckit-judge"; Version = "1" }
let adapter = Catalog.adapter judge Catalog.defaultDial   // the ONE Adapter value (FR-003)

// 1. OBSERVER-ONLY + FIVE-COMPONENT (SC-001): the adapter is fully specified by the five SPI
//    components + the Bridge; it exposes NO artifact-authoring operation. (Verified by inspection
//    of the .fsi: no System.IO, no Model/Msg/Effect, no write-spec/write-plan function.)
printfn "rules = %d / probes = %d / fences = %d"
    adapter.Rules.Length adapter.Probes.Length adapter.Fences.Length

// 2. PHASE GUARD (SC-002): a whenPhase Plan rule is a definite not-applicable before Plan.
let beforePlan = [ { Id = FactId "ph"; Value = PhaseReached Phase.Specify; Provenance = [] } ]
let atPlan     = [ { Id = FactId "ph"; Value = PhaseReached Phase.Plan;    Provenance = [] } ]
let planRule   = Catalog.planSatisfiesSpec
Check.eval beforePlan planRule.Check   // ⇒ Pass (vacuously satisfied — NOT Fail/Uncertain) (P1)
Check.eval atPlan     planRule.Check   // ⇒ Uncertain "judgement" (the Opaque check now contributes) (P2)

// 3. INNER-LOOP vs MERGE (SC-003): nothing blocks before merge; merge is the single fence.
let innerChange = { Phase = Phase.Tasks; Surfaces = Set.ofList [ SpecKitArtifact.Tasks ] }
let mergeChange = { Phase = Phase.Merge; Surfaces = Set.ofList [ SpecKitArtifact.Tasks ] }
let innerRoute  = Route.route adapter.Fences adapter.Rules Inner innerChange   // Blocking = []  (advisory)
let mergeRoute  = Route.route adapter.Fences adapter.Rules Gate  mergeChange   // Blocking = the dial's set
printfn "inner blocking = %d / merge blocking = %d" innerRoute.Blocking.Length mergeRoute.Blocking.Length

// 4. EVIDENCE / TAINT via the KERNEL (SC-004): AutoSynthetic flows down TaskDependsOn by F05's
//    fixed point; evidenceNotSynthetic is a blocking failure at merge that NO flag flips.
let tainted =
    [ { Id = FactId "t1"; Value = TaskState ("T1", Synthetic); Provenance = [] }
      { Id = FactId "t2"; Value = TaskState ("T2", Real);      Provenance = [] }
      { Id = FactId "d";  Value = TaskDependsOn ("T2", "T1");  Provenance = [] } ]
Check.eval tainted Catalog.evidenceNotSynthetic.Check   // ⇒ Fail (T2 is AutoSynthetic via T1) (E1/E3)

// 5. THE CONSTITUTION DIAL (SC-005): the blocking set is the dial's, not a fixed list.
let lightDial = { Catalog.defaultDial with BlockingAtMerge = Set.empty }   // only evidence blocks
let lightAdapter = Catalog.adapter judge lightDial
let lightMerge = Route.route lightAdapter.Fences lightAdapter.Rules Gate mergeChange
printfn "light merge blocking = %d (fewer than default)" lightMerge.Blocking.Length

// 6. RENDER & EXPLAIN (SC-006): every rule renders to a sentence and explains itself.
for r in Catalog.catalog do
    printfn "%s :: %s" (let (RuleId id) = r.Id in id) (Check.render r.Check)

// 7. FAITHFUL LIFT (SC-007): the adapter lifts unchanged into a coproduct (proven in LiftTests
//    by composing with a second synthetic toy domain; standalone == lifted (verdict, provenance)).
```

## Validation scenarios (mapped to Success Criteria)

| # | Scenario | Asserts | SC |
|---|---|---|---|
| V1 | `Catalog.adapter judge dial` supplies the five components; `.fsi` shows no authoring op, no I/O | five-component / observer-only; 100% kernel reuse | SC-001 |
| V2 | `whenPhase P` rule over `PhaseReached < P` ⇒ `Pass`; over `≥ P` ⇒ the check's verdict | phase guard inert/transparent (P1/P2) | SC-002 |
| V3 | `Route.route … Inner` ⇒ `Blocking = []` for every inner phase; `… Gate` at `Merge` ⇒ blocking set | advisory inner / single merge fence | SC-003 |
| V4 | `Synthetic` upstream `TaskState` + `TaskDependsOn` ⇒ `evidenceNotSynthetic` `Fail` at merge; no flag flips | kernel taint; non-negotiable evidence | SC-004 |
| V5 | `constitution-complete` advisory inner, blocking at merge under `defaultDial`; empty dial ⇒ fewer blocks | the dial is the blocking set | SC-005 |
| V6 | every `r ∈ Catalog.catalog`: `Check.render` non-empty; `Check.explain` top verdict `= Check.eval` | self-describing rules | SC-006 |
| V7 | compose with a second toy domain: for 100% of the catalog, lifted `(verdict, provenance)` == standalone | faithful lift (F09 guarantee) | SC-007 |
| V8 | SpecKit surface == committed baseline; SpecKit deps ⊆ {BCL, FSharp.Core, Spi, Kernel}; kernel/Spi do not reference SpecKit | surface baseline + dependency hygiene | SC-008 |

## Notes
- **Sensing is out of scope** (FR-015): the live repository (`.specify/feature.json`, `tasks.md`,
  `tasks.deps.yml`, artifact-content hashes) is read into `SpecKitFact`s by the **F08** effects shell and
  the **F12** CLI, not this feature. The tests feed `SpecKitFact`s directly.
- **The second composition domain is synthetic** (Principle V): the Spec Kit adapter is the *real* adopter
  under test; only the toy domain it is composed with for the faithful-lift proof is a synthetic example
  domain — disclosed at its definition, `Synthetic` token in the test names that assert via it, listed in
  the PR description.
