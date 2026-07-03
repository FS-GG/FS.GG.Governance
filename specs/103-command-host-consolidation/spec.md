# Feature Specification: Command-host second extraction pass

**Feature Branch**: `103-command-host-consolidation`

**Created**: 2026-07-03

**Status**: Draft

**Input**: Governance issue #49 (Epic #44, review findings F2, M-CLI-3, M-CLI-7, F9, F13, F15). Continue the command-host consolidation begun by the already-merged first pass (commit `8e43c36`, which extracted the shared `guard` and generic `drive` into the CommandHost leaf). This second pass removes the remaining cross-host duplication and fixes the latent correctness bugs that the duplication hides.

## Context

FS.GG.Governance ships ~seven command hosts (Route, Ship, Verify, Release, Refresh, Evidence, CacheEligibility). Each has its own `Interpreter.fs` carrying near-identical impure-leaf helpers. The 2026-07-02 code-quality review measured the concrete duplication and, crucially, found that the copies have **drifted**: at least one is now a latent bug. Because a helper exists N times, a fix (or a bug) lands in only one copy, so the copies diverge silently — this is the "H3-class drift" root cause. Consolidating to one implementation per concern both removes the duplication and makes the divergences impossible going forward.

This is a **Tier-2 internal refactor**. It is behavior-preserving **except** for a small, enumerated set of correctness fixes (below) that the review flagged. No new product capability is added; the observable command surface stays the same apart from the named fixes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Latent correctness bugs the duplication hid are fixed (Priority: P1)

An operator invokes the governance CLI with argument shapes and flag combinations that today behave incorrectly because the divergent/duplicated code has bugs. After this feature, those invocations behave correctly and identically across every host.

**Why this priority**: These are observable, user-facing defects (wrong output, silently-swallowed flags, non-deterministic ordering). They are the reason the consolidation is worth doing now rather than being cosmetic. They are independently valuable even if no line of duplication were removed.

**Independent Test**: Drive each affected command with the specific failing input and assert the corrected behavior — no knowledge of the internal helper layout is required.

**Acceptance Scenarios**:

1. **Given** any host that takes an option expecting a value (e.g. `--repo`), **When** the operator writes `--repo --json` (forgetting the value), **Then** the host rejects it with a clear error instead of silently consuming `--json` as the value of `--repo` (fixes M-CLI-3).
2. **Given** a command that supports both `--plain` and `--format json`, **When** the operator passes `--format json --plain`, **Then** JSON is emitted (the explicit format wins) rather than `--plain` overriding it (fixes M-CLI-7).
3. **Given** a command that emits a handoff/reference set whose order is observable, **When** it is run twice on the same inputs, **Then** the ordering is identical and matches the single canonical sort — no host emits a divergently-sorted copy (fixes the `realHandoffs` sort drift).
4. **Given** the `Wrote(Ok)` success projection, **When** it is rendered from any host, **Then** the wording/shape is identical across hosts (fixes the F15 micro-drift).
5. **Given** a target already in a `Done` state, **When** a command that must be inert on `Done` is invoked, **Then** it makes no mutation and reports the no-op (adds the F13 Done-inertness guard).

---

### User Story 2 - Each impure leaf has exactly one implementation (Priority: P2)

A maintainer fixing or extending an impure helper (atomic file write, environment/builder sensing, handoff assembly, snapshot/catalog step prefix) edits it in exactly one place and every host inherits the change.

**Why this priority**: This is the structural payload — it removes the drift *mechanism*, not just today's drifted instance. It is the largest slice by lines (~600–800 net deleted) and prevents the P1 bug class from recurring. It depends on the P1 fixes being settled (the canonical version must be the *correct* one).

**Independent Test**: Grep confirms one definition per helper reachable by all hosts; the full suite stays green; a deliberately introduced change in the shared helper is observed to affect all hosts (not just one).

**Acceptance Scenarios**:

1. **Given** the impure helpers `writeAtomic`, `realHandoffs`, `senseEnvironmentReal`, `senseBuilderReal`, and the snapshot/catalog `step` prefix, **When** the codebase is searched, **Then** each has a single shared definition that the hosts call, with no per-host copies remaining.
2. **Given** `EvidenceCommand`, which already references the Cli project, **When** its artifact-reading logic is inspected, **Then** it calls `Cli/ArtifactReading` rather than carrying a verbatim ~325-line copy (`EvidenceCommand/Interpreter.fs` shrinks accordingly).
3. **Given** the consolidated helpers, **When** the SurfaceDrift tests and the full Expecto suite run, **Then** they pass with no assertion changes required beyond those covering the P1 fixes.

---

### User Story 3 - Host conventions converge or are documented (Priority: P3)

A contributor reading any two hosts finds the same vocabulary and exit-decision handling, or a recorded reason why a given divergence is intentional.

**Why this priority**: Lower-risk cleanup that improves consistency but is not a correctness defect. Safe to defer or split if time-boxed.

**Independent Test**: The four format-flag vocabularies are either identical or each divergence is documented in-repo; `CommandHost.ExitDecision` is either adopted by all hosts or removed if dead — verified by grep + a passing suite.

**Acceptance Scenarios**:

