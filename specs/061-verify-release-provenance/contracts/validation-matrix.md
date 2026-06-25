# Contract: `FS.GG.Governance.ValidationMatrix` (pure, P3)

A declared exhaustive validation matrix + a pure boundary decision reusing the F25 `CostBudget` ordered `Cost`
ceiling (research D4). No scheduler, no cron, no network in the cores — the actual CI trigger that *invokes* the
scheduled boundary is a host/CI concern out of this row's scope. F26 supplies the declaration surface + the
decision only.

## `Model.fsi` (draft)

```fsharp
namespace FS.GG.Governance.ValidationMatrix

open FS.GG.Governance.Config.Model            // Cost

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// A declared broad validation matrix (product-neutral; the axes are opaque tokens).
    type ExhaustiveMatrix =
        { Name: string
          Cost: Cost
          Dimensions: string list }

    /// Which run boundary is executing.
    type MatrixBoundary =
        | InnerLoop
        | ScheduledOrRelease

    /// Why a declared matrix did not run now (named, deterministic).
    type DeferReason =
        | DeferredToScheduledBoundary of name: string * cost: Cost

    /// The decision.
    type MatrixPlan =
        | RunNow of ExhaustiveMatrix
        | Deferred of DeferReason
        | NotDeclared
```

## `Matrix.fsi` (draft)

```fsharp
namespace FS.GG.Governance.ValidationMatrix

open FS.GG.Governance.CostBudget.Model         // CostBudget (the F25 ordered ceiling)
open FS.GG.Governance.ValidationMatrix.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Matrix =

    /// Decide whether a DECLARED exhaustive matrix runs now or is deferred, reusing the F25 CostBudget
    /// ceiling VERBATIM. None ⇒ NotDeclared (never an invented matrix, FR-009). Some m ⇒ RunNow m iff the
    /// boundary's budget admits m.Cost (i.e. ScheduledOrRelease admits Exhaustive), else
    /// Deferred (DeferredToScheduledBoundary …). PURE, TOTAL.
    val decideMatrix:
        budget: CostBudget ->
        boundary: MatrixBoundary ->
        declared: ExhaustiveMatrix option ->
            MatrixPlan
```

> The exact `CostBudget` constructor/accessor is reused from F25 verbatim; the contract depends only on the
> "does this budget admit `Exhaustive`?" predicate the F25 surface already exposes. If F25 exposes the ceiling
> as an ordered `Cost`, `decideMatrix` compares `m.Cost <= ceiling`; the `Plan.fs` body reuses the F25 helper
> rather than re-implementing the comparison.

## Behavioral guarantees (tested)

- A declared `Exhaustive` matrix at `InnerLoop` ⇒ `Deferred (DeferredToScheduledBoundary …)` — does not run,
  recorded as deferred (SC-006 / Story 5.1).
- The same matrix at `ScheduledOrRelease` ⇒ `RunNow` — runs and gates the verdict (SC-006 / Story 5.2).
- `None` declared ⇒ `NotDeclared` — no matrix invented at any boundary (SC-006 / Story 5.3).
