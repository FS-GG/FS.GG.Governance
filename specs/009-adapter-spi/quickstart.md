# Phase 1 — Quickstart (F09 · 009-adapter-spi)

A run/validation guide for the adapter SPI and composition root. It exercises the **public**
surface ([`contracts/Adapter.fsi`](./contracts/Adapter.fsi) +
[`contracts/Composition.fsi`](./contracts/Composition.fsi)) exactly as a downstream consumer
(F10/F11/F12) would — through the built `FS.GG.Governance.Adapters.Spi` library and
`scripts/prelude.fsx` (Principle I, SC-008). It does not duplicate implementation; see
[data-model.md](./data-model.md) and the `.fsi` for the contract.

## Prerequisites
- .NET `net10.0` SDK; repo restored.
- F01–F07 already merged (the pure kernel). This feature adds the **new** pure
  `FS.GG.Governance.Adapters.Spi` project that depends on the kernel.

## Build & run

```bash
dotnet build src/FS.GG.Governance.Adapters.Spi
dotnet fsi scripts/prelude.fsx        # includes the F09 sketch below
dotnet test                            # runs AdapterTests + CompositionTests + the Spi surface drift test
# Bless the NEW Spi surface baseline after the F09 surface lands:
BLESS_SURFACE=1 dotnet test
```

## FSI sketch (added to `scripts/prelude.fsx`)
Drafted against the contracts before any `.fs` body exists; it uses `failwith`-stubs/`Unchecked`
where a body is not yet present — the point of the pass is that the SHAPES typecheck against the
two `.fsi` and that authoring an adapter, lifting it, and composing two read naturally.

```fsharp
// ── Adapter SPI sketch (F09) — a domain plugs in by supplying only its own vocabulary ──
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open Check   // the ==> / .& operators

// ── Toy domain A (SYNTHETIC example domain — illustrative, not a real adopter) ──
type DocFact = HasTitle of bool | Reviewed of RuleOutcome      // a tiny "document" domain
type DocArtifact = TheDoc
let docToRef = function TheDoc -> { Kind = "doc"; Key = "the-doc" }
let titled = Check.probe "has-title" [ docToRef TheDoc ] []
                (fun fs -> if fs |> List.exists (fun f -> f.Value = HasTitle true) then Met
                           else Unmet "no title")
let docRule = CheckRule.rule (RuleId "doc-titled") Deterministic
                { Document="doc-policy"; Section="title" } titled |> Result.toOption |> Option.get
let docBridge : Bridge<DocFact> =
    { Judge = { ModelId="test"; Version="1" }
      ArtifactHash = fun _ _ -> ""
      Embed = Reviewed
      Project = function Reviewed o -> Some o | _ -> None }
let docAdapter : Adapter<DocFact, DocArtifact, Set<string>> =
    { Identify = (FactId << sprintf "%A"); ToRef = docToRef; Probes = [ (* titled's probe *) ]
      Rules = [ docRule ]; Fences = [ { Name="doc"; Trips = Set.contains "doc.md" } ]
      Bridge = docBridge }

// 1. STANDALONE: the adapter governs itself using ONLY kernel facilities (V61/V62).
let supplied = [ { Id = FactId "t"; Value = HasTitle true; Provenance = [] } ]
let stdRules = Adapter.toRules docAdapter
let stdResult = FixedPoint.evaluate docAdapter.Identify stdRules supplied   // kernel inference
printfn "standalone rounds = %d / facts = %d" stdResult.Rounds stdResult.Facts.Length

// ── Toy domain B (SYNTHETIC) — UNRELATED vocabulary, distinct artifacts/probes (V70/V71) ──
type TaskFact = TaskOpen of bool | TaskGov of RuleOutcome
type TaskArtifact = TheTask

// ── The composition root (consumer-authored): the closed coproduct + its wiring (D8) ──
type ProjectFact = Doc of DocFact | Task of TaskFact | Gov of RuleOutcome
let (|DocP|_|)  = function Doc f  -> Some f | _ -> None
let (|TaskP|_|) = function Task f -> Some f | _ -> None
let projBridge : Bridge<ProjectFact> =
    { Judge = { ModelId="test"; Version="1" }; ArtifactHash = fun _ _ -> ""
      Embed = Gov; Project = function Gov o -> Some o | _ -> None }
let projIdentify : ProjectFact -> FactId = FactId << sprintf "%A"   // delegates per case (law L3)

// 2. FAITHFUL LIFT: the lifted check's render & hash are byte-identical to standalone (V63/V64).
let lifted = Lift.checkRule (|DocP|_|) docRule
printfn "render invariant = %b" (Check.render lifted.Check = Check.render docRule.Check)   // true
printfn "hash invariant   = %b" (Check.hash lifted.Check   = Check.hash docRule.Check)     // true (cache key stable)

// 3. COMPOSE two unrelated adapters + one cross-domain Implies at the one root (V66/V67).
let crossDomain =
    // "if the doc is titled, the task must be governed" — Implies over the coproduct (FR-007)
    [ CheckRule.rule (RuleId "doc-task-link") AgentReviewed { Document="root"; Section="x-domain" }
        (Lift.check (|DocP|_|) titled ==> Check.probe "task-governed" [] [] (fun _ -> Met))
      |> Result.toOption |> Option.get ]
let composed =
    Composition.compose
        [ Composition.lift (|DocP|_|)  id docAdapter
          Composition.lift (|TaskP|_|) id (Unchecked.defaultof<Adapter<TaskFact,TaskArtifact,Set<string>>>) ]
        crossDomain
printfn "composed catalog  = %d rules / %d fences" composed.Catalog.Length composed.Fences.Length

// 4. The composed catalog runs through the UNCHANGED kernel (V66, SC-006).
let projRules = Composition.toRules projBridge composed
let projFacts = [ { Id = FactId "d"; Value = Doc (HasTitle true); Provenance = [] } ]
let projResult = FixedPoint.evaluate projIdentify projRules projFacts
let projRoute  = Route.route composed.Fences composed.Catalog Gate (set [ "doc.md" ])
printfn "project rounds=%d stakes=%A" projResult.Rounds projRoute.Stakes

// 5. REMOVAL/BOUNDARY: drop one adapter — the rest is intact, cross-domain rule goes inert (V69).
let withoutTask = Composition.compose [ Composition.lift (|DocP|_|) id docAdapter ] crossDomain
printfn "after removal     = %d rules (kernel + doc intact)" withoutTask.Catalog.Length
```

