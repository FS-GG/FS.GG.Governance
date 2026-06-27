# Feature Specification: SDD→Governance Handoff Consumer (enforce, not just produce)

**Feature Branch**: `081-sdd-handoff-consumer`

**Created**: 2026-06-27

**Status**: Draft

**Change Classification**: **Tier 1** (contracted change — new public library surface + additive public `Loop.fsi`/`Interpreter.fsi` on three host commands + a new cross-project dependency; ADR/tutorial updated in lockstep). See plan.md "Constitution Check".

**Input**: User description: "next governance item on the project coordination board." — resolved to the **P3 Governance** roadmap item *"Ship the handoff CONSUMER (ADR-0002): enforce `governance-handoff.json`, not just produce it"* (Coordination board #1, child of the *"Governance actually fires, and the handoff is consumed"* epic).

## Overview

Today the SDD→Governance handoff is **one-directional**: `FS.GG.SDD` produces a versioned,
optional document — `readiness/<id>/governance-handoff.json` (contract `v1.0.0`,
`schemaVersion = 1`) — that projects each work item's normalized work model, declared
evidence, and verify/ship readiness. Governance has **acknowledged** this contract (local
ADR 0002) and **documented** the field-by-field mapping (tutorial
`docs/tutorials/sdd-governance-handoff.md`), but ships **no code that consumes it**. Feature
072 stated this explicitly: *"No consumer code ships in this repository (T022)"* — the
reader/parser, the `evidence.nodes → Evidence.build` adapter, and the routing fold were
deferred as ADR-0002's queued Governance-side work. **This feature is that work.**

The result is the "real gap" the Coordination board names: a scaffolded, SDD-governed
product can declare its evidence and readiness, but that declaration never reaches
Governance's routing/evidence/enforcement loop, so it never affects a verdict. The handoff is
an inert file.

This feature closes the gap by shipping the **handoff consumer**: a reader/parser that loads
and version-checks the document, an adapter that maps its declared evidence into Governance's
existing evidence model and runs the existing taint closure, and **host wiring** so the
handoff's evidence and SDD readiness actually drive `route`/`ship`/`verify` verdicts
end-to-end. Per the user's scope decision, SDD merge-boundary readiness is promoted to a
**first-class gate-registry entry** (resolving ADR-0002's open queue item #4 in favour of the
gate-registry binding rather than the prior advisory-only stance).

Governance remains the consumer side only: it imports no SDD code and changes no SDD-owned
contract. The document shape, version, and field semantics are SDD-owned; this feature reads
them against Governance's own target shapes exactly as the accepted mapping (ADR 0002) and
the tutorial already specify.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A produced handoff actually drives a Governance verdict (Priority: P1)

An integrator has a scaffolded, SDD-governed product whose `readiness/<id>/governance-handoff.json`
declares its work items' evidence (e.g. a `failed` test-evidence node) and readiness. They run
the Governance loop over the product. Governance now **reads** the handoff, **maps** its
declared evidence into the evidence model, runs taint closure, and the declared evidence
**changes the outcome**: a handoff that declares failing/blocking evidence produces a
different ship/verify verdict than one that declares everything satisfied. The handoff is no
longer inert — what SDD declares is what Governance enforces.

**Why this priority**: This is the headline value and the literal board item — "enforce, not
just produce." Without it the handoff is a file nobody reads; with it the SDD→Governance
boundary is live. Every other story is a refinement of this one.

**Independent Test**: Place a fixture handoff under `readiness/<id>/governance-handoff.json`
in a temp product, run the loop twice (one fixture declaring satisfied evidence, one declaring
failing evidence), and confirm the two runs yield materially different verdicts traceable to
the declared evidence.

**Acceptance Scenarios**:

1. **Given** a valid `v1.x` handoff declaring a `failed` evidence node for a governed work
   item, **When** Governance runs `ship`/`verify` over the product, **Then** the resulting
   verdict reflects that failure (blocking where the existing enforcement rules say a failed
   gate blocks) rather than passing as if no evidence existed.
2. **Given** the same product with the handoff declaring all evidence `real`/`satisfied`,
   **When** the loop runs, **Then** the verdict is correspondingly satisfied — i.e. the
   declared evidence is the thing that moved the outcome.
3. **Given** no handoff present (the optional contract), **When** the loop runs, **Then**
   Governance behaves exactly as it does today (the handoff is optional; absence is not an
   error and changes nothing).

---

### User Story 2 - The consumer reads and version-checks the contract safely (Priority: P1)

The consumer loads `readiness/<id>/governance-handoff.json`, validates it against the pinned
`contractVersion` 1.x, and maps each field per the accepted mapping. A handoff whose
`contractVersion` **major** is unrecognized, or that is malformed, yields a clear diagnostic
finding — never a silent misread and never a crash.

**Why this priority**: Reading the document correctly and refusing to misread it is the
foundation the enforcement in US1 stands on. A consumer that silently misreads a future
contract major would distort verdicts invisibly — the worst failure mode.

**Independent Test**: Feed the consumer (a) a well-formed `v1.x` handoff, (b) a handoff with
`contractVersion` major `2`, and (c) a malformed/garbage file; confirm (a) loads and maps,
(b) and (c) each produce a distinct, descriptive diagnostic finding and no partial/garbage
mapping is enforced.

**Acceptance Scenarios**:

1. **Given** a well-formed `v1.x` handoff, **When** the consumer loads it, **Then** every
   `evidence.nodes[].state` ∈ `{pending, real, synthetic, failed, skipped}` maps straight
   through to the matching evidence-model state.
2. **Given** an SDD `deferred` / `accepted-deferral` evidence result, **When** mapped,
   **Then** it maps to `skipped` (a recorded-rationale skip), **not** `pending`.
3. **Given** a handoff that declares `autoSynthetic` as a node state, **When** loaded, **Then**
   the consumer rejects it with a finding (it is invalid in a produced handoff; the
   evidence-model derives `autoSynthetic` only via taint closure).
4. **Given** a node marked `stale`, **When** mapped, **Then** it maps to its underlying
   declared state **plus** a staleness diagnostic (freshness is Governance-owned).
5. **Given** a handoff whose `contractVersion` **major** is unrecognized, **When** loaded,
   **Then** the consumer emits a version-mismatch finding and does not enforce a mapped result.

---

### User Story 3 - SDD merge-boundary readiness participates as a first-class gate (Priority: P2)

The handoff's `readiness.*` block (`shipDisposition`, `verificationReadiness`,
`blockingDiagnosticIds`, counts, `perViewState`) is surfaced into the Governance loop as a
**typed gate-registry entry**, so SDD's declared readiness participates as a first-class gate
in selection, severity, and the verdict roll-up — consistently with how every other gate is
treated — rather than being an inert advisory note.

