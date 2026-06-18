# Phase 1 — Quickstart (F11 · 011-adapter-designsystem)

A run/validation guide for the design-system adapter. It exercises the **public** surface
([`contracts/DesignSystem.fsi`](./contracts/DesignSystem.fsi) + [`contracts/Catalog.fsi`](./contracts/Catalog.fsi))
exactly as the F08 effects shell / F12 CLI would — through the built
`FS.GG.Governance.Adapters.DesignSystem` library and `scripts/prelude.fsx` (Principle I, SC-001/SC-008). It
does not duplicate implementation; see [data-model.md](./data-model.md) and the `.fsi` for the contract.

## Prerequisites
- .NET `net10.0` SDK; repo restored.
- F01–F07 (the pure kernel), **F09** (`FS.GG.Governance.Adapters.Spi`, the SPI + `Lift`/`Composition`), and
  **F10** (`FS.GG.Governance.Adapters.SpecKit`) already merged. This feature adds the **new** pure
  `FS.GG.Governance.Adapters.DesignSystem` project that depends on the SPI (never on F10); the **test** project
  references F10 only to prove cross-domain composition.

## Build & run

```bash
dotnet build src/FS.GG.Governance.Adapters.DesignSystem
dotnet fsi scripts/prelude.fsx        # includes the F11 sketch below
dotnet test                            # runs DesignSystemTests + CatalogTests + LiftTests + the DesignSystem surface drift test
# Bless the NEW DesignSystem surface baseline after the F11 surface lands:
BLESS_SURFACE=1 dotnet test
```

## FSI sketch (added to `scripts/prelude.fsx`)
Drafted against the contracts before any `.fs` body exists; it uses `failwith`-stubs / `Unchecked` where a body
is not yet present — the point of the pass is that the SHAPES typecheck against the two `.fsi` and that
authoring the adapter, governing fixture facts, routing by tier, and lifting alongside F10 read naturally.

```fsharp
// ── Design-system adapter sketch (F11) — a second, unrelated domain adopts the kernel from fixtures ──
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.DesignSystem

let judge : JudgeId = { ModelId = "design-judge"; Version = "1" }
let adapter = Catalog.adapter judge          // the ONE Adapter value — NO dial (FR-003, D8)

// 1. FIVE-COMPONENT + NO-F10-SHAPE (SC-001): the adapter is fully specified by the five SPI
//    components + the Bridge; it exposes NO artifact-authoring op, NO Phase/whenPhase/merge fence/dial.
//    (Verified by inspection of the .fsi: no System.IO, no Model/Msg/Effect, no rendering type, no Phase.)
printfn "rules = %d / probes = %d / fences = %d"
    adapter.Rules.Length adapter.Probes.Length adapter.Fences.Length   // fences = 1 (token-surface only)

// 2. THE TIER SPLIT (SC-002): deterministic token/contrast/surface checks block; judgement is Opaque;
//    adopting a new policy is HumanOnly.
let driftOk   = [ { Id = FactId "m"; Value = SurfaceObservation ("surface-matches", GeneratedTokenSurface, true);  Provenance = [] } ]
let driftBad  = [ { Id = FactId "m"; Value = SurfaceObservation ("surface-matches", GeneratedTokenSurface, false); Provenance = [] } ]
Check.eval driftOk  Catalog.tokenDrift.Check          // ⇒ Pass
Check.eval driftBad Catalog.tokenDrift.Check          // ⇒ Fail (Blocking, deterministic)
Check.eval []       Catalog.contrastPolicy.Check      // ⇒ Uncertain (absent fixture ⇒ Unknown, never silent Pass) (Pr3)
Check.eval []       Catalog.colourInformational.Check // ⇒ Uncertain (Opaque — requires visual judgement) (C4)
Check.isReified Catalog.colourInformational.Check     // ⇒ false  ⇒ AgentReviewed, never Deterministic (FR-008)
Catalog.adoptNewPolicy.Tier                            // ⇒ HumanOnly (escalates, never decides) (C4)

// 3. ADVISORY BY DEFAULT; THE TOKEN-SURFACE FENCE (SC-002): only a change touching the public token
//    surface trips the single fence — there is NO merge fence and NO phase (the difference from F10).
let plainChange  = { Surfaces = Set.ofList [ RenderedCapture ] }            // not the token surface
let surfaceChange = { Surfaces = Set.ofList [ GeneratedTokenSurface ] }     // the high-stakes surface
let plainRoute   = Route.route adapter.Fences adapter.Rules Gate plainChange    // Blocking = []  (advisory)
let fencedRoute  = Route.route adapter.Fences adapter.Rules Gate surfaceChange  // Blocking = the deterministic/HumanOnly set
printfn "plain blocking = %d / fenced blocking = %d" plainRoute.Blocking.Length fencedRoute.Blocking.Length

// 4. EVIDENCE / TAINT via the KERNEL (SC-003 taint): a deterministic verdict resting on a synthetic
//    input is AutoSynthetic via F05's fixed point; evidenceMeasured fails and NO flag flips it.
let tainted =
    [ { Id = FactId "x"; Value = MeasurementState ("contrast-px", Synthetic);   Provenance = [] }
      { Id = FactId "v"; Value = MeasurementState ("contrast-verdict", Real);   Provenance = [] }
      { Id = FactId "e"; Value = VerdictRestsOn ("contrast-verdict", "contrast-px"); Provenance = [] } ]
Check.eval tainted Catalog.evidenceMeasured.Check     // ⇒ Fail (contrast-verdict is AutoSynthetic via contrast-px) (E1/E3)

// 5. RENDER & EXPLAIN + COMMUTATIVE HASH (SC-004/SC-005): every rule renders to a sentence; the
//    deterministic hash is invariant under commutative re-ordering of sub-checks.
for r in Catalog.catalog do
    printfn "%s :: %s" (let (RuleId id) = r.Id in id) (Check.render r.Check)
// Check.hash (allOf [a; b]) = Check.hash (allOf [b; a])   ⇒ cache key does not move (H1)

// 6. FAITHFUL LIFT alongside the REAL F10 adapter (SC-006/SC-007): the adapter lifts unchanged into a
//    ProjectFact coproduct that ALSO carries the Spec Kit adapter — proven in LiftTests; standalone ==
//    lifted (verdict, provenance) for 100% of the catalog; neither domain references the other.
```

