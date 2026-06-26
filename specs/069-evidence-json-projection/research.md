# Phase 0 — Research: Effective-Evidence `evidence.json` Projection Host

**Feature**: `069-evidence-json-projection` | **Date**: 2026-06-26

This document resolves every open decision before design. Each entry is **Decision / Rationale / Alternatives
considered**. There are no remaining `NEEDS CLARIFICATION` markers.

## D1 — Shape: a new host + projection, not an edit to the inline `Cli.evidenceJson`

**Decision**: Ship a **pure packable projection leaf** `FS.GG.Governance.EvidenceJson`
(`schemaVersion = "fsgg.evidence/v1"`, `ofReport : EvidenceDocument -> string`) plus a **standalone MVU host
Exe** `FS.GG.Governance.EvidenceCommand` (`fsgg evidence`), structured exactly like `CacheEligibilityJson` +
`CacheEligibilityCommand`. Leave the existing inline `Cli.evidenceJson` and `Project.evidenceReport` untouched.

**Rationale**: The spec's primary Assumption is "a dedicated information-only Governance host… mirrors the
established `cache-eligibility` precedent rather than embedding the document into an existing command." The
effective-evidence content already exists (`Project.evidenceReport : Host.Model<ProjectFact> ->
ProjectEvidenceReport` carries per-node `Declared`/`Effective`/`Freshness`/`Source`, `Dependencies`,
`Disclosures`, `Failures`), and `Cli` already has an inline `evidenceJson`. But that inline emitter is **not a
trustworthy artifact**: it emits `"kind":"evidence"` with **no `schemaVersion`** (violates FR-001), **silently
drops `report.Failures`** and never surfaces a `GraphError` (violates FR-004), renders freshness as a bare
`Fresh`/`Stale`/`null` string with **no cause and no missing facts** (violates FR-003), and is a private string
concatenation inside the `fsgg-governance` Exe with no determinism golden and no surface baseline. Promoting it
to a first-class projection (the cache-eligibility shape) is the cleanest way to close all four gaps while
staying purely additive.

**Alternatives considered**:
- *Edit `Cli.evidenceJson` in place.* Rejected: it would change the existing `fsgg-governance evidence
  --format json` output (a behavior change), couples the artifact to the monolith, and gives it no
  determinism/surface guards. The spec explicitly wants a standalone artifact.
- *One combined `evidence` subcommand inside an existing host (route/verify).* Rejected by the spec
  (information-only standalone host, not embedded).

## D2 — Output path: `readiness/evidence.json` (flat), not `readiness/<id>/evidence.json`

**Decision**: Default output is `readiness/evidence.json` (under `--repo`, overridable with `--out`), matching
every live sibling — `route.json`, `verify.json`, `cache-eligibility.json`, `evidence-reuse.json` all write
flat under `readiness/`. The CLI **verb is `evidence`** (the parser tolerates and drops a leading `evidence`
token, exactly as the cache-eligibility parser drops `cache-eligibility`).

**Rationale**: The design doc names the eventual layout `readiness/<id>/evidence.json`, but **no existing
artifact uses a per-change `<id>` subdirectory** — `<id>` is a *work-item* identifier owned by SDD's
`work/<id>/` source layout, and SDD ↔ Governance directory integration is a separate, future concern. Inventing
a `readiness/<id>/` convention now, used by no sibling, would be a non-additive surprise and would require
sourcing/validating an `<id>` this host does not yet receive. The spec's Assumptions explicitly leave "the
exact CLI verb… locked by the plan/implementation," so the plan locks the flat path to match the established
precedent. The host writes atomically (temp file + rename), reusing the cache-eligibility write pattern.

**Alternatives considered**:
- *`readiness/<id>/evidence.json` with `<id>` = HEAD revision / route id.* Rejected for this feature: no
  sibling does it, and the `<id>` source is unspecified. Recorded as future SDD-integration scope (Out of
  Scope).

## D3 — Evidence-graph source: reuse the F12 fact pipeline + `Project.evidenceReport` verbatim; re-run `Evidence.build` at the host edge to recover the swallowed `GraphError`

