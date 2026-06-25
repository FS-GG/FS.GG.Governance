# Contract: `FS.GG.Governance.HumanText.ReportView` (pure)

The shared, presentation-free navigable model behind the **rich tables** and the **TUI**, so both stay parity-true
to the report object (FR-009). A pure projection — no Spectre, no I/O.

## `ReportView.fsi` (draft)

```fsharp
namespace FS.GG.Governance.HumanText

open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain.Model
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseReport.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GateRun.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReportView =

    type ReportNode =
        | Leaf of label: string * detail: string option
        | Group of title: string * children: ReportNode list

    type ReportView =
        { Title: string
          ExitStatus: string
          Sections: ReportNode list }

    val viewOfRouteResult:
        RouteResult -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> ReportView
    val viewOfRouteExplanation: RouteExplanation -> ReportView
    val viewOfShipDecision:
        ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> ReportView
    val viewOfVerifyDecision:
        ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> ReportView
    val viewOfReleaseReport: ReleaseReport -> ReportView
    val viewOfCacheEligibilityReport: CacheEligibilityReport -> ReportView
```

## Invariants

- **One structure behind every human surface** — `HumanText.of*` (plain), the rich grouped tables, and the TUI all
  render this same `ReportView`, so they cannot diverge from the report object (parity, FR-001/FR-009).
- **Deterministic** — stable section/child ordering, normalized labels; no clock/path/username/env (FR-011).
- **Read-only** — pure projection; carries no effect, no verdict re-derivation.
