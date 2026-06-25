# Contract: `FS.GG.Governance.HumanText` (pure, P1)

A pure projection library — the **human sibling of the `*Json` libraries**. One ANSI-free, deterministic
projection per report object, each mirroring the matching `*Json.of*` input tuple so the human view and the JSON
are two projections of the **same** immutable report value (FR-001). Non-contractual (smoke-snapshot stable), but
deterministic in content (FR-003, FR-011).

## `HumanText.fsi` (draft)

```fsharp
namespace FS.GG.Governance.HumanText

open FS.GG.Governance.Route.Model              // RouteResult
open FS.GG.Governance.RouteExplain.Model         // RouteExplanation
open FS.GG.Governance.Ship.Model                 // ShipDecision
open FS.GG.Governance.ReleaseReport.Model         // ReleaseReport
open FS.GG.Governance.CacheEligibility.Model       // CacheEligibilityReport
open FS.GG.Governance.Gates.Model                  // GateId
open FS.GG.Governance.GateRun.Model                // GateOutcome

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HumanText =

    val ofRouteResult:
        result: RouteResult ->
        cache: CacheEligibilityReport option ->
        outcomes: (GateId * GateOutcome) list ->
        string

    val ofRouteExplanation: explanation: RouteExplanation -> string

    val ofShipDecision:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        outcomes: (GateId * GateOutcome) list ->
        string

    val ofVerifyDecision:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        outcomes: (GateId * GateOutcome) list ->
        string

    val ofReleaseReport: report: ReleaseReport -> string

    val ofCacheEligibilityReport: report: CacheEligibilityReport -> string
```

## Invariants (must hold for every `of*`)

- **ANSI-free** — no `ESC[` / CSI sequence ever (FR-004, SC-002). Verified for clean/blocked/warnings fixtures.
- **Report-object parity** — conveys the same verdict, blockers, exit status as the matching `*Json.of*` over the
  same inputs; never separately computed (FR-001, SC-001).
- **Deterministic** — byte-identical on repeated calls; no abs-path/clock/username/env; stable ordering, normalized
  paths (FR-011, SC-003).
- **Blocked is blocked** — a blocked verdict renders explicit blocking reason(s) + exit status, matching the report
  object's exit-code basis; never softened (FR-002).
- **Safe failure** — an input-error signal carried in the report renders as a clear input signal distinct from a
  tool defect; no fabricated report (FR-012, Constitution VI).
- **Non-contractual but snapshot-stable** — wording/layout may change; a change re-blesses the smoke snapshot only
  and leaves every JSON golden byte-identical (FR-003, SC-008).
