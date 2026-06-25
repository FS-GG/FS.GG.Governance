# Contract — Per-Host Render Dispatch

**Scope**: the text/human + rich render branch of each wired host (`route`, `ship`, `verify`, evidence-standalone).
The JSON branch is **unchanged** (see `cli-surface.md`). No new public type; this is a host-interpreter branch.

## Dispatch (per host, at the interpreter edge)

```
explicitJson = <host's existing JSON selector: --json bool, or --format json>
capability   = HumanRender.Capability.senseCapability explicitPlain     // --plain / --no-color
mode         = HumanText.selectMode explicitJson capability             // pure, F27, unchanged

match mode with
| Json  -> <existing JSON path — byte-for-byte unchanged>
| Plain -> emit (HumanText.of<report> report …) ; emit <host operational lines>
| Rich  -> RichRender.emit Rich (ReportView.viewOf<report> report …)
                              (HumanText.of<report> report …) console
           ; emit <host operational lines>
```

## Per-host bindings

| Host | `of<report>` / `viewOf<report>` | Report value (already in `Model`) |
|---|---|---|
| `route` | `ofRouteResult` / `viewOfRouteResult` | `model.Result : RouteResult` + `CacheEligibilityReport option` + `(GateId*GateOutcome) list` |
| `ship` | `ofShipDecision` / `viewOfShipDecision` | `model.Decision : ShipDecision` + aux tuple |
| `verify` | `ofVerifyDecision` / `viewOfVerifyDecision` | `model.Decision : ShipDecision` + aux tuple |
| evidence | `ofCacheEligibilityReport` / `viewOfCacheEligibilityReport` | `CacheEligibility.evaluate candidates store : CacheEligibilityReport` (already computed in `Loop.fs`) |

## Guarantees

- **G1 (single source of truth)**: the report value passed to `of<report>` is the same value the JSON path projects
  (SC-001).
- **G2 (operational lines)**: `<host operational lines>` (e.g. `wrote <path> (<schema>)`, changed-path counts) are
  emitted by the host around the report projection, never inside the JSON contract, never inside the `HumanText`
  string (FR-003). `release` has no such lines (it is deferred anyway).
- **G3 (ANSI discipline)**: `Plain` and `Json` emit no ANSI escapes; only `Rich` does (FR-005, SC-003).
- **G4 (Json bypass)**: `Json` never constructs a `ReportView` and never calls `RichRender` (SC-004).
- **G5 (blocked stays blocked)**: the `HumanText` projection renders a blocked verdict as blocked (F27 guarantee).