**Why this priority**: This resolves ADR-0002's explicitly-open queue item #4 (gate-registry
entry vs merge-fence) in favour of the gate binding. It is P2 because US1 already delivers
enforcement of the declared *evidence*; this extends enforcement to declared *readiness*.

**Independent Test**: Provide handoffs whose readiness block declares a non-shippable
disposition and a blocking diagnostic, and confirm the readiness gate appears in the gate set,
is selected and severity-resolved like other gates, and contributes to the verdict per the
existing enforcement rules.

**Acceptance Scenarios**:

1. **Given** a handoff whose `shipDisposition`/`blockingDiagnosticIds` declare a blocking
   readiness state, **When** the loop runs, **Then** a readiness gate is present, selected,
   and contributes its declared severity to the verdict.
2. **Given** a handoff declaring a clean, shippable readiness state, **When** the loop runs,
   **Then** the readiness gate is present and non-blocking.

---

### Edge Cases

- **No handoff at all**: the contract is optional — absence is the common case and MUST be a
  no-op, not a finding.
- **Handoff present but references work items / paths not in Governance's own snapshot facts**:
  the consumer must reconcile without crashing (Governance's F016 snapshot remains the primary
  routing source; the handoff is consumed alongside it).
- **`governedReferences[*]` present**: these are optional routing *enrichment*; the consumer
  MAY use them but Governance's snapshot facts remain primary — absence or presence must not
  change correctness.