**Decision**: The host obtains the declared evidence graph by reusing the existing F12 project-sensing path
verbatim — `Project.compose`/`toLoopConfig` drive the `Host` loop over the real repo (SpecKit `tasks.md` task
states `X→Real`/`-→Skipped`/`S→Synthetic`/else `Pending` + `tasks.deps.yml` edges; design-system measurements;
review-cache outcomes), yielding a `Host.Model<ProjectFact>`. The host then calls
`Project.evidenceReport host` to get the `ProjectEvidenceReport` (nodes with declared/effective/freshness/source,
dependencies, disclosures, failures). **To surface graph failures by name (FR-004), the host re-runs
`Kernel.Evidence.build` over the report's `(id, declared)` nodes + `dependencies` at its own edge** and branches
on the `Result`:
- `Error (GraphError<string>)` → emit a **graph-failure** document (the named `Cycle` / `UnknownNode` /
  `AutoSyntheticDeclared`), with **no** per-node effective map (FR-004, SC-003).
- `Ok graph` → `Kernel.Evidence.effective graph` gives the per-node effective states; emit the node list with
  declared + effective both shown (FR-001/FR-002).

**Rationale**: This composes the existing cores and the existing sensing edge **verbatim** (FR-008): no new
sensing, no new evidence representation. Critically, `Project.evidenceReport` today does
`match Evidence.build … with Ok g -> Evidence.effective g | Error _ -> Map.empty` — it **swallows** the
`GraphError`, so a malformed graph silently collapses to an empty effective map and `report.Failures` (which
carries only `Host` I/O failures, never a graph error) never names it. Re-running `Evidence.build` at the new
host edge recovers the exact error **without modifying** `Project.evidenceReport` (keeping the change additive,
FR-009) and is the honest, safe-failure behavior (Constitution VI). When the graph is well-formed, the host's
re-derived effective map equals the report's `Effective` field by construction (same `build`/`effective`), so
no truth diverges.

**Alternatives considered**:
- *Fix `Project.evidenceReport` to return the `GraphError`.* Rejected for this feature: it changes a public
  signature in the `Cli` root (non-additive; re-blesses the Cli baseline and risks the existing
  `fsgg-governance evidence` output). The host-edge re-run achieves the same honesty additively. (A future
  cleanup may fold the recovery back into the bridge — noted, not done here.)
- *Author an `work/<id>/evidence.yml` reader.* Rejected/out of scope: SDD owns authored declarations; the
  `EvidenceStateFact`/`EvidenceDependencyFact` cases exist in the `ProjectFact` union but are produced only by
  test fixtures today, and wiring a new authored source is a separate sensing feature.

## D4 — Per-node freshness *cause*: compose `FreshnessResolution`/`EvidenceReuse`; name what is missing, never guess

**Decision**: Each node's freshness in the document is a closed `NodeFreshness`:
`Fresh | Stale of RecomputeCause | Unresolved of MissingFact list | Unknown`. The MVP (US1) carries only
declared/effective state and the plain `Freshness option` already on `EvidenceNodeReport` (`Fresh`/`Stale`/
absent → `Unknown`). The US2 enrichment composes the **existing** gate-freshness pipeline:
`FreshnessSensing.senseFreshness` → `FreshnessResolution.resolve` → per gate a `ResolutionOutcome`
(`Resolved inputs` then `EvidenceReuse.decide` → `Reuse`/`Recompute of RecomputeCause`, or
`Unresolved of MissingFact list`). A node is enriched **where its identity joins a resolved gate**; a stale
node then names its `RecomputeCause` (`noPriorEvidence` or `inputsChanged` with the exact `InputCategory`
tokens), and an unresolved node names every `MissingFact` via `missingFactToken`. A node with **no** joinable
freshness signal carries `Unknown` (an honest `null`) — never a guessed `Fresh`.

