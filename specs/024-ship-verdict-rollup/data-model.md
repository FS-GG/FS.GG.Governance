# Phase 1 Data Model: Ship Verdict Rollup (Pure Core)

**Feature**: `024-ship-verdict-rollup` | **Date**: 2026-06-21

All new types live in `FS.GG.Governance.Ship.Model` (`Model.fsi`/`Model.fs`). The rollup entry point
lives in `FS.GG.Governance.Ship.Ship` (`Ship.fsi`/`Ship.fs`). Reused types are **opened, never
redefined** (constitution Principle III; spec "reuse, don't re-derive").

## Reused types (no redefinition)

| Type | Source module | Used for |
|---|---|---|
| `RouteResult`, `SelectedGate`, `CostRollup` | `FS.GG.Governance.Route.Model` (F019) | the rollup input |
| `Gate`, `GateId`, `gateIdValue` | `FS.GG.Governance.Gates.Model` (F018) | gate identity / base-severity source |
| `FindingReport`, `UnknownGovernedPathFinding`, `FindingId`, `FindingZone`, `findingIdToken` | `FS.GG.Governance.Findings.Model` (F017) | finding identity / base-severity source |
| `Maturity`, `GovernedPath` | `FS.GG.Governance.Config.Model` (F014) | the maturity lever; finding path |
| `RunMode`, `Profile`, `Severity`, `EnforcementInput`, `EnforcementDecision`, `deriveEffectiveSeverity` | `FS.GG.Governance.Enforcement.Enforcement` (F023) | the per-item derivation, reused verbatim |

## New types

### `Verdict` (closed)

```fsharp
type Verdict =
    | Pass
    | Fail
```

The whole-change outcome. `Fail` iff at least one enforced item is effective-`Blocking` (i.e.
`Blockers` is non-empty); `Pass` otherwise (FR-002). Closed so consumers switch exhaustively.

### `ExitCodeBasis` (closed)

```fsharp
type ExitCodeBasis =
    | Clean
    | Blocked
```

A typed **basis**, not a number (FR-007). `Clean` when `Verdict = Pass`, `Blocked` when `Verdict =
Fail`. The later `fsgg ship` host edge maps this to a numeric process exit (and to the distinct
usage/input-error categories F022 defined); this pure core sets no exit code and never exits.

### `EnforcedItemId` (closed)

```fsharp
type EnforcedItemId =
    | GateItem of GateId
    | FindingItem of FindingId * GovernedPath
```

The identity of one enforced item. A gate is identified by its `GateId`; a finding by its `FindingId`
paired with its normalized `Path` (the same `FindingId` may recur on several paths, so the path
disambiguates — research D6). Closed so the audit projection can render each kind distinctly.

### `EnforcedItem` (record)

```fsharp
type EnforcedItem =
    { Id: EnforcedItemId
      Decision: EnforcementDecision }
```

One selected gate or one finding after enforcement. `Decision` is the F023 `EnforcementDecision`
returned verbatim — carrying **all six** no-hide fields: `BaseSeverity` (echoed unchanged, FR-006),
`Maturity`, `Mode`, `Profile`, `EffectiveSeverity`, and `Reason` (FR-005). No field is recomputed.

**Validation / invariants**:
- `Decision.BaseSeverity` equals the base severity the input mapping (D3/D4) assigned — never altered,
  never hidden (FR-006, SC-003).
- `Decision.Mode` / `Decision.Profile` equal the rollup's two arguments for every item.

### `ShipDecision` (record — the aggregate)

```fsharp
type ShipDecision =
    { Verdict: Verdict
      Blockers: EnforcedItem list
      Warnings: EnforcedItem list
      Passing: EnforcedItem list
      ExitCodeBasis: ExitCodeBasis }
```

The whole-change ship decision. The three item lists are the **mutually exclusive, jointly exhaustive**
partition of every enforced item (research D5):

- `Blockers` — `Decision.EffectiveSeverity = Blocking`.
- `Warnings` — `Decision.BaseSeverity = Blocking` **and** `Decision.EffectiveSeverity = Advisory`
  (base-blocking relaxed to advisory by mode/maturity/profile).
- `Passing`  — `Decision.BaseSeverity = Advisory` (effective `Advisory`; never escalated — FR-011).

**Invariants** (each is an asserted property, see quickstart):
- **Verdict consistency**: `Verdict = Fail ⟺ Blockers ≠ []`; `ExitCodeBasis = Blocked ⟺ Verdict = Fail`
  (FR-002, FR-007).
- **1:1 accounting**: `|Blockers| + |Warnings| + |Passing| = |route.SelectedGates| +
  |route.Findings.Findings|` (FR-010, SC-006). No item dropped; none double-counted (F019 already
  distinct-by-`GateId`, research D7).
- **Disjointness**: the three lists share no `EnforcedItem`.
- **Deterministic order**: within each list, items are sorted by the stable composite key
  (`"gate:" + gateIdValue id`, or `"finding:" + path + ":" + findingIdToken`), ordinal string
  comparison — gates before findings, then by id/path (FR-009, SC-004).
- **No-hide**: every item in every list carries its full `EnforcementDecision` with `BaseSeverity`
  byte-identical to the mapping's assignment (FR-005, SC-003).

## Input → enforcement-input mappings (research D3/D4)

These hidden mappings live in `Ship.fs` (absent from `Ship.fsi`):

**Gate → `EnforcementInput`** (`mode`/`profile` from the rollup args):

| `gate.Maturity` | `BaseSeverity` | `Maturity` |
|---|---|---|
| `Observe`, `Warn` | `Advisory` | (verbatim) |
| `BlockOnPr`, `BlockOnShip`, `BlockOnRelease` | `Blocking` | (verbatim) |

`EnforcedItemId = GateItem gate.Id`.

**Finding → `EnforcementInput`** (`mode`/`profile` from the rollup args):

| `finding.Zone` | `BaseSeverity` | `Maturity` |
|---|---|---|
| `GovernedRootUnknown` | `Advisory` | `Warn` |
| `ProtectedBoundaryUnknown _` | `Blocking` | `BlockOnShip` |

`EnforcedItemId = FindingItem (finding.Id, finding.Path)`.

## Out of model (deferred — FR-012)

No `audit.json` (or any) serialized document, no process exit code (only the typed `ExitCodeBasis`), no
cache-eligibility verdict, no freshness evaluation, no `policy.yml`-derived per-class dial, no
wall-clock value, no machine-absolute path, no environment-derived value (SC-007). `CostRollup` is
carried inside the input `RouteResult` but is **not** evaluated or re-projected by the rollup.
