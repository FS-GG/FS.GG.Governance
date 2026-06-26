# Phase 1 — Data Model: Effective-Evidence `evidence.json` Projection

**Feature**: `069-evidence-json-projection` | **Date**: 2026-06-26

This is the typed model behind the `evidence.json` artifact and the host. The wire grammar is in
[contracts/evidence-json.md](./contracts/evidence-json.md); this file fixes the F# shapes and invariants.

## Reused types (verbatim — NOT redefined)

| Type | Home | Used for |
|---|---|---|
| `EvidenceState` (`Pending`/`Real`/`Synthetic`/`Failed`/`Skipped`/`AutoSynthetic`) | `FS.GG.Governance.Kernel` (`Evidence.fsi`) | the declared and effective state per node |
| `Freshness` (`Fresh`/`Stale`) | `FS.GG.Governance.Kernel` (`Freshness.fsi`) | the plain per-node freshness on the report (MVP) |
| `GraphError<'id>` (`Cycle of 'id list`/`UnknownNode of 'id`/`AutoSyntheticDeclared of 'id`) | `FS.GG.Governance.Kernel` (`Evidence.fsi`) | the named graph failure (`'id = string`) |
| `EvidenceGraph<'id>`, `Evidence.build`, `Evidence.effective` | `FS.GG.Governance.Kernel` | re-running the closure at the host edge |
| `RecomputeCause` (`NoPriorEvidence`/`InputsChanged of InputCategory list`) | `FS.GG.Governance.EvidenceReuse` | the stale cause (US2) |
| `InputCategory`, `categoryToken` | `FS.GG.Governance.FreshnessKey` | naming changed-input categories (US2) |
| `MissingFact`, `missingFactToken` | `FS.GG.Governance.FreshnessResolution` | naming missing facts on unresolved freshness (US2) |
| `ProjectEvidenceReport`, `EvidenceNodeReport` | `FS.GG.Governance.Cli` (`Project.fsi`) | the host's input — the already-folded evidence world |
| `Disclosure` (`{ Rule; Justification }`) | `FS.GG.Governance.Host` (`Loop.fsi`) | optional disclosures carried through |

`EvidenceNodeReport = { Id: string; Declared: EvidenceState option; Effective: EvidenceState option;
Freshness: Freshness option; Source: string }` and `ProjectEvidenceReport = { Nodes; Dependencies:
(string*string) list; Disclosures; Failures }` are the existing shapes the host maps **from**.

## New types — owned by `FS.GG.Governance.EvidenceJson`

The projection owns the artifact's wire model (the analogue of `CacheEligibility.Model` for
`CacheEligibilityJson`). It is the only thing rendered to bytes.

### `NodeFreshness`

```fsharp
/// Per-node freshness with a no-hide cause (FR-003). `Stale`/`Unresolved` name *why*; `Unknown` is an honest
/// null for a node with no joinable freshness signal — never a guessed `Fresh`.
type NodeFreshness =
    | Fresh
    | Stale of cause: RecomputeCause          // noPriorEvidence | inputsChanged categories
    | Unresolved of missing: MissingFact list // non-empty; each named via missingFactToken
    | Unknown
```

### `EvidenceNode`

```fsharp
/// One evidence node in a well-formed graph. Declared AND effective are BOTH present (FR-002): taint surfaces
/// as the delta, never as a silent overwrite of `Declared`.
type EvidenceNode =
    { Id: string
      Declared: EvidenceState
      Effective: EvidenceState
      Freshness: NodeFreshness
      Source: string }
```

`Declared`/`Effective` are non-optional here: the host maps a well-formed node's report `option`s to concrete
states (a node always entered with a declared state; effective comes from `Evidence.effective`). A
report node with `Declared = None` is a sensing defect surfaced as a host diagnostic, not a fabricated state.

### `EvidenceContent` — the well-formed/malformed split

```fsharp
/// A graph failure means the effective-state map is NOT emitted (FR-004): the document carries the named
/// failure INSTEAD of a partial/guessed per-node map.
type EvidenceContent =
    | WellFormed of nodes: EvidenceNode list * dependencies: (string * string) list
    | Malformed of failure: GraphError<string>
```

