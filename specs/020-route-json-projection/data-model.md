# Phase 1 Data Model: Deterministic route.json Projection (F020)

This row introduces **no new domain types**. It consumes the F019 `RouteResult` (and the types it
embeds) and produces a JSON **string**. The "route.json document" entities the spec names are realized
as the wire object shape fixed in [`contracts/route-json-document.md`](./contracts/route-json-document.md),
not as new F# records. The only new public bindings are `RouteJson.ofRouteResult` and
`RouteJson.schemaVersion` (see [`contracts/RouteJson.fsi`](./contracts/RouteJson.fsi)).

## 1. Consumed value (read-only; nothing re-derived)

The whole input is one already-validated, already-ordered `RouteResult` (`FS.GG.Governance.Route.Model`):

```
RouteResult = { SelectedGates: SelectedGate list   // F019: sorted by GateId ordinal
                Findings:      FindingReport        // F017: carried order (path, then finding-id token)
                Cost:          CostRollup }         // F019: per-tier counts, order-free

SelectedGate = { Gate: Gate                          // F018 gate, embedded verbatim
                 SelectingPaths: SelectingPath list }// F019: sorted by normalized Path ordinal
SelectingPath = { Path: GovernedPath; MatchedGlob: GovernedPath }   // F014 normalized paths
CostRollup    = { Cheap: int; Medium: int; High: int; Exhaustive: int }
```

The embedded `Gate` (`FS.GG.Governance.Gates.Model`) supplies every selected-gate field VERBATIM —
`Id: GateId`, `Domain: DomainId`, `Description: string`, `Prerequisites: GatePrerequisite list`,
`Cost: Cost`, `Timeout: TimeoutLimit`, `Owner: Owner`, `Maturity: Maturity`, `ProductCheck: bool`,
`FreshnessKey: FreshnessKey` — so the projection re-derives none of it (FR-002, FR-010). The
`FindingReport` (`FS.GG.Governance.Findings.Model`) is carried unchanged (FR-005).

**Invariant relied on (not re-established):** `RouteResult` is the output of F019's pure total
`Route.select`, so every collection is already in its documented ordinal order and the value is
order-independent (permuting upstream inputs yields an equal value). The projection therefore **adds no
ordering decision** beyond the fixed field sequence and re-sorts nothing.

## 2. Produced value

A single JSON **string** (compact UTF-8). Shape, field order, and tokens are fixed in
`contracts/route-json-document.md`. No typed intermediate document is produced (research D4).

## 3. Rendering rules (per field) and the tokens this row owns

| Upstream value | Rendered as | Token source |
|---|---|---|
| `GateId` | string | **reuse** `Gates.gateIdValue` (verbatim, never re-parsed — FR-010) |
| `FindingId` | string | **reuse** `Findings.findingIdToken` |
| `DomainId`/`Owner`/`CheckId`/`CommandId`/`SurfaceId`/`GovernedPath` | string | local newtype unwrap at use site |
| `TimeoutLimit` | number (int seconds) | local newtype unwrap |
| `Cost` | `cheap`/`medium`/`high`/`exhaustive` | **local hidden** `match` (research D3) |
| `Maturity` | `observe`/`warn`/`blockOnPr`/`blockOnShip`/`blockOnRelease` | **local hidden** `match` |
| `EnvironmentClass` | `local`/`ci`/`localOrCi`/`release` | **local hidden** `match` |
| `GatePrerequisite` (`RequiresCommand c`) | `{ "requiresCommand": "<id>" }` | local writer |
| `FindingZone` | `"governedRootUnknown"` / `{ "protectedBoundary": "<sid>" }` | **local hidden** `match` |
| `CommandId option` (in `freshnessKey`) | string or JSON `null` | local writer |

The three closed-enum token helpers and the zone/prerequisite writers are **hidden** in `RouteJson.fs`
(absent from `RouteJson.fsi`), mirroring `Kernel/Json.fs`'s `severityToken`/`stateToken`/`writeOutcome`.
Each enum `match` is **exhaustive over the closed DU** (no wildcard), so a future tier/maturity/zone
case is a compile error here rather than a silently dropped or mis-tokened field (research D3).

## 4. Determinism (FR-007, SC-002, SC-003)

- **Field order** is the single ordering decision the projection makes: the fixed `Utf8JsonWriter` call
  sequence (top-level `schemaVersion` → `selectedGates` → `findings` → `cost`, and the per-object field
  orders in §3 of the contract).
- **Collection order** is inherited from `RouteResult` (gates by `GateId`, selecting paths by path,
  findings in F017 order) — preserved verbatim, never re-sorted (re-sorting findings would violate
  FR-005).
- **No `Map` iteration**, so no key-sort step is needed (contrast `Kernel/Json.ofEffective`, which must
  ordinal-sort its map keys).
- **Compact output**: default `Utf8JsonWriter` options ⇒ no indentation ⇒ no whitespace variance.
- **No clock/host/environment input** enters the document (FR-012).

Consequence: `ofRouteResult r` is byte-identical across runs (SC-002), and two `RouteResult`s equal as
values but assembled from differently-ordered upstream inputs project identically (SC-003, inherited
from F019's permutation-invariance).

## 5. Totality (FR-008, FR-009, SC-006)

`ofRouteResult` is total: it pattern-matches only closed DUs (exhaustively) and unwraps single-case
newtypes — no partial function, no division, no parse, no I/O — so it cannot throw for any well-typed
`RouteResult`. The empty route (`SelectedGates = []`, `Findings.Findings = []`, all-zero `Cost`)
projects to `{ schemaVersion, "selectedGates": [], "findings": [], "cost": {0,0,0,0} }` — a valid
success, never an error and never a "select everything" fallback (FR-009). A findings-only route
(empty `SelectedGates`, non-empty `Findings`) projects with both sections coexisting.

## 6. Exclusions (FR-011, FR-012, SC-007)

The writer only ever emits the fields enumerated in §3 / the contract. There is no code path that reads
or writes severity, profile, mode, enforcement, a cache-eligibility verdict, a ship verdict, blockers,
warnings, an exit code, expected artifacts, raw YAML, a host/absolute path, a timestamp, or an
environment value — none of those exist on `RouteResult`/`Gate`/`FreshnessKey`/`FindingReport` to begin
with. The exclusion sweep test asserts the emitted document contains none of those tokens.
