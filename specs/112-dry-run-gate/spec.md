# Feature Specification: Dry-run / simulated governance gate

**Feature Branch**: `112-dry-run-gate`

**Created**: 2026-07-07

**Status**: Draft

**Input**: Governance issue [#101](https://github.com/FS-GG/FS.GG.Governance/issues/101) —
"Add a `--dry-run` / simulated governance gate over ship.json". From the FS.GG.Audio
workflow-feedback (§3.10, §5): **every** Governance signal came back `notEvaluated` across both
runs because no Governance runtime was installed — including on the single most gate-worthy event,
an additive published-contract change. The SDD↔Governance boundary being *optional* is correct,
but "optional" had collapsed into "untestable": there was no way to preview gate behaviour or
check the handoff's **sufficiency** without a full install.

**Change tier**: **Tier 1** — this adds public CLI surface (a new dry-run lever and its output
contract). The `.fsi` surfaces and surface-area baselines move in lockstep; a simulated-verdict
JSON projection is a new (clearly-marked) wire shape.

## Why this exists (the gap)

Governance is designed to be a *protected boundary* that stays out of the way when nothing
gate-worthy happens. But a consumer team (e.g. FS.GG.Audio) that has **not** installed the
Governance runtime cannot answer two questions that matter most precisely when a boundary is
about to be crossed:

1. **"What would Governance say about this ship?"** — a preview of the pass/fail verdict.
2. **"Is my handoff even sufficient?"** — whether the `governance-handoff.json` carries the
   signals the policy needs, or whether the real gate would simply return `notEvaluated` (an
   *absence*, not a *pass*) the way it did in the Audio runs.

Today both require the full runtime. This feature makes the boundary **previewable** from the two
artifacts a team already has — `ship.json` / `governance-handoff.json` — evaluated against a
**bundled sample policy**, with no runtime install and no real gate-command execution.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Preview the gate verdict without a runtime (Priority: P1)

A reviewer or a consumer team has a `governance-handoff.json` (and/or a `ship.json`) but no
installed Governance runtime. They point the dry-run mode at those artifacts plus a sample policy
and get back a **simulated verdict** — Pass / Fail with the blocking and warning items — printed
to the terminal. Nothing is executed and nothing is written to `readiness/`.

**Why this priority**: This is the MVP and the core of the issue. It converts an *untestable*
optional boundary into a *previewable* one. Without it, the other stories have nothing to render.

**Independent Test**: Run the dry-run mode against a fixture handoff + the bundled sample policy
on a machine with no Governance gates installed; confirm it prints a deterministic verdict and
writes no repo artifacts. Fully delivers value on its own.

**Acceptance Scenarios**:

1. **Given** a valid `governance-handoff.json` and the bundled sample policy, **When** the user
   runs the dry-run preview, **Then** a simulated verdict (Pass/Fail + blockers/warnings) is
   printed and the process performs no gate-command execution and writes no `readiness/` artifact.
2. **Given** a handoff that the policy would clear, **When** previewed, **Then** the verdict is
   Pass with an empty blocker list.
3. **Given** a handoff whose evidence a policy gate would block on, **When** previewed, **Then**
   the verdict is Fail and the offending gate/finding appears in the blockers.
4. **Given** the same inputs run twice, **When** previewed, **Then** the output is byte-identical
   (deterministic preview).

---

### User Story 2 - See handoff sufficiency, especially against a surface bump (Priority: P2)

A team is about to ship an additive published-contract change (a surface bump) — the exact event
that came back all-`notEvaluated` in the Audio feedback. Before shipping they want to know
**which required signals their handoff is missing**: which policy gates would have nothing to
evaluate (would return `notEvaluated`) versus which are actually satisfied. The dry-run distinguishes
"the policy did not require this" from "the policy required it but your handoff didn't carry it".

**Why this priority**: This is the sharper half of the issue — the failure mode wasn't a *Fail*,
it was a silent *absence*. A verdict alone (US1) can look green while the handoff is actually
empty. Sufficiency reporting names the gap so a team can fix the handoff before the real gate runs.

**Independent Test**: Feed a handoff that omits a signal the sample policy's surface-bump gate
requires; confirm the preview flags that gate as **unsatisfied / would-be-`notEvaluated`** and
distinguishes it from gates the policy never required. Testable against fixtures without US3.

**Acceptance Scenarios**:

1. **Given** a sample policy with a gate that requires a signal, and a handoff that omits it,
   **When** previewed, **Then** that gate is reported as unsatisfied (the required-but-absent
   case), separately from any gate the policy did not require.
2. **Given** a surface-bump-shaped handoff, **When** previewed under a stricter profile, **Then**
   the sufficiency report reflects the stricter profile's additional requirements.
3. **Given** a handoff that satisfies every required signal, **When** previewed, **Then** the
   sufficiency report shows no required-but-absent gaps.

---

### User Story 3 - Machine-readable simulated result for CI and review (Priority: P3)

A reviewer or a CI step wants the dry-run result as a **machine-readable document** that mirrors
the real audit/ship JSON shape (so existing tooling can read it) but is **unambiguously marked as
simulated** so it can never be mistaken for, or substituted for, a real gate result.

**Why this priority**: Makes the preview scriptable and diffable in review, but the human preview
(US1) and sufficiency (US2) already deliver the core value; this is an additive projection.

**Independent Test**: Run the dry-run in machine-readable mode; confirm the emitted document
validates against the audit contract's readable shape, carries an explicit `simulated`/dry-run
marker at top level, and is byte-identical across repeated runs on the same inputs.

**Acceptance Scenarios**:

1. **Given** any dry-run evaluation, **When** the machine-readable output is requested, **Then**
   the document carries an explicit top-level marker identifying it as a simulated/dry-run result.
2. **Given** the machine-readable output, **When** compared to a real audit document, **Then** it
   is structurally recognizable to the same readers but distinguishable by its marker.
3. **Given** the same inputs, **When** emitted twice, **Then** the documents are byte-identical.

---

### Edge Cases

- **Malformed or version-mismatched handoff**: the preview MUST fail safely with a clear
  diagnostic naming the cause (malformed / contract-version mismatch), not a crash and not a
  silent Pass. An unreadable handoff is a *defect in the input*, distinct from a *satisfied* or an
  *absent* signal.
- **No handoff and no ship.json provided**: the preview MUST report that it had nothing to
  evaluate (an explicit empty-input diagnostic), never an implicit Pass.
- **Sample policy absent or unspecified**: the mode MUST use a bundled default sample policy so it
  works with zero configuration; an explicitly-named policy that cannot be loaded is a clear error.
- **Everything `notEvaluated`**: when the policy required nothing the handoff carried, the preview
  MUST make the all-absent state visible (this is the exact Audio failure mode) rather than
  presenting an empty blocker list as a clean Pass.
- **Simulated result must never masquerade as real**: no dry-run output may be written to the real
  `readiness/` artifact path, and the machine-readable form MUST carry its simulated marker so it
  cannot be consumed as a genuine gate outcome.
- **Profile selection**: the preview MUST let the user choose the evaluation profile (the handoff
  gate is profile-aware); an unrecognized profile name is a clear error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a dry-run / simulated-gate mode that evaluates a
  `governance-handoff.json` and/or a `ship.json` against a sample policy and reports a verdict,
  **without requiring an installed Governance runtime** and **without executing any real gate
  command**.
- **FR-002**: The mode MUST accept the input artifact(s) explicitly (so a team with no repo
  install can point at files it already has), and MUST use a **bundled default sample policy**
  when none is specified.
- **FR-003**: The mode MUST print a human-readable simulated verdict — Pass/Fail with the blocking
  and warning items — as its primary output.
- **FR-004**: The mode MUST report **handoff sufficiency**: for the chosen policy and profile, it
  MUST distinguish (a) signals the policy did not require, (b) required signals the handoff
  satisfied, and (c) required signals the handoff did **not** carry (the would-be-`notEvaluated`
  gaps).
- **FR-005**: The mode MUST NOT write to the real gate-result artifact location (`readiness/`) and
  MUST NOT mutate repository state; it is read-and-report only.
- **FR-006**: Every simulated output — human and machine-readable — MUST be **clearly and
  unambiguously marked as simulated/dry-run**, so it can never be mistaken for, or substituted for,
  a real gate result.
- **FR-007**: The mode MUST let the user select the evaluation **profile** (the handoff gate is
  profile-aware); the sufficiency and verdict MUST reflect the chosen profile.
- **FR-008**: The mode MUST fail safely on bad input — malformed or version-mismatched handoff,
  absent-but-named policy, or no input at all — with a diagnostic that names the cause, and MUST
  NOT emit a Pass verdict for a defect-in-input or empty-input case.
- **FR-009**: Output MUST be **deterministic**: identical inputs produce byte-identical output
  (repo convention for all projections).
- **FR-010**: The mode MUST offer a **machine-readable** projection of the simulated result that is
  structurally recognizable to existing audit/ship JSON readers yet carries the FR-006 simulated
  marker (User Story 3).
- **FR-011**: The mode MUST distinguish, in its output, the *absence* of a signal from a *pass* —
  an all-`notEvaluated` evaluation MUST be visibly surfaced, not rendered as a clean Pass with an
  empty blocker list.

### Key Entities *(include if feature involves data)*

- **Governance handoff** (`governance-handoff.json`): the SDD→Governance contract document — a
  contract/schema version, an evidence block of declared nodes with states, an optional readiness
  block, and governed references. The primary dry-run input.
- **Ship rollup / audit document** (`ship.json` / `audit.json`): the existing ship-verdict wire
  contract (verdict, blockers, warnings, passing items, per-gate outcomes). The dry-run's output
  is a *simulated* projection recognizable against this shape.
- **Sample policy** (bundled reference gate set): the versioned reference `.fsgg` policy/profile
  set that serves as the default "what the boundary would enforce" when no policy is supplied.
- **Simulated verdict**: the dry-run result — a Pass/Fail plus blockers, warnings, and the
  per-signal sufficiency breakdown — carrying an explicit simulated marker.
- **Profile**: the enforcement profile (e.g. light / standard / strict / release) the preview
  evaluates under.
- **Sufficiency breakdown**: per-gate classification of *not-required* / *satisfied* /
  *required-but-absent* for the chosen policy and profile.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with **no** Governance runtime installed can obtain a simulated gate verdict
  from a `governance-handoff.json` and the bundled sample policy in a single command invocation.
- **SC-002**: For a handoff missing a policy-required signal, the preview names the specific
  required-but-absent gate(s) in 100% of cases — the all-`notEvaluated` Audio failure mode is
  never presented as a clean Pass.
- **SC-003**: A dry-run invocation writes **zero** repository artifacts (no `readiness/` file, no
  state mutation), verifiable by an unchanged working tree after the run.
- **SC-004**: Every simulated output carries a simulated/dry-run marker; a reviewer (or a script)
  can distinguish it from a real gate result in 100% of cases.
- **SC-005**: Repeated runs on identical inputs produce byte-identical output (human and
  machine-readable), verifiable by a diff.
- **SC-006**: The same inputs previewed under a stricter profile surface at least the strict
  profile's additional requirements, demonstrating profile-aware evaluation.

## Assumptions

- **Sample policy = the bundled reference gate set.** The default "sample policy" is the existing
  versioned reference `.fsgg` gate set shipped with the project (the same one packaged for
  consumers), so the mode works with zero configuration and no external file.
- **Explicit inputs, no runtime sensing required.** Because the target user has no install, the
  mode is driven by explicitly-provided artifact paths rather than requiring a sensed repo/git
  diff; if run inside a repo that already has the artifacts, sensing them is an acceptable
  convenience but not required for the MVP.
- **Preview is informational — exit status does not fail a pipeline by default.** The dry-run is a
  preview, so by default it completes successfully (exit 0) with the simulated verdict carried in
  the output rather than the process exit code; whether to also offer an opt-in "exit reflects the
  simulated verdict" lever is a plan-phase decision, not a spec requirement.
- **Reuses existing surfaces.** The verdict rollup, the audit/ship JSON shape, the human
  (plain/rich) projections, and the SDD-handoff ingestion already exist; this feature composes
  them behind a simulated evaluation that substitutes a no-execution path for real gate commands.
  No new external dependency is introduced.
- **Scope boundary.** This feature previews the boundary; it does not change what the *real* gate
  enforces, nor the handoff contract itself, nor how the runtime is installed.
