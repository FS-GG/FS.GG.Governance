# Phase 1 Data Model: Deterministic gates.json Projection (F021)

This feature introduces **no new domain types**. It consumes the F018 `GateRegistry` and emits a JSON
**string**; the only new public values are `GatesJson.ofGateRegistry` and `GatesJson.schemaVersion`
(see [contracts/GatesJson.fsi](./contracts/GatesJson.fsi)). This document records the **consumed value**,
the **emitted document** structure, the **field order**, and the **determinism** properties — the data
the projection reads and writes, not new entities.

## Consumed value — the F018 `GateRegistry` (read-only, verbatim)

From `FS.GG.Governance.Gates.Model` (referenced as-is; not modified):

- **`GateRegistry`** = `{ Gates: Gate list }` — already sorted by `GateId` ordinal by
  `Gates.buildRegistry`; re-ordering the source checks never changes it. An empty `Gates` list is a
  valid, successful registry (no declared checks).
- **`Gate`** = `{ Id: GateId; Domain: DomainId; Description: string; Prerequisites: GatePrerequisite
  list; Cost: Cost; Timeout: TimeoutLimit; Owner: Owner; Maturity: Maturity; ProductCheck: bool;
  FreshnessKey: FreshnessKey }` — carried verbatim; the projection re-derives none of it.
- **`GatePrerequisite`** = `RequiresCommand of command: CommandId` — the single MVP case (closed DU).
- **`FreshnessKey`** = `{ Check: CheckId; Domain: DomainId; Cost: Cost; Environment: EnvironmentClass;
  Command: CommandId option }` — carried **inputs** only; no cache verdict is computed.
- Renderer reused: **`Gates.gateIdValue : GateId -> string`** for the `id` field (FR-010: never
  re-parse a `GateId`).

Config newtypes unwrapped verbatim (arriving transitively via `Gates`): `DomainId`, `Owner`,
`CommandId`, `CheckId`, and `TimeoutLimit` (its `int` seconds). Closed enums tokenized by hidden helpers:
`Cost`, `Maturity`, `EnvironmentClass` (token tables in [research D3](./research.md)).

## Emitted document — structure (see [contracts/gates-json-document.md](./contracts/gates-json-document.md) for the authoritative wire contract)

```text
{ schemaVersion: string                       # "fsgg.gates/v1" (FR-013)
, gates: [                                     # registry.Gates, GateId ordinal order; [] for empty (FR-009)
    { id, domain, description, cost, timeout, owner, maturity, productCheck
    , prerequisites: [ { requiresCommand } ]   # carried order; present-and-empty when none (FR-004)
    , freshnessKey: { check, domain, cost, environment, command }  # inputs only; command = string|null (FR-014)
    } ... ] }
```

The `gates[*]` entry is the F020 `selectedGates[*]` entry **minus** `selectingPaths` (research D5); the
top-level omits route's `findings` and `cost` rollup (those are per-change, FR-011).

## Field order (part of the contract, FR-007)

| Object | Field order |
|---|---|
| top level | `schemaVersion`, `gates` |
| `gates[*]` | `id`, `domain`, `description`, `cost`, `timeout`, `owner`, `maturity`, `productCheck`, `prerequisites`, `freshnessKey` |
| `prerequisites[*]` | `requiresCommand` |
| `freshnessKey` | `check`, `domain`, `cost`, `environment`, `command` |

Order is fixed by the writer's call sequence in `GatesJson.fs` and asserted by the determinism/field-order
tests.

## Determinism & totality properties

- **Deterministic** (FR-007, SC-002): one linear walk of the already-ordered registry through a single
  `Utf8JsonWriter` with default (compact) options ⇒ identical input yields byte-for-byte identical text.
  The projection adds **no** ordering decision beyond the fixed field sequence.
- **Order-independent** (SC-003): because the gate order is the registry's `GateId` order (fixed by
  F018, not by the source-check order), two registries equal as values but built from
  differently-ordered check lists project to identical bytes.
- **Total** (FR-008, SC-006): no file/process/clock/network/git access; never throws for any well-typed
  `GateRegistry`. The empty registry → `{ "schemaVersion": "fsgg.gates/v1", "gates": [] }`, a success.
- **Carry-faithful** (FR-002/FR-004/FR-005/FR-006/FR-014): every gate's id, domain, description, cost,
  timeout, owner, maturity, product-check flag, prerequisites, and freshness-key inputs render verbatim;
  the `None` freshness command renders as explicit `null`; the empty prerequisite list renders as a
  present empty array.
- **Exclusion-faithful** (FR-011/FR-012, SC-007): no severity/profile/mode/enforcement/cache-verdict/
  per-change-selection/route-trace/ship-verdict/raw-YAML/host-path/timestamp/environment field appears.

## State transitions

None. The projection is a single pure function with no state, no effects, and no lifecycle (Principle IV
exempt case — research and plan Constitution Check).
