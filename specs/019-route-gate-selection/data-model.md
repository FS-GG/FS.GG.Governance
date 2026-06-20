# Phase 1 Data Model: Route Gate Selection (F019)

All types are product-neutral, YAML-free pure values in `FS.GG.Governance.Route`. They reuse the
upstream typed values — the F018 `Gate`/`GateRegistry`/`GateId`, the F015 `RouteReport`/`PathRouting`/
`RoutingResult`, the F017 `FindingReport`, and the F014 `GovernedPath`/`Cost`/`DomainId` newtypes —
and redefine nothing. Authoritative signatures: [`contracts/Model.fsi`](./contracts/Model.fsi) and
[`contracts/Route.fsi`](./contracts/Route.fsi).

## Consumed types (defined elsewhere — not redefined here)

| Type | Source | Role in F019 |
|---|---|---|
| `GateRegistry` | `Gates.Model` | `{ Gates: Gate list }` — the gates selected from (never rebuilt). |
| `Gate` | `Gates.Model` | `{ Id: GateId; Domain: DomainId; Cost: Cost; … }` — embedded verbatim on each `SelectedGate`. |
| `GateId` | `Gates.Model` | the selected-gate sort key and dedup key (id equality only). |
| `RouteReport` | `Routing.Model` | only `Routings` is read; `Diagnostics` is **not** consumed (D7). |
| `PathRouting` | `Routing.Model` | `{ Path: GovernedPath; Result: RoutingResult }` — the per-path outcome. |
| `RoutingResult` | `Routing.Model` | `Routed of DomainId * GovernedPath * PrecedenceReason` selects; `UnmatchedInRoot`/`OutOfScope` select nothing. |
| `FindingReport` | `Findings.Model` | `{ Findings: … }` — carried onto the route **unchanged** (FR-005). |
| `GovernedPath`, `Cost`, `DomainId` | `Config.Model` | declared id newtypes carried on the trace; `Cost` is `Cheap\|Medium\|High\|Exhaustive`. |

Upstream guarantees this feature relies on: F018 gate ids are distinct and each `Gate.Domain`
resolves; F015 `Routed` carries the resolved winning domain + the winning glob; F017 findings are
already deterministically ordered. This feature **re-derives none of these** (FR-008/FR-010).

## Produced types (this feature)

### `SelectingPath` (record) — the route reason

| Field | Type | Source / rule |
|---|---|---|
| `Path` | `GovernedPath` | a changed path classified `Routed` to the gate's domain |
| `MatchedGlob` | `GovernedPath` | the F015 `Routed` `matchedGlob` that path won on (the "rule") |

The "why this gate" link (FR-004). Sorted by normalized `Path` ordinal under each gate (FR-007).

### `SelectedGate` (record) — one selected gate + its trace

| Field | Type | Source / rule |
|---|---|---|
| `Gate` | `Gate` (F018) | the selected registry gate, verbatim — supplies `Id`, `Domain`, `Cost`, metadata (FR-004/FR-010/FR-012) |
| `SelectingPaths` | `SelectingPath list` | every `Routed` path that reached `Gate.Domain`, deduped onto this one gate (FR-002), path-ordinal sorted (FR-007) |

A gate reached by several paths appears **once** with all selecting paths recorded. Deduplicated by
`GateId`.

### `CostRollup` (record) — the rolled-up route cost

| Field | Type | Notes |
|---|---|---|
| `Cheap` | `int` | count of **distinct** selected gates whose `Cost = Cheap` |
| `Medium` | `int` | … `Cost = Medium` |
| `High` | `int` | … `Cost = High` |
| `Exhaustive` | `int` | … `Cost = Exhaustive` |

A multiset of the closed `Cost` tiers (research D5) — **not** a summed scalar (no declared weights to
sum). Each distinct selected gate counted once (FR-006). Identity = all-zero (empty selection),
a valid success (FR-009). Order-free, hence trivially deterministic (SC-004).

### `RouteResult` (record) — the route trace (aggregate)

| Field | Type | Source / rule |
|---|---|---|
| `SelectedGates` | `SelectedGate list` | the union of selected gates, deduped by `GateId`, **sorted by `GateId` ordinal** (FR-002/FR-007) |
| `Findings` | `FindingReport` (F017) | carried **unchanged** (FR-005) |
| `Cost` | `CostRollup` | the per-tier rollup over the distinct selected gates (FR-006) |

The single value explaining what runs (`SelectedGates`), what is unclassified (`Findings`), and what
it costs (`Cost`). An empty `SelectedGates` with the all-zero `Cost` is a valid empty route (FR-009).

## Invariants (proven by tests)

| # | Invariant | Source | Test |
|---|---|---|---|
| INV-1 | Every `Routed`-to-`d` path selects exactly the gates with `Gate.Domain = d`; a gate in an unreached domain is absent. | FR-002 | SelectionTests |
| INV-2 | `UnmatchedInRoot` / `OutOfScope` select no gate; no "select everything" fallback. | FR-003 | SelectionTests |
| INV-3 | Selected set = union across `Routed` paths, deduped by `GateId` (a multi-path gate appears once with all selecting paths). | FR-002/FR-004 | TraceTests |
| INV-4 | Each `SelectedGate` names path(s), domain, matching glob(s), and declared cost; only declared id newtypes — no raw YAML/host paths/severity. | FR-004/FR-012 | TraceTests |
| INV-5 | `Findings` equals the input `FindingReport`, byte-identical (empty stays empty, a success). | FR-005 | FindingsCarryTests |
| INV-6 | `Cost` counts exactly the distinct selected gates per tier; empty selection ⇒ all-zero; identical on re-run. | FR-006 | CostRollupTests |
| INV-7 | `select` twice over identical inputs ⇒ byte-identical; with candidate paths AND registry gates permuted ⇒ unchanged. | FR-007 | DeterminismTests (FsCheck) |
| INV-8 | `select` is total — never throws over any well-typed input; empty registry / empty routings ⇒ empty successful route. | FR-008/FR-009 | DeterminismTests (FsCheck) |
| INV-9 | Selection joins on `Gate.Domain` = routed `DomainId` by id equality; the `GateId` string is never re-parsed for a domain. | FR-010 | SelectionTests |

## Out of scope (FR-011) — not modelled here

No severity, profile/mode/maturity enforcement, freshness verdict (the carried `FreshnessKey` is
never evaluated), cache-reuse decision, gate execution/ordering, ship verdict/blockers/warnings/
exit-code, or route/audit JSON / `.fsgg/gates.json` / CLI. Those consume this `RouteResult` later.
