# Phase 1 — Quickstart (F08 · 008-effects-interpreter)

A run/validation guide for the effects shell. It exercises the **public** surface
([`contracts/Loop.fsi`](./contracts/Loop.fsi) + [`contracts/Interpreter.fsi`](./contracts/Interpreter.fsi))
exactly as a downstream consumer (F12) would — through the built `FS.GG.Governance.Host` library
and `scripts/prelude.fsx` (Principle I, SC-010). It does not duplicate implementation; see
[data-model.md](./data-model.md) and the `.fsi` for the contract.

## Prerequisites
- .NET `net10.0` SDK; repo restored.
- F01–F07 already merged (the pure kernel). This feature adds the **new** `Host` project that
  depends on the kernel.

## Build & run

```bash
dotnet build src/FS.GG.Governance.Host
dotnet fsi scripts/prelude.fsx        # includes the F08 sketch below
dotnet test                            # runs LoopTests + InterpreterTests + the Host surface drift test
# Bless the NEW Host surface baseline after the F08 surface lands:
BLESS_SURFACE=1 dotnet test
```

## FSI sketch (added to `scripts/prelude.fsx`)
Drafted against the contracts before any `.fs` body exists; it uses `failwith`-stubs until the
bodies land — the point of the pass is that the SHAPES typecheck against the two `.fsi` and that
both sides of the boundary (pure `update`, edge `run`) read naturally.

```fsharp
// ── Effects-shell sketch (F08) — sense → plan → act, nondeterminism reified as evidence ──
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

// A domain-neutral 'change (set of changed paths) and 'fact (string) for the sketch.
let change = set [ "src/Api.fs" ]

// A minimal LoopConfig: identity, one AgentReviewed rule, a bridge, one fence, Gate mode,
// the default policy, and a sense-lift. (Adapter wiring is F09/F12's job — supplied here.)
let cfg : LoopConfig<Set<string>, string> =
    { Identify       = FactId
      Rules          = [ (* an AgentReviewed CheckRule over a probe that reads "src/Api.fs" *) ]
      Bridge         = Unchecked.defaultof<_>   // supplied by the host; sketch only
      Fences         = [ { Name = "merge-boundary"; Trips = fun c -> c |> Set.exists (fun p -> p.StartsWith "src/") } ]
      Mode           = Gate
      Policy         = Loop.defaultPolicy        // SingleSample (documented default)
      SenseArtifact  = fun ref content -> sprintf "%s=%s" ref.Key content }

// 1. PURE side: init computes the Route and emits sense effects — NO I/O (V48).
let (m0, startup) = Loop.init cfg change
printfn "init phase      = %A" m0.Phase                       // Sensing
printfn "startup effects = %A" startup                        // [ ReadArtifact {Key="src/Api.fs";...} ]
printfn "route stakes    = %A" m0.Route.Stakes               // Fenced "merge-boundary"

// 2. PURE side: feed a sensed-artifact Msg; assert the next Model + effects with zero I/O (V49).
let (m1, eff1) = Loop.update cfg (Sensed ({ Kind="file"; Key="src/Api.fs" }, Ok "let x = 1")) m0
printfn "after sense     = %A / %A" m1.Phase eff1            // Planning / [ LoadReview key ]

// 3. PURE side: the acceptance policy is a pure fold — below-policy stays pending (V51/V52).
printfn "accept single   = %A" (Loop.accept SingleSample      [ { Verdict = Pass; Confidence = 0.9 } ])  // Freeze Pass
printfn "accept agree<n  = %A" (Loop.accept (Agreement 2)     [ { Verdict = Pass; Confidence = 0.9 } ])  // StayPending
printfn "accept conf<t   = %A" (Loop.accept (Confidence 0.8)  [ { Verdict = Pass; Confidence = 0.5 } ])  // StayPending

// 4. EDGE side: drive run against a REAL temp fixture + a FAKE judge + a real-fs store (V53/V55).
let tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString())
System.IO.Directory.CreateDirectory tmp |> ignore
System.IO.File.WriteAllText(System.IO.Path.Combine(tmp, "Api.fs"), "let x = 1")

let mutable dispatches = 0
let ports : Ports =
    { Read  = fun ref ->
                try Ok (System.IO.File.ReadAllText(System.IO.Path.Combine(tmp, System.IO.Path.GetFileName ref.Key)))
                with e -> Error e.Message
      Judge = fun _task -> dispatches <- dispatches + 1; Ok { Verdict = Pass; Confidence = 1.0 }   // fake judge
      Store = let cache = System.Collections.Generic.Dictionary<string, RecordedReview>()           // real-ish store
              { Load = fun k -> Ok (match cache.TryGetValue k with | true, v -> Some v | _ -> None)
                Save = fun rr -> cache.[rr.Key] <- rr; Ok () }
      Sink  = fun out -> printfn "emit: %A" out }

let first  = Interpreter.run ports cfg change
printfn "\nfirst run dispatches  = %d" dispatches            // 1 (one cache MISS dispatched + frozen)
let second = Interpreter.run ports cfg change
printfn "second run dispatches  = %d" dispatches             // 1 (UNCHANGED — cache HIT, zero new dispatch) (V55)
printfn "final phase           = %A" second.Phase            // Quiescent
printfn "no failures           = %b" (List.isEmpty second.Failures)
```