- **Multiple handoffs** (more than one `readiness/<id>/...` directory): the consumer must have
  a defined, deterministic behaviour across all present handoffs (load all, in a stable order).
- **Empty `evidence.nodes`** or a handoff with readiness but no evidence (or vice-versa): each
  block is consumed independently; one being empty does not invalidate the other.
- **Conflicting declarations** between the handoff and Governance's own sensed facts: a defined
  precedence/diagnostic, never a silent overwrite of sensed truth.
- **Malformed JSON / missing required contract fields**: a descriptive diagnostic finding, not
  a crash or a partial enforce.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST locate and read `readiness/<id>/governance-handoff.json`
  artifacts within a governed product as an **optional** input — absence MUST be a silent
  no-op that leaves all existing behaviour unchanged.
- **FR-002**: The system MUST parse a present handoff and validate it against the pinned
  contract major (`1.x`); an unrecognized `contractVersion` **major** MUST yield a
  version-mismatch finding and MUST NOT enforce a mapped result (no silent misread).
- **FR-003**: The system MUST map each `evidence.nodes[].state` ∈
  `{pending, real, synthetic, failed, skipped}` straight through to the corresponding
  evidence-model state, token-for-token.
- **FR-004**: The system MUST map an SDD `deferred` / `accepted-deferral` evidence result to
  `skipped` (a recorded-rationale skip), never to `pending`.
- **FR-005**: The system MUST reject a handoff that declares `autoSynthetic` as a node state
  with a finding; `autoSynthetic` is derived only by the evidence model's taint closure, never
  accepted as a declared input.
- **FR-006**: The system MUST map a `stale` node to its underlying declared state **plus** a
  staleness diagnostic (freshness is Governance-owned; the handoff never carries a freshness
  verdict).
- **FR-007**: The system MUST feed the mapped declared evidence into the existing evidence
  model and run the existing taint closure, so handoff-declared evidence participates in the
  evidence graph exactly as evidence from any other adapter does.
- **FR-008**: The system MUST wire the consumer into the existing `route`/`ship`/`verify`
  loop so that handoff-declared evidence demonstrably affects the produced verdict end-to-end
  (the "enforce" requirement) — a failing/blocking declaration MUST be capable of changing the
  outcome relative to a satisfied declaration.
- **FR-009**: The system MUST surface the handoff's `readiness.*` block
  (`shipDisposition`, `verificationReadiness`, `blockingDiagnosticIds`, counts, `perViewState`)
  as a **typed gate-registry entry** that participates in gate selection, severity resolution,
  and verdict roll-up like any other gate.
- **FR-010**: The system MUST treat `governedReferences[*]` as optional routing *enrichment*
  only; Governance's own snapshot facts remain the primary routing source and correctness MUST
  NOT depend on the presence of `governedReferences`.
- **FR-011**: The system MUST produce a clear, descriptive diagnostic finding for a malformed
  handoff or one missing required contract fields, without crashing and without enforcing a
  partial mapping. ("Finding" here means a surfaced, descriptive **diagnostic** carried alongside
  a blocking handoff-integrity gate — *not* an F017 path-scoped `FindingId`; the `Findings` model
  surface stays frozen. See research D5.)
- **FR-012**: The system MUST behave deterministically when zero, one, or several handoffs are
  present (stable ordering, defined aggregation).
- **FR-013**: The consumer MUST import no SDD code and MUST NOT change any SDD-owned contract
  or document shape; it consumes the contract against Governance's own target shapes only.
- **FR-014**: The mapping the consumer implements MUST match ADR 0002 row-for-row; if the
  accepted mapping changes, the consumer and ADR 0002 MUST be updated together (no silent
  divergence between code and the documented contract).
