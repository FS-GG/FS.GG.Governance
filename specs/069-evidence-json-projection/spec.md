# Feature Specification: Effective-Evidence `evidence.json` Projection Host

**Feature Branch**: `069-evidence-json-projection`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next backlog item" → the next genuinely-open roadmap row after `068-finding-rule-id`: a dedicated Governance `evidence.json` effective-evidence projection host (Phase 6 🟡, `docs/initial-implementation-plan.md` lines 804–809; design contract `docs/initial-design.md`: "Effective evidence states, taint propagation, freshness, and graph failures").

## Overview

Governance already owns the *cores* that decide effective evidence: the synthetic-taint closure
(`Kernel.Evidence` — `build`/`effective`, with `GraphError` for malformed graphs), the freshness decision
(`Kernel.Freshness`, `FreshnessKey`), per-gate freshness resolution (`FreshnessResolution`), evidence reuse
(`EvidenceReuse`), and real sensing into a run (`FreshnessSensing`). What is missing is the **one document that
makes that evidence world inspectable**: a deterministic `evidence.json` that, for a routed change, records
every evidence node's declared and effective state, how synthetic taint propagated, each node's freshness, and
any graph failure — the Governance-owned sibling of the existing `route.json` / `audit.json` / `verify.json` /
`cache-eligibility.json` projections.