### `EvidenceDocument`

```fsharp
/// The complete value `ofReport` renders. `schemaVersion` is stamped by the projection, not carried here.
type EvidenceDocument =
    { Content: EvidenceContent
      Disclosures: Disclosure list }   // carried through from the report; [] when none
```

### Projection surface

```fsharp
val schemaVersion: string                       // "fsgg.evidence/v1" — fixed constant
val ofReport: document: EvidenceDocument -> string   // PURE, TOTAL, deterministic
```

## Host mapping — `ProjectEvidenceReport` → `EvidenceDocument`

Performed at the `EvidenceCommand` edge (impure sensing; pure assembly), reusing cores verbatim:

1. **Build/closure (D3).** From `report.Nodes` take `(node.Id, declared)` (where `declared = node.Declared`
   defaulted/flagged) and `report.Dependencies`. Call `Kernel.Evidence.build nodes deps`.
   - `Error e` → `Content = Malformed e`. Stop — no per-node map (FR-004).
   - `Ok graph` → `let eff = Evidence.effective graph` → continue to (2).
2. **Per node (well-formed).** For each report node (sorted by `Id` in the projection, not here):
   `Declared = node.Declared`, `Effective = eff.[node.Id]`, `Source = node.Source`,
   `Freshness =` the `NodeFreshness` from (3).
3. **Freshness (D4).** MVP: map `node.Freshness` — `Some Fresh → Fresh`; `Some Stale → Unknown` **unless** a
   real `RecomputeCause` is resolved for the node (a bare `Stale` with no resolved cause maps to `Unknown`,
   **never** a guessed `Stale NoPriorEvidence` — INV-6 forbids fabricating a cause); `None → Unknown`. US2:
   where the node joins a resolved gate from `FreshnessResolution.resolve`, set `Stale cause` /
   `Unresolved missing` from the resolution + `EvidenceReuse.decide`. Never guess `Fresh`, and never guess a
   cause.
4. **Disclosures.** `Disclosures = report.Disclosures` (carried through; ordering fixed by the projection).

`report.Failures` (the `Host` I/O `Failure` list — `ArtifactUnavailable`/`ReviewDispatchFailed`/
`ReviewStoreUnavailable`) is an **operational** signal, distinct from a graph failure: it surfaces through the
host's exit code / diagnostics (FR-007, Edge Cases), not as document content. A graph failure is the
`Malformed` content; an operational failure is a non-`Success` exit. The two are never conflated.

## Invariants

- **INV-1 (both states).** In `WellFormed`, every node shows `Declared` and `Effective`; taint is the visible
  delta (FR-002). Verified by a node where `Declared = Real`, `Effective = AutoSynthetic`.
- **INV-2 (closed-set distinctness).** `Skipped` renders distinct from `Failed`/`Pending`/absent (FR-005);
  exhaustive wildcard-free token match (D6).
- **INV-3 (no partial map on failure).** `Malformed` emits the named failure and **zero** node objects
  (FR-004, SC-003).
- **INV-4 (determinism).** `ofReport` sorts nodes by `Id`, dependencies by `(dependent, dependency)`,
  disclosures by `(rule, justification)`; identical `EvidenceDocument` ⇒ byte-identical output (FR-006,
  SC-002).
- **INV-5 (empty).** `WellFormed ([], [])` renders a valid document with `"nodes": []` (FR-010), a success.
- **INV-6 (no-hide freshness).** `Stale` always carries a `RecomputeCause`; `Unresolved` always carries a
  non-empty `MissingFact list`; `Unknown` is the only causeless freshness and is an explicit null, not a
  guessed `Fresh` (FR-003).
- **INV-7 (totality/purity).** `ofReport` never throws for any well-typed `EvidenceDocument`; no clock, env,
  path, git, or I/O (FR-006/FR-007).
- **INV-8 (self-describing).** From the document alone a reader determines why any node is not effective —
  tainted (effective≠declared), stale (cause), unresolved (missing facts), skipped (declared `Skipped`), or
  graph-failure (`Malformed`) — FR-011, SC-006.
