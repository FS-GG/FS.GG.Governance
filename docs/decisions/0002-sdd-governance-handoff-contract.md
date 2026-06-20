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
- Merge-boundary readiness (`shipDisposition`, `verificationReadiness`,
  `blockingDiagnosticIds`, counts, `perViewState`) are **advisory declared inputs**
  to a Governance decision, never an enforcement verdict. Accepted — whether SDD
  readiness becomes an F018 gate-registry entry or an F010 merge-fence condition is
  a Governance-side decision (see queued work below).

## Governance-side consumer work (queued — does not block SDD)

Tracked for a future Governance feature; none blocks the SDD handoff feature:

1. A reader/parser for `readiness/<id>/governance-handoff.json` (an F008-style
   `ReadArtifact` + parse, parallel to F014's `.fsgg` loader), pinned to
   `contractVersion` 1.x. A consumer that does not recognize the handoff's
   `contractVersion` **major** MUST report a version-mismatch finding rather than
   misread the document (Constitution VI/VIII).
2. An SDD-native adapter mapping `evidence.nodes` + `dependencies` into
   `Evidence.build` and running `Evidence.effective` (parallel to the F10 SpecKit
   adapter over `TaskDependsOn`).
3. Optional: fold `governedReferences` into `Routing.route` inputs, or ignore in
   favour of F016 snapshot facts.
4. A decision on whether SDD merge-boundary readiness becomes a gate-registry
   entry (F018) or a merge-fence condition (F010).

## Versioning posture

Governance pins **contract v1.x** and ignores unknown additive fields (SDD bumps
**minor** for additive changes). A mapping/shape change that alters meaning is a
**major** bump + `schemaVersion` change + a migration note in both repos; on a
major mismatch Governance reports a version-mismatch finding, never a silent
misread.