This feature is **information-only and purely additive**. It introduces no new freshness, taint, reuse, or
evidence policy (it composes the existing cores verbatim), changes no existing artifact or schema, and changes
no verdict or exit-code basis. Folding an ineffective-evidence finding into a *blocking* verdict is a separate,
out-of-scope concern (Phase 7's remaining row); this projection only surfaces the truth.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inspect the effective-evidence world for a change (Priority: P1)

A developer or CI job has routed a change and wants a single, machine-readable answer to "what evidence backs
this change, and which of it is actually effective?" They run the Governance evidence projection over the repo
and read `evidence.json`: every evidence node appears with its **declared** state and its **effective** state
(after synthetic-taint closure), so nothing is hidden and no node is silently dropped.

**Why this priority**: This is the core value and the MVP. Without the effective-state document there is no way
to inspect, in one place, the evidence world that the cores already compute internally. It stands alone: it
delivers value even if freshness and graph-failure detail (Stories 2–3) are minimal.

**Independent Test**: Route a fixture change with a known evidence graph, run the projection, and assert that
`evidence.json` lists every node with its declared state and the effective state that matches the
`Kernel.Evidence.effective` closure for that graph — byte-for-byte stable across repeated runs.

**Acceptance Scenarios**:

1. **Given** a change whose evidence graph has a node declared `Real` that depends on a node declared
   `Synthetic`, **When** the projection runs, **Then** `evidence.json` shows the dependent node with declared
   state `Real` and **effective** state tainted (synthetic), naming both states so the demotion is visible.
2. **Given** a change with a node declared `Real` whose inputs are all `Real`, **When** the projection runs,
   **Then** that node appears with declared and effective state both `Real` (untainted).
3. **Given** the projection is run twice on the identical repository state, **When** the two `evidence.json`
   outputs are compared, **Then** they are byte-identical.
4. **Given** a change with no evidence nodes, **When** the projection runs, **Then** `evidence.json` is a valid,
   deterministic document with an empty node list (not an error, not a missing file).

### User Story 2 - Understand why a node is not effective (Priority: P2)

A consumer sees that a node's effective state is not `Real` and needs to know *why* — is it stale, tainted by a
synthetic dependency, failed, or an accepted deferral (skipped)? `evidence.json` answers each case
self-describingly: each node carries its freshness (`Fresh`/`Stale`, with the no-hide cause — missing facts or
the exact changed-input categories) and, where its effective state differs from its declared state, the reason
is derivable from the document alone without consulting any other artifact.

**Why this priority**: An effective-state map without *why* forces the consumer back into the cores. Surfacing
freshness and the taint/deferral distinction is what makes the document actionable, but it builds on Story 1.

**Independent Test**: Build fixtures for each non-effective cause (stale, synthetic-tainted, failed, skipped)
and assert that each node in `evidence.json` distinguishes the cause, with stale nodes naming their recompute
cause (no-prior-evidence vs. the exact changed-input category list) and unresolved freshness naming every
missing fact.

**Acceptance Scenarios**:

1. **Given** a node whose freshness inputs no longer match its recorded evidence, **When** the projection runs,
   **Then** the node is shown `Stale` with the named cause (the changed-input categories, or `noPriorEvidence`).
2. **Given** a node declared `Skipped` (an accepted deferral), **When** the projection runs, **Then** it is
   shown distinctly from a `Failed` or `Pending` node, so a deliberate deferral is not mistaken for a gap.
3. **Given** a node whose freshness cannot be resolved because a required fact is missing, **When** the
   projection runs, **Then** the document names every missing fact rather than guessing fresh or stale.

### User Story 3 - See graph failures named, never silently swallowed (Priority: P3)

When the evidence graph itself is malformed — a dependency cycle, a dependency on an unknown node, or an
illegal directly-declared auto-synthetic node — the projection must report the failure **by name** in the
document rather than emit a partial or guessed effective-state map. A malformed graph is a disclosed finding,
not a silent pass.

**Why this priority**: Safe-failure honesty (Constitution VI). It is lower priority only because well-formed
graphs are the common path, but it must ship for the document to be trustworthy.

**Independent Test**: Feed each `GraphError` kind (cycle, unknown node, auto-synthetic-declared) and assert
`evidence.json` reports the named failure and does not present a fabricated effective-state map for the broken
graph.

**Acceptance Scenarios**:

1. **Given** an evidence graph containing a dependency cycle, **When** the projection runs, **Then**
   `evidence.json` reports a named `cycle` graph failure and does not emit a guessed per-node effective state.
2. **Given** a graph whose node depends on an undeclared id, **When** the projection runs, **Then** the
   document reports a named `unknownNode` failure identifying the offending reference.
3. **Given** a graph that declares an auto-synthetic node directly, **When** the projection runs, **Then** the
   document reports the named `autoSyntheticDeclared` failure.

### Edge Cases

- **Empty evidence set**: a valid, deterministic document with an empty node list (FR-010).
- **All-fresh vs. all-stale**: both produce well-formed documents; freshness is per node, never collapsed to one
  flag.
- **A skipped node feeding a downstream node**: the downstream node's effective state reflects the closure over
  its real inputs; the skip is visible on the skipped node.
- **Mixed graph failure + otherwise-valid nodes**: a graph failure means the effective-state map is not emitted
  (the failure is reported instead) — partial guessing is forbidden.
- **Unresolved freshness for some nodes but not others**: each node names its own missing facts; resolvable
  nodes still resolve.
- **Operational failure (unreadable input, tool error)**: surfaced through the host's exit code and diagnostics,
  never as a fabricated "all effective" document.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST emit a single deterministic, versioned `evidence.json` document
  (`schemaVersion` `fsgg.evidence/v1`) that, for a routed change, lists every evidence node with both its
  **declared** evidence state and its **effective** evidence state (after the synthetic-taint closure).
- **FR-002**: Each node whose effective state differs from its declared state (synthetic-taint inheritance) MUST
  show **both** states so the demotion is visible; taint MUST never silently overwrite the declared state.
- **FR-003**: Each node MUST carry its freshness outcome (`Fresh`/`Stale`) with a no-hide cause — for stale
  nodes the recompute cause (no-prior-evidence vs. the exact changed-input categories), and for unresolved
  freshness the named missing facts.
- **FR-004**: When the evidence graph is malformed, the document MUST report the graph failure **by name** (the
  three kinds: dependency cycle, unknown-node reference, directly-declared auto-synthetic) and MUST NOT emit a
  partial or guessed per-node effective-state map for the broken graph.
- **FR-005**: An accepted deferral (`Skipped`) MUST be represented distinctly from `Failed`, `Pending`, and a
  missing node, so a deliberate deferral is not mis-reported as a gap.
- **FR-006**: The document MUST be byte-identical for identical inputs — no wall-clock, git re-sensing,
  environment, absolute path, locale, or collection-order leakage; node and failure ordering MUST be
  deterministic.
- **FR-007**: The projection MUST be information-only: emitting `evidence.json` MUST NOT change any verdict,
  exit-code basis, enforcement truth table, or any existing artifact (`route.json` / `audit.json` /
  `verify.json` and their goldens stay byte-identical). The host's own process exit code MUST reflect only
  operational outcome (success / usage error / input-unavailable / tool error), never a ship/merge verdict.
- **FR-008**: The feature MUST reuse the existing effective-evidence cores verbatim (taint closure, freshness
  decision and resolution, reuse decision, real sensing) and MUST introduce no new freshness, taint, reuse, or
  evidence representation or policy.
- **FR-009**: The feature MUST be purely additive: a new standalone artifact and host surface, bumping no
  existing schema version and altering no existing public projection signature.
- **FR-010**: With no evidence nodes the system MUST still emit a valid, deterministic document with an empty
  node list (not an error and not an absent file).
- **FR-011**: A consumer MUST be able to determine, from `evidence.json` alone, why any node is not effective
  (stale / synthetic-tainted / failed / skipped / graph-failure) without consulting another artifact or the
  cores.

### Key Entities

- **Evidence node**: a single unit of evidence for the routed change, carrying a stable id, a **declared**
  evidence state, its dependency references, and (derived) its **effective** state and freshness.
- **Evidence state**: the kernel's closed set — `Pending`, `Real`, `Synthetic`, `Failed`, `Skipped`,
  `AutoSynthetic` — appearing as both the declared and the effective value per node.
- **Synthetic-taint propagation**: the transitive closure whereby a node depending (directly or indirectly) on
  synthetic/failed evidence inherits a tainted effective state; surfaced as the declared-vs-effective delta.
- **Freshness outcome**: per node `Fresh`/`Stale`, with the no-hide cause (changed-input categories or
  no-prior-evidence) and named missing facts when unresolved.
- **Graph failure**: a malformed-graph report — dependency `cycle`, `unknownNode` reference, or
  `autoSyntheticDeclared` — emitted in place of a per-node effective map.
- **`evidence.json` document**: the deterministic, versioned (`fsgg.evidence/v1`) artifact carrying the node
  list (or the graph failure) for one routed change.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any routed change with a well-formed evidence graph, `evidence.json` represents **100%** of
  the graph's nodes, each with both declared and effective state — zero nodes dropped or collapsed.
- **SC-002**: Running the projection twice on identical repository state yields **byte-identical**
  `evidence.json` output.
- **SC-003**: **All three** graph-failure kinds (cycle, unknown node, auto-synthetic-declared) are reported by
  name in fixtures, and in **0** of those cases is a guessed per-node effective map emitted.
- **SC-004**: For every fixture, the effective-state set in `evidence.json` matches the `Kernel.Evidence`
  effective closure exactly — a node tainted by a synthetic/failed input is shown tainted, and a node with only
  real inputs is shown untainted.
- **SC-005**: Introducing the feature changes **0 bytes** of every existing `route.json` / `audit.json` /
  `verify.json` golden and changes **no** verdict or exit-code basis.
- **SC-006**: For every non-effective node across the fixtures, a reader can identify the cause (stale /
  tainted / failed / skipped / graph-failure) from `evidence.json` alone — **100%** of non-effective nodes are
  self-describing.

## Assumptions

- **Standalone host, mirroring `fsgg cache-eligibility`**: the projection is delivered as a dedicated
  information-only Governance host that senses the change from a real repository and writes a deterministic
  `evidence.json` under `readiness/`. This mirrors the established `cache-eligibility` precedent rather than
  embedding the document into an existing command. The exact CLI verb and output path are locked by the
  plan/implementation: per plan D2 the host writes the **flat** `readiness/evidence.json` (matching every live
  sibling — `route.json` / `verify.json` / `cache-eligibility.json`), and the design's per-work-item
  `readiness/<id>/evidence.json` layout is **deferred** to a future SDD-directory integration (no sibling uses
  an `<id>` subdir today, and the `<id>` source is unspecified for this host).
- **Evidence nodes are the routed change's evidence-bearing units with their declared dependency edges**, and
  each node's declared evidence state and freshness derive from the **same** sensing/resolution pipeline already
  used by `fsgg verify` / `fsgg ship` (route → select → resolve freshness → reuse/execute). No new sensing is
  introduced; the host composes the existing edge.
- **Pure deterministic projection** in the style of the sibling `*Json` contracts: the document carries
  `schemaVersion` plus the node list / graph failure and no timestamp, absolute path, or environment value.
  Source-digest/generator provenance is owned by `provenance.json` and is out of scope here.
- **Information-only / advisory**: the document surfaces the evidence world but does not itself block; blocking
  on ineffective evidence remains an enforcement concern handled by `fsgg verify` / `fsgg ship`.
- **Reuses the kernel and freshness cores verbatim**: `Kernel.Evidence` (`build`/`effective`/`GraphError`),
  `Kernel.Freshness`, `FreshnessKey`, `FreshnessResolution`, `EvidenceReuse`, and `FreshnessSensing` are
  composed unchanged; no core is re-opened.

## Out of Scope

- Folding a stale / ineffective-evidence finding into a **blocking** verdict at the Governance boundary — that is
  Phase 7's remaining row and a separate enforcement change; this feature only projects the evidence world.
- Any new freshness, taint, reuse, or evidence **policy** or representation.
- SDD-side authoring of `work/<id>/evidence.yml` — SDD owns authored declarations; Governance owns the effective
  projection.
- Source-digest / generator-version provenance and stale-view manifests (owned by `provenance.json` /
  `refresh.json`).
- Embedding the evidence document into, or changing the schema of, `route.json` / `audit.json` / `verify.json`.
