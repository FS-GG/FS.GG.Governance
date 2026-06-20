# Phase 1 Data Model: Unknown Governed Path Findings (F017)

All types are product-neutral, YAML-free pure values in `FS.GG.Governance.Findings`. They reuse
the F014 newtypes (`GovernedPath`, `SurfaceId`) and consume the F015 `RouteReport`; nothing is
redefined. Authoritative signatures: [`contracts/Model.fsi`](./contracts/Model.fsi) and
[`contracts/Findings.fsi`](./contracts/Findings.fsi). Decision contract:
[`contracts/precedence.md`](./contracts/precedence.md).

## Consumed types (defined elsewhere — not redefined here)

| Type | Source | Role in F017 |
|---|---|---|
| `GovernedPath` | `Config.Model` | the normalized path of a candidate / surface declaration |
| `SurfaceId` | `Config.Model` | the escalating protected surface's identity, carried on the zone |
| `SurfaceClass` | `Config.Model` | only `Routine` (suppress) and `ProtectedSurface` (escalate) act |
| `Surface` | `Config.Model` | `{ Id; Class; Paths; Owner; Maturity }` — read for `Id`/`Class`/`Paths` |
| `TypedFacts` | `Config.Model` | only `Capabilities.Surfaces` is read |
| `RoutingResult` | `Routing.Model` | `Routed` / `UnmatchedInRoot` / `OutOfScope` — selects candidates |
| `PathRouting` | `Routing.Model` | `{ Path; Result }` — the per-path outcome consumed |
| `RouteReport` | `Routing.Model` | `{ Routings; Diagnostics }` — `Routings` consumed, `Diagnostics` ignored |

## Produced types (this feature)

### `FindingId` (closed DU)

| Case | Token | Meaning |
|---|---|---|
| `UnknownGovernedPath` | `unknownGovernedPath` | ordinary in-root unknown |
| `UnknownProtectedBoundaryPath` | `unknownProtectedBoundaryPath` | unknown on a protected boundary (escalated) |

Closed so tests assert exactly one fixture per id. Rendered by `findingIdToken` (total).

### `FindingZone` (closed DU)

| Case | Carries | Meaning |
|---|---|---|
| `GovernedRootUnknown` | — | no declared surface escalated the path |
| `ProtectedBoundaryUnknown of surface: SurfaceId` | the escalating `SurfaceId` | unknown on a declared protected boundary |

The id **and** the zone both distinguish the protected flavor (FR-006 allows either; F017 provides
both — the id for cheap switching, the zone for the surface identity). For multiple matching
protected surfaces, `surface` is the ordinal-first `SurfaceId` (precedence.md).

### `UnknownGovernedPathFinding` (record)

| Field | Type | Notes |
|---|---|---|
| `Id` | `FindingId` | stable diagnostic id |
| `Path` | `GovernedPath` | the offending normalized path (FR-014) |
| `Zone` | `FindingZone` | which region triggered it (FR-006) |
| `Message` | `string` | names the path + ≥1 remediation; protected findings name the surface(s) (FR-008, SC-006) |

**Validation / invariants** (enforced by construction, not by a validator):
- Produced **only** for a path whose `RoutingResult` is `UnmatchedInRoot` and that is not
  routine-suppressed (FR-002/FR-003/FR-004/FR-005).
- `Id = UnknownProtectedBoundaryPath` **iff** `Zone = ProtectedBoundaryUnknown _` (the escalated
  flavor pairs the id and the zone; the ordinary flavor pairs `UnknownGovernedPath` with
  `GovernedRootUnknown`).
- At most one finding per distinct `Path` (dedup, FR-010).
- No raw YAML, host path, timestamp, or non-id product vocabulary in any field (FR-008, SC-006).

### `FindingReport` (record)

| Field | Type | Notes |
|---|---|---|
| `Findings` | `UnknownGovernedPathFinding list` | sorted by (`Path` ordinal, then id token); possibly empty (FR-012) |

An empty `Findings` is a valid success (no unclassified in-root paths), distinct from any error —
this feature has no failure mode of its own (FR-012).

## State transitions

None. Every type is an immutable value; `findUnknownGovernedPaths` is a pure total function from
`(TypedFacts, RouteReport)` to `FindingReport`. No lifecycle, no mutation, no effects.

## Determinism summary (FR-009 / SC-004)

| Collection | Sort key |
|---|---|
| `FindingReport.Findings` | `String.CompareOrdinal Path` then `String.CompareOrdinal (findingIdToken Id)` |
| protected-surface tiebreak (for the zone's `SurfaceId`) | ordinal-first matching `SurfaceId` |
| dedup of candidate routings | group by normalized path, keep one routing per path (identical by construction — routing is a pure function of the path; precedence.md §"Deduplication") |

Re-ordering the candidate paths or the authored surfaces leaves every output byte-identical.
