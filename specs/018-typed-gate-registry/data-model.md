# Phase 1 Data Model: Typed Gate Registry (F018)

All types are product-neutral, YAML-free pure values in `FS.GG.Governance.Gates`. They reuse the F014
newtypes (`DomainId`, `Owner`, `Cost`, `Maturity`, `TimeoutLimit`, `CommandId`, `EnvironmentClass`,
`CheckId`) and consume the F014 `TypedFacts`; nothing is redefined. Authoritative signatures:
[`contracts/Model.fsi`](./contracts/Model.fsi) and [`contracts/Gates.fsi`](./contracts/Gates.fsi).

## Consumed types (defined elsewhere — not redefined here)

| Type | Source | Role in F018 |
|---|---|---|
| `Check` | `Config.Model` | `{ Id; Domain; Command; Owner; Cost; Environment; Maturity }` — the gate's source (one gate per check) |
| `CommandSpec` | `Config.Model` | `{ Id; Command; Timeout; Environment }` — read for `Timeout` of a check's referenced command |
| `CapabilityFacts` | `Config.Model` | only `Checks` is read |
| `ToolingFacts` | `Config.Model` | only `Commands` is read (for timeouts); `TypedFacts.Tooling` is `option` — `None` (no `tooling.yml`) ⇒ empty command index ⇒ every timeout is `defaultTimeout` |
| `TypedFacts` | `Config.Model` | `{ Project; Policy: PolicyFacts option; Capabilities; Tooling: ToolingFacts option }` — `Capabilities.Checks` + (when `Tooling = Some`) `Tooling.Commands` consumed; `Policy`/`Tooling = None` are valid |
| `DomainId`, `Owner`, `Cost`, `Maturity`, `TimeoutLimit`, `CommandId`, `EnvironmentClass`, `CheckId` | `Config.Model` | carried verbatim on the gate |

F014 guarantees over `Valid TypedFacts` this feature relies on (research D4): check ids are unique
catalog-wide; `Check.Domain` and `Check.Command` (when present) resolve. No FS.GG.Routing type is
consumed — gate *selection* by route is a later row. **`TypedFacts.Tooling` is optional**: when `None`
(no `tooling.yml`) the command-timeout index is empty and every gate takes `defaultTimeout` — a check
may carry `Command = Some c` even with `Tooling = None`, and its timeout then falls back to
`defaultTimeout`. The projection unwraps with `facts.Tooling |> Option.map (fun t -> t.Commands) |>
Option.defaultValue []`, never assuming `Some`.

## Produced types (this feature)

### `GateId` (newtype)

`GateId of string`, value `"<domainText>:<checkIdText>"`. Injective over distinct checks (FR-003/
FR-005). Rendered by `gateIdValue` (total). The stable id route/evidence/audit JSON key on.

### `GatePrerequisite` (closed DU)

| Case | Carries | Meaning |
|---|---|---|
| `RequiresCommand of CommandId` | the declared command id | the gate cannot run until that command is available |

Closed. The MVP's only prerequisite source is `Check.Command`; gate-to-gate prerequisites are the
documented Phase-10 extension point and produce no case here (research D5).

### `FreshnessKey` (record)

| Field | Type | Notes |
|---|---|---|
| `Check` | `CheckId` | declared check identity |
| `Domain` | `DomainId` | owning domain |
| `Cost` | `Cost` | declared cost class |
| `Environment` | `EnvironmentClass` | declared environment class |
| `Command` | `CommandId option` | the referenced command, if any |

The declared inputs a later freshness/cache step will hash (FR-009). **Carried, never evaluated** —
no clock, no instants, no cache (research D8). Phase 11 extends it with hashes / base-head.

### `Gate` (record)

| Field | Type | Source / rule |
|---|---|---|
| `Id` | `GateId` | `"<domain>:<checkId>"` (FR-003) |
| `Domain` | `DomainId` | `Check.Domain` |
| `Description` | `string` | fixed deterministic format `"Capability check '<checkId>' in domain '<domain>'"`, declared ids only; no raw YAML (FR-004) |
| `Prerequisites` | `GatePrerequisite list` | `[RequiresCommand c]` if `Check.Command = Some c`, else `[]` (D5) |
| `Cost` | `Cost` | `Check.Cost` |
| `Timeout` | `TimeoutLimit` | referenced command's `Timeout` when found in the command index, else `defaultTimeout` — including when `Tooling = None` or the check is command-less (D9, FR-010) |
| `Owner` | `Owner` | `Check.Owner` |
| `Maturity` | `Maturity` | `Check.Maturity`, verbatim (no enforcement — Phase 5) |
| `ProductCheck` | `bool` | `Check.Environment = Release` (MVP heuristic, D6) |
| `FreshnessKey` | `FreshnessKey` | declared identity inputs (D8) |

**Invariants** (by construction, not by a validator):
- Exactly one gate per declared `Check`; `GateId` injective ⇒ no collision/merge/drop (FR-005).
- Every `RequiresCommand` references a declared command (F014-guaranteed; preserved) (FR-006).
- `Timeout` is always bounded (never zero/unbounded) (FR-010, SC-005).
- No raw YAML, host path, timestamp, or non-id product vocabulary in any field (FR-004, SC-004).

### `GateRegistry` (record)

| Field | Type | Notes |
|---|---|---|
| `Gates` | `Gate list` | sorted by `gateIdValue` ordinal; possibly empty (FR-014) |

**No diagnostic channel and no failure mode** (research D4): assembly is total over `Valid TypedFacts`
because F014 already proved the facts consistent and the registry preserves that by construction. An
empty `Gates` is a valid success (no declared checks), distinct from any error (FR-014).

## State transitions

None. Every type is an immutable value; `buildRegistry` is a pure total function from `TypedFacts` to
`GateRegistry`. No lifecycle, no mutation, no effects, no MVU (research D2).

## Determinism summary (FR-011 / FR-012 / SC-003 / SC-006)

| Collection | Sort key |
|---|---|
| `GateRegistry.Gates` | `String.CompareOrdinal (gateIdValue Id)` |
| `Gate.Prerequisites` | at most one element in the MVP; ordinal-stable if extended |
| dependency-respecting order (US5) | MVP: the `GateId` ordinal sort (no gate-to-gate edges); Phase-10 extension point: topological order placing each gate after its dependencies |

Re-ordering the declared checks or commands leaves every output byte-identical.