**Rationale**: This reuses the freshness/reuse cause vocabulary verbatim (FR-008) and honours the no-hide rule
(FR-003): the document distinguishes stale-because-tainted (effective-state delta, from `Kernel.Evidence`) from
stale-because-recompute (the `RecomputeCause`) from unresolved (named `MissingFact`s) from skipped (the
`Skipped` declared state) — exactly US2's four causes. **Principal design risk**, resolved here by safe
default: evidence-graph node ids (`speckit:T001`, `design:…`, `review:…`) are *not* gate ids, so the node↔gate
join is partial; for any node the pipeline does not resolve, the document says `Unknown`/names the missing
facts rather than fabricating freshness. Because US2 is P2 (not the MVP) and US3 is P3, the work is staged:
US1 ships states+determinism using only the report + `Kernel.Evidence`; US3 adds the `GraphError` surfacing
(host re-run, D3); US2 adds the freshness-cause join last.

**Alternatives considered**:
- *Force a 1:1 node→gate freshness.* Rejected: there is no total mapping; forcing it would require guessing,
  which FR-003/Constitution VI forbid.
- *Collapse freshness to a single document-level flag.* Rejected by the spec Edge Cases ("freshness is per
  node, never collapsed to one flag").

## D5 — Deterministic wire model, ordering, and `schemaVersion`

**Decision**: `schemaVersion = "fsgg.evidence/v1"` is a fixed constant, stamped first in the document, never
derived from a clock/env/input (mirrors `CacheEligibilityJson.schemaVersion`). `ofReport` sorts every
collection by a stable key — nodes by `Id` (ordinal), dependencies by `(dependent, dependency)`, disclosures
by `(rule, justification)`, missing-fact and category token lists in their core-defined order — and emits a
fixed field sequence. Serialization is the shared-framework `System.Text.Json` only (no new PackageReference).

**Rationale**: FR-006/SC-002 require byte-identity for identical inputs. The only byte-producing function is
the pure `ofReport`; sorting by stable keys removes collection-order leakage, and the fixed constant +
clock/env/path abstinence removes every other source of nondeterminism — the exact discipline the existing
`CacheEligibilityJson.ofReport` already proves.

**Alternatives considered**:
- *Preserve insertion order.* Rejected: `ProjectEvidenceReport` collection order is not guaranteed stable
  across runs; an explicit sort is the determinism guarantee.

## D6 — `ofReport` is total and no-hide (exhaustive, wildcard-free)

**Decision**: Every closed case is matched without a wildcard: `EvidenceState` (`Pending`/`Real`/`Synthetic`/
`Failed`/`Skipped`/`AutoSynthetic`), `NodeFreshness`, `GraphError<string>` (`Cycle`/`UnknownNode`/
`AutoSyntheticDeclared`), `RecomputeCause`, and `MissingFact` all render through exhaustive token matches reusing
the cores' own token helpers (`missingFactToken`, `categoryToken`) where they exist. An empty node list renders
`"nodes": []` (FR-010); `Skipped` is its own token, distinct from `Failed`/`Pending` (FR-005).

**Rationale**: A wildcard would let a future `EvidenceState`/`GraphError` case silently mis-token a field; an
exhaustive match makes it a compile error instead (the `CacheEligibilityJson` precedent). Totality means
`ofReport` never throws for any well-typed `EvidenceDocument` (FR-007).

**Alternatives considered**: *Catch-all `_ -> "unknown"`.* Rejected — it is exactly the silent mis-report
Constitution VI forbids.

## D7 — Leaf dependency direction (no cycle)

**Decision**: `EvidenceJson` references only `Kernel` (+ `FreshnessResolution`/`EvidenceReuse` for cause
vocabulary) — all pure cores with no back-references — so it is a true leaf. The `EvidenceCommand` host sits on
top and references `EvidenceJson` + `Cli` + `Host` + the cores; nothing references the host. The dependency
direction is one-way into the pure cores.

**Rationale**: Projections layer on top of cores, not into them (Constitution: "heavier capabilities layer on
top, not into the core"). Keeping `EvidenceJson` off any command/host project guarantees no cycle and lets the
artifact be packed independently, exactly like `CacheEligibilityJson`.

**Alternatives considered**: *Put the projection inside `Cli`.* Rejected (D1): not standalone, not packable
as its own contract, couples to the monolith.