## Validation scenarios (→ semantic tests)

| ID | Scenario | Asserts | Spec |
|---|---|---|---|
| **V61** | Five-part contract is total | a missing component does not compile (a documented compile guard); an `Adapter` value carries exactly the five + `Bridge` | US1, FR-001/014, SC-001 |
| **V62** | Standalone governs itself | an example adapter derives facts, evaluates rules, renders an explanation using ONLY kernel facilities (no inference/arbitration/render/hash/route code of its own) | US1, FR-002, SC-001 |
| **V63** | Faithful lift — verdict+provenance | for 100% of an adapter's rules, the lifted rule's `(verdict, provenance)` over coproduct-wrapped facts equals the standalone original byte-for-byte | US2, FR-004, SC-002 |
| **V64** | Faithful lift — render/hash invariant | `Check.render`/`Check.hash`/`reads`/`isReified` of a lifted check equal the original (cache key does not move) | US2, FR-004, SC-002 |
| **V65** | Lifted `Opaque`/`AgentReviewed` | a lifted judgement rule stays out of `Deterministic` and routes to review exactly as un-lifted | US2, FR-004 |
| **V66** | Composed runs through the kernel | the composed catalog evaluates via UNCHANGED `CheckRule.toRule` + `FixedPoint.evaluate`; the kernel gains no adapter code | US2, FR-005, SC-006 |
| **V67** | Cross-domain `Implies` | a single cross-domain rule at the root couples two domains; a blocking result wins under F07 precedence | US3, FR-007, SC-003 |
| **V68** | Order-independence (property) | every permutation of adapter-composition order and rule order ⇒ identical least fixed point and identical merged route/verdict | US3, FR-008, SC-003/SC-007 |
| **V69** | Removal / boundary | compose ≥2, drop one ⇒ kernel + remaining adapter(s) evaluate unchanged; the cross-domain rule naming the removed domain goes inert (`Unmet`), not throws | US4, FR-009, SC-004 |
| **V70** | Two unrelated domains | two example adapters with distinct vocabularies/artifacts/probes each govern themselves; neither imports the other's facts/artifacts/probes/rules | US5, FR-010, SC-005 |
| **V71** | Compose unrelated without reshaping | the two unrelated adapters compose at one root without either being reshaped to resemble the other | US5, FR-010, SC-005 |
| **V72** | Surface + hygiene | the Spi public surface matches the blessed baseline; Spi references only BCL/FSharp.Core/Kernel (the kernel does NOT reference Spi) | FR-015/016, SC-008 |

## Expected outcomes
- An adapter is **fully specified by exactly five** components + the F04 `Bridge` wiring, and
  **reuses 100%** of kernel facilities — it contains no inference, arbitration, evidence,
  rendering, hashing, explanation, severity, or routing code of its own (SC-001).
- **Lifting is faithful**: a lifted rule's verdict and provenance are byte-for-byte identical to
  the standalone original, and `render`/`hash` are invariant so the agent-review cache key does not
  move (SC-002).
- Several adapters **compose at one root** through the **unchanged** kernel (SC-006); cross-domain
  coupling is a small, named `Implies` set whose merged verdict is **deterministic and
  order-independent** (a blocking result always wins) under every permutation (SC-003/SC-007).
- **Removing one adapter** leaves the kernel and the remaining adapter(s) intact, and a
  cross-domain rule naming the removed domain becomes **inert** rather than throwing — the boundary
  test, the concrete proof that the kernel is a library, not a platform (SC-004).
- **Two unrelated** example domains adopt the kernel with **zero cross-copying** (SC-005), and the
  Spi adds **no heavy dependency** — BCL + FSharp.Core + kernel only, the kernel not referencing it
  (SC-008, the new surface/hygiene test).