- **FR-015**: The feature MUST record the decision that SDD merge-boundary readiness binds to a
  gate-registry entry (resolving ADR-0002 queue item #4), so the chosen disposition is
  traceable.

### Key Entities *(include if feature involves data)*

- **Governance handoff document**: the SDD-owned `readiness/<id>/governance-handoff.json`
  (`contractVersion`, `schemaVersion`, an `evidence` block of declared nodes + dependencies, a
  `readiness` block of merge-boundary disposition, and optional `governedReferences`). Read-only
  to Governance.
- **Declared evidence node**: one entry in `evidence.nodes` — an identity, a declared state
  (`pending`/`real`/`synthetic`/`failed`/`skipped`, plus SDD's `deferred`/`stale` results), and
  a rationale. Maps into the evidence model.
- **Mapped evidence graph**: the result of feeding declared nodes + dependencies into the
  existing evidence model and running taint closure — the bridge from declaration to
  enforcement.
- **Readiness gate**: the typed gate-registry entry derived from the handoff's `readiness.*`
  block, participating in selection/severity/roll-up like other gates.
- **Version-mismatch / mapping-rejection finding**: the diagnostic emitted for an unrecognized
  contract major, an `autoSynthetic` declaration, or a malformed document.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given two otherwise-identical products differing only in their handoff's declared
  evidence (one satisfied, one failing/blocking), the Governance loop produces two materially
  different verdicts attributable to the declared evidence — i.e. the handoff demonstrably
  drives the outcome (the "enforce, not produce" proof).
- **SC-002**: 100% of the ADR-0002 mapping rows are exercised by tests — straight-through
  states, `deferred → skipped`, `autoSynthetic` rejected, `stale` → state + diagnostic,
  `governedReferences` optional, `readiness.*` as a gate, unknown major → version-mismatch —
  with each test traceable to its ADR-0002 row.
- **SC-003**: A product with **no** handoff produces byte-identical loop output to today (the
  optional contract is a true no-op when absent).
- **SC-004**: An unrecognized `contractVersion` major and a malformed handoff each produce a
  distinct, descriptive finding and never a crash or a silently-mapped/enforced result, in
  100% of injected-fault cases.
- **SC-005**: An SDD-declared blocking readiness state surfaces as a selected gate that
  contributes to the verdict; a clean readiness state surfaces as a present, non-blocking gate.
- **SC-006**: The consumer references no SDD source and the SDD-owned contract files are
  unchanged (verifiable: no SDD dependency added, no contract document edited).

## Assumptions

- **Contract shape is SDD-owned and stable at `v1.0.0` / `schemaVersion = 1`.** The exact JSON
  field names/structure are taken from the accepted contract (ADR 0002 + the handoff tutorial);
  the consumer pins major `1.x` and ignores unknown additive (minor) fields.
- **The accepted mapping (ADR 0002) is authoritative** and is reproduced row-for-row by the
  tutorial; this feature implements exactly that mapping and does not renegotiate it.
- **The consumer lands as a new sibling adapter** alongside the existing `Adapters.Spi` (F009),
  `Adapters.SpecKit` (F010), and `Adapters.DesignSystem` (F011) — i.e. it conforms to the
  established adapter SPI rather than introducing a new integration mechanism. (Implementation
  detail; the binding requirement is FR-007/FR-013, not the project name.)
- **Host wiring targets the existing `route`/`ship`/`verify` commands and their MVU loop**;
  this feature adds no brand-new top-level command — the handoff becomes a recognized evidence
  source / gate within the existing loop.
- **SDD merge-boundary readiness binds to a gate-registry entry (F018)** per the user's scope
  decision — this resolves ADR-0002's open queue item #4 and deliberately goes beyond the prior
  advisory-only stance; it is recorded as a decision (FR-015).
- **Governance's F016 snapshot facts remain the primary routing source**; the handoff is
  consumed alongside them, and `governedReferences` enrichment is optional.
- **No cross-repo contract change is required**: this is consumer-side work against an existing,
  SDD-owned, optional contract; the registry `governance-handoff@1` entry is unchanged.
- **Production wiring of the seam into `fsgg-sdd init` remains sibling-owned** (`FS.GG.SDD`);
  this feature delivers the Governance-side consumer and its host wiring only.
