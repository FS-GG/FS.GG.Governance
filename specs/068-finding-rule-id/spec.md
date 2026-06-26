# Feature Specification: Per-Finding Rule Identity

**Feature Branch**: `068-finding-rule-id`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next backlog item" — resolved (after reconciling the roadmap) to the
last genuinely-open Phase 5 row: *"Emit every finding with **rule id**, verdict, base severity, mode,
profile, maturity, effective severity, and reason"* and its sibling *"Ensure profiles never hide
underlying verdicts, alter rule hashes, or remove findings from JSON."* The roadmap note records the gap:
**"per-finding rule id is still un-modeled upstream, so it is not yet carried … the rule-hash guarantee
follows once rule-id is modeled upstream."**

## Overview

Today every enforced finding is emitted with its verdict, base severity, run mode, profile, maturity,
effective severity, and a lever-naming reason — but **not** the identity of the rule that produced it.
A consumer reading `audit.json` / `verify.json` / `route.json` can see *that* a finding blocked and *how*
the enforcement levers were applied, but cannot answer **"which rule said so?"** without guessing from the
message text. The decision record (`EnforcementDecision`) has six fields and no rule reference; the
unknown-path finding record carries a finding *kind*, not a stable per-rule id.

This feature gives every emitted finding a **stable, deterministic rule id** that names the rule (or
typed gate) responsible for it, threads that id from the point a finding is produced through enforcement
into the deterministic JSON projections, and uses it to anchor the long-standing **"profiles never alter
rule hashes"** integrity guarantee: a finding's rule id (and the rule hash it maps to) is identical
regardless of which profile or mode evaluated it.

The change is **purely additive** to the observable JSON contracts: the rule id is a new field on each
emitted finding; existing fields, ordering, verdicts, exit codes, and schema versions are unchanged, and
output stays byte-identical for any input that produced no findings.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Each emitted finding names its originating rule (Priority: P1)

A reviewer (human or CI consumer) reads a governance report (`audit.json`, `verify.json`, or `route.json`)
and, for every finding, can see a stable rule id identifying exactly which rule or typed gate raised it —
alongside the verdict, base/effective severity, mode, profile, maturity, and reason already present.

**Why this priority**: This is the core capability and the MVP. Without a per-finding rule id, none of the
downstream value (grouping, suppression auditing, rule-hash anchoring) is possible. It is the upstream
prerequisite the roadmap explicitly names as blocking the rule-hash guarantee.

**Independent Test**: Run a governance evaluation over a fixture repository that triggers at least one
finding, and confirm every emitted finding in the resulting document carries a non-empty rule id that
identifies its originating rule, with all previously-emitted fields unchanged.

**Acceptance Scenarios**:

1. **Given** a change that triggers a blocking finding from a known rule, **When** the report is produced,
   **Then** that finding carries the originating rule's stable id together with its existing
   verdict/severity/mode/profile/maturity/reason fields.
2. **Given** a change that triggers multiple findings from different rules, **When** the report is produced,
   **Then** each finding carries the id of the specific rule that produced it (not a shared or placeholder id).
3. **Given** the same finding produced from the same inputs in two separate runs, **When** the two reports
   are compared, **Then** the rule id is byte-identical across runs (deterministic, no clock/host/order
   influence).

---

### User Story 2 - Profiles and modes never change a finding's rule identity (Priority: P1)

The same finding, evaluated under different profiles (light → release) or run modes (sandbox → release),
keeps the **same** rule id and maps to the **same** rule hash. Relaxing a profile may change a finding's
*effective severity* (a blocker becomes a visible warning, per the existing no-hide rule) but never its
rule identity, and never removes the finding from the output.

**Why this priority**: This is the integrity guarantee the roadmap row is ultimately about — *"profiles
never hide underlying verdicts, alter rule hashes, or remove findings from JSON."* The per-finding rule id
is what makes this checkable end-to-end at the JSON level. It is co-equal P1 because the rule id is only
trustworthy if it is provably profile/mode-invariant.