1. **Given** the four format-flag vocabularies (F9), **When** they are compared, **Then** they are converged to one vocabulary, or each remaining difference has an in-repo note explaining why it must differ.
2. **Given** `CommandHost.ExitDecision` (re-declared across hosts), **When** exit handling is inspected, **Then** hosts share the one definition, or it is deleted as dead code — no redundant re-declarations remain.

### Edge Cases

- A `--`-prefixed token that is a *legitimate* value (if any option is documented to accept one) must not be broken by the M-CLI-3 fix; the rejection applies only where a value is expected and a flag-looking token appears.
- Consolidating `realHandoffs` onto one sort must not change the *set* of handoffs, only guarantee a stable order — snapshot/golden outputs that encode the old divergent order (if any) must be updated deliberately, not silently.
- Any `.fsi` signature that must change to expose a now-shared helper is an intentional, reviewed surface change guarded by the SurfaceDrift tests — it must not be an accidental visibility widening.
- If a helper's copies are *not* in fact equivalent (a hidden third behavior), consolidation must reconcile them explicitly rather than picking one and dropping the difference unnoticed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST reject an option value that is a `--`-prefixed token where a value is expected, with a clear error, uniformly across all command hosts (M-CLI-3).
- **FR-002**: When both an explicit output format (`--format json`) and `--plain` are supplied, the system MUST honor the explicit format; `--plain` MUST NOT override it (M-CLI-7).
- **FR-003**: The system MUST assemble and emit handoff/reference sets using a single canonical sort shared by every host; no host may retain a divergently-sorted copy.
- **FR-004**: The `Wrote(Ok)` success projection MUST render identically regardless of the emitting host (F15).
- **FR-005**: Commands that must be inert on a `Done` target MUST perform no mutation and MUST report the no-op (F13).
- **FR-006**: Each of `writeAtomic`, `realHandoffs`, `senseEnvironmentReal`, `senseBuilderReal`, and the snapshot/catalog `step` prefix MUST have exactly one shared implementation invoked by all hosts; per-host duplicate definitions MUST be removed.
- **FR-007**: `EvidenceCommand` MUST obtain artifact-reading behavior from `Cli/ArtifactReading` and MUST NOT carry a verbatim copy of it.
- **FR-008**: The four format-flag vocabularies MUST be converged to a single vocabulary, OR each surviving divergence MUST be documented in-repo with its rationale (F9).
- **FR-009**: `CommandHost.ExitDecision` MUST be shared by all hosts that need it, OR removed entirely if unused; redundant re-declarations MUST NOT remain.
- **FR-010**: Apart from the fixes in FR-001–FR-005, the observable behavior of every command MUST be unchanged (behavior-preserving refactor).
- **FR-011**: Any `.fsi` public-surface change MUST be intentional and minimal (only what consolidation requires), and MUST be caught/covered by the SurfaceDrift tests rather than widening visibility accidentally.
- **FR-012**: The full Expecto test suite and the SurfaceDrift tests MUST pass, with new/changed assertions only where they encode FR-001–FR-005.

### Key Entities *(include if feature involves data)*

- **Command host**: A per-command project (Route, Ship, Verify, Release, Refresh, Evidence, CacheEligibility) exposing an interpreter over the shared effect model. The unit across which duplication is measured and removed.
- **Impure leaf helper**: A side-effecting primitive (atomic write, environment/builder sensing, handoff assembly, step prefix) that should have one home in the shared CommandHost project.
- **Format-flag vocabulary**: The set of output-format options a host accepts (`--json` / `--format` / `--plain` variants) — the thing being converged or documented.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The five enumerated correctness defects (M-CLI-3, M-CLI-7, the `realHandoffs` sort drift, F15, F13) each have a test that is RED before the fix and GREEN after, exercised through the real command surface.
- **SC-002**: Net source lines removed is on the order of 600–800 (the review's estimate), verified by the branch diff; no compensating duplication is reintroduced elsewhere.
- **SC-003**: After the change, a repo-wide search finds exactly one definition of each consolidated helper (`writeAtomic`, `realHandoffs`, `senseEnvironmentReal`, `senseBuilderReal`, snapshot/catalog `step` prefix) reachable by all hosts.
- **SC-004**: `EvidenceCommand/Interpreter.fs` no longer contains a copy of `ArtifactReading`; its length drops by roughly the size of the removed copy (~325 lines).
- **SC-005**: The full Expecto suite and the SurfaceDrift tests are green on the branch PR; the deterministic gate and API-compatibility gate report no unintended public-surface change.
- **SC-006**: Every command's non-fixed behavior is identical before and after (verified by the unchanged assertions in the suite plus spot-checked golden outputs).

## Assumptions

- The first extraction pass (`8e43c36`: shared `guard` + generic `drive`) is already in `main`; this feature builds on that shared CommandHost leaf rather than re-doing it.
- The seven-host enumeration is approximate; the actual set is whatever hosts carry the named duplicated helpers at implementation time. Scope is "every host that has a copy," not a fixed list.
- Where copies of a helper are equivalent, the canonical version is the correct/most-complete one; where they differ, the divergence is a bug to reconcile (per the review), not a feature to preserve — unless implementation reveals a genuine intentional difference, which is then documented (FR-008 pattern).
- The SurfaceDrift tests and the bounded `build.fsx test` entrypoint are the evidence mechanism; no new CI job is added by this feature.
- "Documented in-repo" (FR-008) means a code comment or a short note in the relevant module/spec, not a new external document.
