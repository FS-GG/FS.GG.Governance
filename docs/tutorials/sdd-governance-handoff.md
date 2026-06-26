# SDD → Governance handoff: connect readiness to the Governance loop

**Audience**: an integrator connecting a scaffolded, SDD-governed product to the
Governance routing/evidence/enforcement loop. **Outcome (FR-007, SC-008)**: you can
state exactly which `readiness/<id>/governance-handoff.json` fields Governance
consumes and how each maps — matching the accepted contract row-for-row.

This tutorial uses the product you scaffolded in
[adopter-onboarding.md](./adopter-onboarding.md) as the worked subject. It is
**explanatory only** — see the scope note at the end.

## The handoff contract

`FS.GG.SDD` authors a versioned, optional, SDD-owned document —
`readiness/<id>/governance-handoff.json` (contract v1.0.0, `schemaVersion = 1`) —
that projects each work item's normalized work model, declared evidence, and
verify/ship readiness into a single document **Governance consumes**. SDD imports
no Governance code; the contract is validated against Governance's target shapes by
mapping tests on the SDD side.

The locally build-verifiable anchor for the mapping is **ADR 0002**
([`docs/decisions/0002-sdd-governance-handoff-contract.md`](../decisions/0002-sdd-governance-handoff-contract.md)),
which records Governance's accepted position.

## The readiness → Governance mapping (verbatim from ADR 0002)

| `governance-handoff.json` field / state | Governance outcome | Verified against ADR 0002 |
|---|---|---|
| `evidence.nodes[].state` ∈ `{pending, real, synthetic, failed, skipped}` | maps straight through to `Kernel.EvidenceState`; tokens identical | ✓ "States `synthetic`/`real`/`failed`/`pending` map straight through" |
| `deferred` / `accepted-deferral` (SDD) | → `skipped` (a `[-]` skip with rationale, **not** `pending`) | ✓ Decision: "Confirm `deferred → skipped`." |
| `autoSynthetic` | **invalid in a produced handoff**; Governance derives it via `Evidence.effective` (taint closure) | ✓ "SDD never emits `autoSynthetic` (computed-only)… Governance's taint closure derives [it]" |
| `stale` | underlying declared state **+** a `staleEvidence` diagnostic (Governance-owned freshness) | ✓ "`stale` maps to the underlying declared state **plus** a `staleEvidence` diagnostic" |
| `governedReferences[*]` | optional routing **enrichment**; Governance MAY ignore (F016 snapshot is primary) | ✓ "`governedReferences[*]` are **optional routing enrichment**; Governance MAY ignore" |
| `readiness.*` (`shipDisposition`, `verificationReadiness`, counts, `blockingDiagnosticIds`, `perViewState`) | **advisory declared inputs** to a Governance decision, never an enforcement verdict | ✓ "Merge-boundary readiness … are **advisory declared inputs** … never an enforcement verdict" |
| unknown `contractVersion` **major** | version-mismatch finding, never a silent misread (pin v1.x) | ✓ "A consumer that does not recognize the handoff's `contractVersion` **major** MUST report a version-mismatch finding" |

**Mapping-drift guard (SC-008).** Every row above carries an explicit
ADR-0002 citation in the rightmost column; a reviewer confirms each row against
the ADR (the per-row ✓ is the checklist). If ADR 0002's accepted mapping changes,
the corresponding row must be updated in the same change — the table is not allowed
to silently diverge from the contract it documents.

## Why `deferred → skipped` (not `pending`)

Governance's kernel `EvidenceState` tokens are exactly
`pending`/`real`/`synthetic`/`failed`/`skipped`/`autoSynthetic`. The constitution
status legend defines `skipped` as "done, skipped with a recorded rationale"
(`[-]`). An SDD deferral carries exactly such a rationale, so it is a `[-]` skip,
not a `[ ]` not-started (`pending`). Mapping a deferral to `pending` would
misrepresent a deliberate, justified postponement as un-started work and would
distort taint closure at the boundary.

## Scope: what ships here, and what does not

> **No consumer code ships in this repository (T022).** The
> `governance-handoff.json` reader/parser, the `evidence.nodes` → `Evidence.build`
> adapter, and the `governedReferences` → routing fold are **ADR 0002's queued
> Governance-side work**, tracked for a future feature — none ships here. This
> tutorial is explanatory and cross-referential only.

> **Production wiring is sibling-owned (FR-013).** Wiring the template-provider
> seam into a production `fsgg-sdd init` is owned by the sibling **`FS.GG.SDD`**
> repository, not this one.

## Cross-references

- **Local, verifiable**: ADR 0002 —
  [`docs/decisions/0002-sdd-governance-handoff-contract.md`](../decisions/0002-sdd-governance-handoff-contract.md).
- **External (cross-repo) pointer**: the sibling **`FS.GG.SDD`** repository's
  `017-governance-handoff` spec (`contracts/integration-requirements.md`). Note
  this is **not** this repo's `specs/017-*` (which is
  `017-unknown-governed-path-findings`) — it lives in the sibling repo and is not
  build-verifiable from here.
