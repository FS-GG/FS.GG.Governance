# Phase 1 — Quickstart (F07 · 007-routing-severity-modes)

A run/validation guide for the light routing layer. It exercises the **public** surface
([`contracts/Route.fsi`](./contracts/Route.fsi)) exactly as a downstream consumer (F08/F12)
would — through the built library and `scripts/prelude.fsx` (Principle I, SC-011). It does not
duplicate implementation; see [data-model.md](./data-model.md) and the `.fsi` for the contract.

## Prerequisites
- .NET `net10.0` SDK; repo restored.
- F02/F03/F04/F06 already merged (the kernel; this feature is additive).

## Build & run the FSI sketch
```bash
dotnet build src/FS.GG.Governance.Kernel
dotnet fsi scripts/prelude.fsx        # includes the F07 routing sketch below
dotnet test                           # runs RouteTests + the re-blessed surface drift test
# Re-bless the surface baseline after the F07 surface lands:
BLESS_SURFACE=1 dotnet test
```

## FSI sketch (added to `scripts/prelude.fsx`)
Drafted against the contract before any `Route.fs` body exists; it calls `failwith`-stubs until
the bodies land — the point of the pass is that the SHAPES typecheck against `Route.fsi`.

```fsharp
// ── Routing sketch (F07) — light by default, deterministic precedence, explainable ──
open FS.GG.Governance.Kernel
open FS.GG.Governance.Kernel.Check   // .&, operators (for building demo checks)

// A real, domain-neutral 'change: a set of changed "paths" (any adapter shape works — D1).
let change = set [ "src/Api.fs"; "README.md" ]

// Two declared fences. forbid-trumps-permit: ANY trip ⇒ Fenced (order-independent).
let mergeFence  = { Name = "merge-boundary";   Trips = fun (c: Set<string>) -> c |> Set.exists (fun p -> p.StartsWith "src/") }
let secFence    = { Name = "security-surface"; Trips = fun (c: Set<string>) -> c.Contains "src/Auth.fs" }
let fences = [ mergeFence; secFence ]

// 1. Light by default: a change tripping no fence is Routine (V40).
printfn "stakesOf [] (no fence)   = %A" (Route.stakesOf [] change)                 // Routine
printfn "stakesOf docs-only       = %A" (Route.stakesOf fences (set [ "README.md" ]))  // Routine

// 2. A single matching fence ⇒ Fenced; order-independent across permutations (V41/V43).
printfn "stakesOf fenced          = %A" (Route.stakesOf fences change)             // Fenced "merge-boundary"
printfn "stakesOf permuted equal? %b" (Route.stakesOf fences change = Route.stakesOf (List.rev fences) change)

// A real blocking rule (reuse an F03 check; F04 authors + promotes it).
let hasReview = Check.probe "peer-reviewed" [] [] (fun (_: FactSet<string>) -> Met)
let spec = { Document = "constitution.md"; Section = "I" }
let blockingRule =
    CheckRule.rule (RuleId "peer-review") Deterministic spec hasReview
    |> Result.map CheckRule.blocking
    |> function Ok r -> r | Error e -> failwithf "%A" e

// 3. Run-mode matrix: same fenced change + blocking rule — advisory in Inner, blocking in Gate;
//    stakes identical across modes (V44).
let inGate  = Route.route fences [ blockingRule ] Gate  change
let inInner = Route.route fences [ blockingRule ] Inner change
printfn "\nGate  blocking count = %d" (List.length inGate.Blocking)    // 1
printfn "Inner blocking count = %d" (List.length inInner.Blocking)     // 0 (advisory only)
printfn "stakes equal across modes? %b" (inGate.Stakes = inInner.Stakes)

// 4. Light change at Gate still blocks nothing (V40).
let lightAtGate = Route.route fences [ blockingRule ] Gate (set [ "README.md" ])
printfn "light @ Gate blocking = %d" (List.length lightAtGate.Blocking)  // 0

// 5. Drift-proof gate: the gate's Statement IS Check.render of the rule's check (V46).
printfn "gate statement = render? %b" ((List.head inGate.Blocking).Statement = Check.render blockingRule.Check)

// 6. Every route carries a non-empty reason — routine and fenced (V45).
printfn "fenced reason non-empty? %b"  (inGate.Reason <> "")
printfn "routine reason non-empty? %b" ((Route.route [] [] Inner change).Reason <> "")

// 7. renderRoute is deterministic and execution-free (V47).
printfn "\n%s" (Route.renderRoute inGate)
printfn "render deterministic? %b" (Route.renderRoute inGate = Route.renderRoute inGate)
```

## Validation scenarios (→ semantic tests in `RouteTests.fs`)

| ID | Scenario | Asserts | Spec |
|---|---|---|---|
| **V40** | Light by default | no matching fence (and empty fence set) ⇒ `Routine` + empty `Blocking` in *every* mode | US1, FR-006, SC-001 |
| **V41** | Single fence trips | ≥1 matching fence ⇒ `Fenced` carrying its name | US2/US4, FR-004, SC-002 |
| **V42** | Fenced gate explained | fenced change at `Gate` ⇒ non-empty `Blocking`; render names rule + fence + rendered check | US2, FR-012, SC-006 |
| **V43** | Order-independent precedence | permuting the fence list ⇒ identical `Stakes` and identical `Route` (forbid trumps permit) | US4, FR-005, SC-003 |
| **V44** | Run-mode matrix | one blocking rule on a fenced change: advisory in `Sandbox`/`Inner`, blocking only in `Gate`; stakes identical across modes | US3, FR-008/009, SC-004 |
| **V45** | Reason mandatory | every route (routine & fenced) has a non-empty `Reason` | US1/US5, FR-011, SC-005 |
| **V46** | Drift-proof gate | `gate.Statement = Check.render rule.Check` byte-for-byte | US2, FR-012, SC-006 |
| **V47** | Short, filterable, deterministic | `Blocking` = exactly the blocking gates, bounded by applicable rules; `renderRoute` deterministic & execution-free; no probe/review run | US5, FR-013/014, SC-007/008/010 |

## Expected outcomes
- `Routine` changes cost nothing — empty `Blocking`, a clear "light — no gates" reason, in any
  mode (SC-001).
- A `Fenced` change blocks **only** at `Gate`, and every gate names its rule, fence, and the
  exact rendered check (SC-004/SC-006).
- Stakes and routes are **byte-for-byte reproducible** and **independent of fence order**
  (SC-003/SC-008); no probe or agent review runs anywhere in the suite (SC-010); the kernel
  still references only the BCL + FSharp.Core (SC-011, the re-blessed V11/V12 tests).
