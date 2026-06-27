# ADR 0002 — Acknowledge the SDD→Governance handoff contract v1.0.0 and confirm the evidence-state mapping

**Status**: Accepted · **Date**: 2026-06-20 · **Feature**: cross-repo coordination (consumer side)

**Resolves**: the open coordination point raised by `FS.GG.SDD` spec
`017-governance-handoff` (`contracts/integration-requirements.md`): *"confirm
`deferred → skipped` (vs `pending`)."* and records Governance's position on the
SDD→Governance handoff contract as a whole.

## Context

`FS.GG.SDD` has authored a versioned, SDD-owned, optional contract —
`readiness/<id>/governance-handoff.json` (contract v1.0.0, `schemaVersion = 1`) —
that projects each work item's normalized work model, declared evidence, and
verify/ship readiness into a single document **Governance consumes**. SDD imports
no Governance code; the contract is validated against Governance's *target* shapes
(`Kernel.Evidence`, F015 routing, F018 gates, F014 config) by inspection and
mapping tests.

The contract was unblocked by Governance shipping its consumer surface: F005
evidence model, F014 typed facts, F015 routing, F016 snapshot, F017 findings, F018
gate registry, F019 route selection, F020 route.json, and (now) F021 gates.json.

SDD left one mapping row for Governance to confirm: SDD's evidence result
`deferred` / `accepted-deferral` maps to the kernel `EvidenceState` token
`skipped`. SDD asked whether Governance prefers `pending` instead.

## Decision

**Confirm `deferred → skipped`.** No `contractVersion` bump is needed.

Rationale: Governance's kernel `EvidenceState` tokens are exactly
`pending`/`real`/`synthetic`/`failed`/`skipped`/`autoSynthetic`
(`Kernel/Json.fsi`). The kernel and the constitution status legend define
`skipped` as **"done, skipped with a recorded rationale"** (`[-]`). An SDD
deferral carries exactly such a rationale (SDD emits `rationale` alongside the
entry), so it is a `[-]` skip, not a `[ ]` not-started (`pending`). Mapping a
deferral to `pending` would misrepresent a deliberate, justified postponement as
un-started work and would distort taint closure at the boundary.

All other mappings in the contract match Governance's tokens verbatim and need no
change:

- States `synthetic`/`real`/`failed`/`pending` map straight through; SDD never
  emits `autoSynthetic` (computed-only — `Evidence.build` rejects a declared
  `autoSynthetic`), which Governance's taint closure derives via
  `Evidence.effective`. Correct: the ownership boundary holds.
- `stale` maps to the underlying declared state **plus** a `staleEvidence`
  diagnostic — staleness is Governance-owned freshness (`Kernel.Freshness`), so
  SDD declaring the base state + a diagnostic (never a freshness verdict) is the
  right split.
- `governedReferences[*]` are **optional routing enrichment**; Governance MAY
  ignore them and route from its own F016 snapshot facts. Accepted — they are
  cheap work-item→path provenance, not the primary routing source.
  **Promoted by F082** (`082-route-governed-refs`, queue item #3 below): the
  declared paths are now merged into the `Routing.route` candidate set (in
  addition to, never instead of, the F016 snapshot facts), so they *can* drive
  domain-gate selection. Still a no-op when absent/empty, so this changes no
  existing output. No `contractVersion` bump — only Governance's consumption
  posture changed.
- Merge-boundary readiness (`shipDisposition`, `verificationReadiness`,
  `blockingDiagnosticIds`, counts, `perViewState`) becomes a **first-class
  gate-registry entry** (F018) that participates in selection, severity
  resolution, and roll-up like any other gate — blocking when the disposition is
  non-shippable **or** `blockingDiagnosticIds` is non-empty, advisory otherwise.
  **Superseded in part by F081** (`081-sdd-handoff-consumer`, queue item #4 below):
  this row originally read "advisory declared inputs … never an enforcement
  verdict" with the gate-vs-merge-fence choice left open; F081 resolves it in
  favour of the **gate-registry binding** (FR-009/FR-015). No `contractVersion`
  bump — only Governance's *consumption* posture changed; the document shape and
  the `governance-handoff@1` registry entry are unchanged.

## Governance-side consumer work (queued — does not block SDD)

Tracked for a future Governance feature; none blocks the SDD handoff feature.
**Items #1, #2, and #4 are RESOLVED by F081** (`081-sdd-handoff-consumer`), which
ships the consumer (`FS.GG.Governance.Adapters.SddHandoff`):

1. **✅ Resolved (F081).** A reader/parser for
   `readiness/<id>/governance-handoff.json` (`Reader.parse`), pinned to
   `contractVersion` major 1.x — an unrecognized major yields a version-mismatch
   diagnostic rather than a silent misread (Constitution VI/VIII).
2. **✅ Resolved (F081).** A pure adapter (`Mapping`) maps `evidence.nodes` +
   `dependencies` into `Evidence.build` and runs `Evidence.effective` for the
   taint closure (parallel to the F10 SpecKit adapter over `TaskDependsOn`).
3. **✅ Resolved (F082, `082-route-governed-refs`).** `governedReferences` are
   now **first-class routing candidates**: `Consumer.candidatePaths` projects the
   declared paths of every consumable document, and each verdict host
   (`route`/`ship`/`verify`) merges them with the sensed change set
   (`sensed @ declared |> List.distinct`) BEFORE `Routing.route`, so the surface a
   work item declares it governs selects the domain gates that own it — even when
   the sensed diff is empty. The merge is de-duplicated (a path in both sources is
   routed once) and a no-op when no consumable document declares any
   `governedReferences` (absent / empty / bad ⇒ `candidatePaths = []` ⇒
   byte-identical output). A bad document still contributes zero candidates while
   its blocking integrity gate is produced by the unchanged `consume` fold. F081's
   `SelectingPath` enrichment on the handoff's OWN gates is retained unchanged;
   F082 adds the *domain-gate* selection on top of it.
4. **✅ Resolved (F081, FR-009/FR-015).** SDD merge-boundary readiness becomes a
   **first-class gate-registry entry** (F018), NOT an F010 merge-fence condition —
   it participates in selection, severity resolution, and roll-up like any other
   gate (the Decision row above is updated to match).

## Versioning posture

Governance pins **contract v1.x** and ignores unknown additive fields (SDD bumps
**minor** for additive changes). A mapping/shape change that alters meaning is a
**major** bump + `schemaVersion` change + a migration note in both repos; on a
major mismatch Governance reports a version-mismatch finding, never a silent
misread.