## Validation scenarios (→ semantic tests)

| ID | Scenario | Asserts | Spec |
|---|---|---|---|
| **V48** | Pure init | `init` computes the `Route` and emits `ReadArtifact` per declared read; **no I/O** | US1, FR-001/005, SC-001 |
| **V49** | Pure transition | `(Model, Sensed) ⇒` next `Model` + effects, asserted with zero I/O | US1, FR-002, SC-001 |
| **V50** | Deterministic update | identical `(Model, Msg)` ⇒ byte-for-byte identical `(Model, effects)` | US1, FR-002, SC-001 |
| **V51** | Policy freezes when met | `accept` freezes a policy-meeting sample set | US4, FR-009, SC-004 |
| **V52** | Policy stays pending | below-policy ⇒ `StayPending`; nothing recorded/cached; conclusion `Uncertain` | US4, FR-009, SC-004 |
| **V53** | Real-fs sense→plan→act | `run` over a real temp fixture + fake judge yields the kernel's fact set | US2, FR-004/016, SC-002 |
| **V54** | Round-trip freeze | first run: exactly one dispatch + one recorded verdict against the F04 key | US3, FR-007, SC-003 |
| **V55** | Cache hit on re-run | second run over unchanged change: **zero** dispatches (cache hit), same decision | US3, FR-008, SC-003 |
| **V56** | Stale ⇒ fresh | mutate any cache-key ingredient ⇒ exactly one fresh dispatch | US3, FR-008, SC-003 |
| **V57** | Instruction isolation | injection-laden artifact ⇒ `Instruction` byte-identical to honest case; only `Data` differs | US5, FR-010, SC-005 |
| **V58** | Safe failure | missing artifact + failing judge ⇒ handled `Msg`s, conclusion `Uncertain`/`Failed`, no throw, well-formed `Model` | US6, FR-012, SC-006 |
| **V59** | Idempotent + order-independent | re-applied result `Msg` records no duplicate; permuted completion order ⇒ identical final `Model` | US6, FR-014, SC-007 |
| **V60** | Gate from base + emit + hygiene | gates enforced only at `Gate` recomputed from base; F06 outputs emitted to the sink; Host deps = BCL/FSharp.Core/Kernel only | US2, FR-011/015/018, SC-008/009 |

## Expected outcomes
- The **entire decision logic** is a pure function of state and events — every `update`
  transition is asserted with **zero** I/O (SC-001).
- The full `sense → plan → act` loop runs end-to-end against a **real filesystem fixture** and a
  **fake judge**, producing the kernel's fact set (SC-002); a verdict round-trips and the **cache
  hits on re-run** (zero dispatches), while any cache-key change forces one fresh dispatch (SC-003).
- A below-policy stochastic verdict is **never frozen** (SC-004); an injection-laden artifact
  never alters the reviewer instruction (SC-005); every failure surfaces as a handled `Msg` with
  no unhandled exception (SC-006); the loop is order- and repetition-robust (SC-007); gates are
  recomputed from base (SC-008); and the suite reaches **no real network or agent** — the Host
  references only the BCL + FSharp.Core + the kernel (SC-009, the new V13/V14 tests).