## Validation scenarios (mapped to Success Criteria)

| # | Scenario | Asserts | SC |
|---|---|---|---|
| V1 | `Catalog.adapter judge` supplies the five components; `.fsi` shows no authoring op, no I/O, no Phase/whenPhase/merge fence/dial | five-component / observer-only / no-F10-shape; 100% kernel reuse | SC-001 |
| V2 | `tokenDrift`/`contrastPolicy`/`tokenSurfaceGate` give definite verdicts & are `Blocking`; `Opaque` rules stay out of `Deterministic` & route with their `Question`; `adoptNewPolicy` (`HumanOnly`) never resolves deterministically | the tier split | SC-002 |
| V3 | full catalog evaluates/explains over the fixture token tree with no rendering lib on the path; kernel/SPI surfaces carry zero rendering/token/colour/layout vocabulary; probes report `Met`/`Unmet`/`Unknown` | fixtures only; kernel stays neutral | SC-003 |
| V4 | every `r ∈ Catalog.catalog`: `Check.render` non-empty; `Check.explain` top verdict `= Check.eval`; published `Statement` `= Check.render` | advertised = enforced | SC-004 |
| V5 | a deterministic rule's `Check.hash` invariant under commutative re-ordering; two structurally-equal `Opaque` rules ⇒ same cache key | render-and-hash, cache-stable | SC-005 |
| V6 | compose alongside the real F10 adapter: for 100% of the catalog, lifted `(verdict, provenance)`/render/hash/reads == standalone | faithful lift (F09 guarantee) | SC-006 |
| V7 | two unrelated domains (Spec Kit, design-system) at one root; neither references the other; dropping one `Lifted` removes it | the adoption bar | SC-007 |
| V8 | DesignSystem surface == committed baseline; DesignSystem deps ⊆ {BCL, FSharp.Core, Spi, Kernel} (NOT F10); kernel/Spi/SpecKit do not reference DesignSystem | surface baseline + dependency hygiene | SC-008 |

## Notes
- **Sensing is out of scope** (FR-015): a live design system (the token tree, rendered captures, contrast
  ratios, artifact-content hashes) is read into `DesignSystemFact`s by the **F08** effects shell and the
  **F12** CLI, not this feature. The tests carry a small fixture token tree under `fixtures/` and lift it to
  facts directly.
- **The shipped adapter never references F10** (FR-005/FR-016): the two adapters are independent siblings. Only
  the **test** project references F10, solely to prove the faithful lift / adoption bar by composing the two
  real domains at one root (D9). The dependency-hygiene test enforces the asymmetry (SC-008).
- **No rendering library, no I/O, no dial, no phase** — the design-system adapter is the deliberate *opposite
  shape* to the Spec Kit adapter, which is exactly what makes it a real second adopter (FR-005).