**Independent Test**: Evaluate one fixture under every profile and every mode and assert that, for each
finding, the rule id and its mapped rule hash are identical across all profile/mode combinations, while
effective severity may differ and no finding is dropped.

**Acceptance Scenarios**:

1. **Given** a finding evaluated under `light` and again under `release`, **When** both reports are
   produced, **Then** the finding's rule id is byte-identical in both.
2. **Given** a profile that relaxes a base-blocking finding to advisory, **When** the report is produced,
   **Then** the finding is still present, still carries its rule id, shows both base and effective
   severity, and its rule id (and mapped rule hash) is unchanged from the unrelaxed case.
3. **Given** the strictest and the most permissive profile applied to the same finding set, **When** the
   two finding sets are compared, **Then** the set of (rule id, finding) pairs is identical — no profile
   value drops a finding or alters a rule id.

---

### User Story 3 - Findings can be grouped and traced by rule across reports (Priority: P2)

A consumer can collate findings by rule id — across a single report or across the route/verify/ship/audit
surfaces — to answer "how many findings did rule X raise?", "is rule X's verdict consistent between the
local `verify` preview and the protected-branch `ship` decision?", and "which rule do I disable/calibrate
to address this class of finding?".

**Why this priority**: This is the practical payoff that makes the rule id useful day-to-day, but it is
strictly downstream of Stories 1 and 2 and can ship after them.

**Independent Test**: Produce reports from two surfaces (`verify` and `ship`) over the same inputs and
confirm a finding common to both carries the same rule id on both surfaces, enabling a join by rule id.

**Acceptance Scenarios**:

1. **Given** a report containing several findings from the same rule, **When** a consumer groups by rule
   id, **Then** all of that rule's findings collate under one id.
2. **Given** the `verify` and `ship` reports over identical inputs, **When** a finding appears in both,
   **Then** its rule id matches across the two surfaces.

---

### Edge Cases

- **A finding with no upstream rule of record** (e.g. a structural/unknown-governed-path finding that is
  raised by a kernel boundary rather than a catalog gate): the finding MUST still carry a stable, honest
  rule id that identifies its boundary source rather than a fabricated or empty id, and MUST be visibly
  distinguishable from catalog-gate rule ids.
- **Two rules producing findings that are otherwise identical** (same path, severity, message): each
  finding keeps its own rule id; they are not merged.
- **A profile/maturity/mode combination that relaxes a finding to advisory**: the rule id is unchanged and
  the finding is not dropped (intersection of this with the existing no-hide guarantee).
- **An empty finding set**: the output is byte-identical to today (no rule-id field appears because no
  finding appears); the additive field never changes the no-findings path.
- **Rule id stability across rule wording/message changes**: changing a rule's human-readable message MUST
  NOT change its rule id (the id identifies the rule, not the message).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every finding that is enforced and emitted MUST carry a **rule id** that identifies the rule
  (or typed gate) responsible for producing it, alongside the existing per-finding enforcement detail (base
  severity, run mode, profile, maturity, effective severity, and reason). (The verdict is the document-level
  rollup, not a per-finding field; the rule id sits at the finding level beside the finding's `id`.)
- **FR-002**: The rule id MUST be **stable and deterministic** — identical inputs yield a byte-identical
  rule id, with no influence from clock, environment, host path, or input ordering.
- **FR-003**: The rule id MUST be **invariant across profile and run mode** — the same finding evaluated
  under any profile or mode carries the same rule id. (Profiles may still change *effective severity*; they
  never change rule identity.)
- **FR-004**: The rule id MUST map deterministically to the rule's existing **rule hash**, such that the
  rule hash for a given rule id is the same regardless of the profile or mode that evaluated the finding —
  satisfying the long-standing "profiles never alter rule hashes" guarantee at the JSON level.
- **FR-005**: No profile, mode, maturity, or other enforcement dial may **remove** a finding from the
  output or **suppress** the rule id of an emitted finding (extends the existing no-hide rule to the rule-id
  field).
- **FR-006**: The rule id MUST be surfaced on every product surface that already emits per-finding
  enforcement detail — at minimum `audit.json` (`fsgg ship`), `verify.json` (`fsgg verify`), and
  `route.json` (`fsgg route`) — using one consistent representation across surfaces, so a finding common to
  two surfaces carries the same id on both.
- **FR-007**: The addition MUST be **purely additive** to every affected JSON contract: existing fields,
  field ordering, verdict, exit-code scheme, and document `schemaVersion` are unchanged, and the output is
  **byte-identical to the pre-feature output for any input that produces no findings**.
- **FR-008**: A finding whose source is a kernel/boundary rule rather than a catalog gate MUST carry a
  stable, **honest** rule id that names its boundary source and is distinguishable from catalog-gate rule
  ids; the system MUST NOT fabricate a placeholder id or emit an empty id to fill the field.
- **FR-009**: Changing a rule's human-readable message or reason text MUST NOT change its rule id (the id
  identifies the rule, not its wording).
- **FR-010**: When a finding cannot be attributed to a rule of record, the situation MUST be surfaced as a
  disclosed diagnostic (an honest "unattributed" marker), never as a silent pass or a guessed id —
  consistent with the project's observability-and-safe-failure posture.

### Key Entities *(include if feature involves data)*

- **Rule id**: A stable, deterministic identifier for a rule or typed gate that can produce findings. The
  same rule always presents the same id; the id is independent of message text, profile, and mode.
- **Finding (enforced)**: An individual governance finding carrying its enforcement decision. Gains a rule
  id linking it to the rule that produced it, alongside the existing base/effective severity, mode,
  profile, maturity, and reason.
- **Rule hash**: The existing content hash of a rule (used by freshness/provenance). The rule id maps to a
  rule hash; the mapping is profile/mode-invariant.
- **Per-finding enforcement record**: The emitted, per-finding detail in the deterministic JSON
  projections. Gains the rule-id field additively.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of emitted findings across `audit.json`, `verify.json`, and `route.json` carry a
  non-empty rule id identifying their originating rule.
- **SC-002**: For any fixture evaluated under all profiles and all run modes, every finding's rule id and
  mapped rule hash are byte-identical across every profile/mode combination (0 variations).
- **SC-003**: For any input that produces no findings, the produced `audit.json` / `verify.json` /
  `route.json` is byte-identical to the pre-feature output (no schema-version change, no field reordering).
- **SC-004**: A relaxing profile changes a finding's effective severity but never its rule id and never
  drops the finding — demonstrated over the full base-severity × maturity × mode × profile truth table with
  zero rule-id changes and zero dropped findings.
- **SC-005**: A finding present in both the `verify` and `ship` reports over identical inputs carries an
  identical rule id on both surfaces in 100% of cases.
- **SC-006**: Every finding emitted by a kernel/boundary source (not a catalog gate) carries a stable,
  distinguishable, non-empty rule id (no fabricated placeholders, no empty ids).

## Assumptions

- **Scope is the rule-id field and its propagation**, not a redesign of the enforcement truth table. The
  existing effective-severity derivation and the verdict rollup remain unchanged; this feature adds an
  identifying field and threads it through, it does not re-open how blocking is decided.
- **The rule hash already exists** (used by freshness/provenance) and is reused as the anchor for the
  rule-id → rule-hash mapping; this feature does not invent a new hashing scheme.
- **"Rule" includes the typed gates** that participate in route/ship/verify, as well as the kernel boundary
  rules that raise structural findings (e.g. unknown-governed-path). Both kinds must present a rule id; the
  ids must be distinguishable by source.
- **The affected surfaces are the existing deterministic JSON projections** that already emit per-finding
  enforcement detail (`audit.json`, `verify.json`, `route.json`); human/Spectre projections may surface the
  id but are not required to and carry no JSON contract.
- **Additive-only posture is mandatory** per the project's frozen-contract discipline: existing goldens for
  no-finding and unchanged-finding cases must stay byte-identical except for the additive rule-id field on
  emitted findings, and any golden that gains the field is re-blessed deliberately.
- **No new external dependency** is introduced; the work reuses the existing finding, enforcement, rule, and
  projection vocabulary.
