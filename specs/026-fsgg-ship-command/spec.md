# Feature Specification: `fsgg ship` Host Command (Protected-Branch Verdict)

**Feature Branch**: `026-fsgg-ship-command`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved to the next
unstarted Phase-2 row in `docs/initial-implementation-plan.md`: the `fsgg ship --mode gate --profile
standard --json` CLI host edge — "Recompute merge policy from base/head and emit the protected-branch
verdict" (`docs/initial-design.md`). This is the host sibling of the merged `fsgg route` command (F022):
where F022 *showed* the selected gates and explicitly **deferred** the ship verdict because Phase-5
enforcement/profiles did not yet exist, those pure cores now do — enforcement levers and effective
severity (F023), the ship verdict rollup (F024), and the deterministic `audit.json` projection (F025).
This row wires them into the first command that emits a real merge **verdict** and a numeric process
**exit code**. The remaining Phase-2 row after this one — GitHub Actions branch-protection guidance —
stays out of scope.

## Overview

The Phase-2 ship-walking skeleton now has every pure core it needs: typed `.fsgg` facts (F014),
path→capability routing (F015), git/CI snapshot sensing (F016), unknown-governed-path findings (F017),
the typed gate registry (F018), per-change gate selection (F019), the `route.json`/`gates.json`
projections (F020/F021), the `fsgg route` host edge that composes them (F022), enforcement levers and
effective severity (F023), the ship verdict rollup (F024), and the `audit.json` projection (F025). What
no command yet does is **decide whether a change may merge**: turn a routed change into a pass/fail
verdict under a chosen run mode and profile, write the audit document a protected branch reads, and exit
with a code CI can block on.

This feature is the **`fsgg ship` host command** — the design's protected-branch gate. Pointed at a
repository, it senses the base/head change (the same scope sensing the route command established), loads
and validates the project's declared `.fsgg` catalog, routes the change and selects the gates it reaches,
then **applies the chosen run mode and profile** to roll the selection up into a ship decision (F024),
projects that decision to a deterministic `audit.json` document (F025), persists it to disk, prints a
deterministic human or JSON summary of the verdict and its blockers/warnings/passing partition, and
**exits with a numeric code derived from the decision's exit-code basis** — a distinct non-zero
"blocked" code when the change may not merge, zero when it is clear.

It composes already-typed, already-tested values; it re-derives, re-sorts, re-classifies, and
re-serializes nothing the pure cores already fixed. The one genuinely new behavior relative to `fsgg
route` is the **verdict and its consequence**: unlike route — which always exits 0 because "selecting
many gates is information, never a failure" — ship can deliberately fail the process when the decision's
exit-code basis is `Blocked`. That blocked exit MUST stay distinct from the tool's own failure codes
(not a repository, invalid catalog, unwritable output): a blocked merge and a broken tool are different
outcomes and CI must be able to tell them apart.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Emit the protected-branch verdict and exit accordingly (Priority: P1)

A developer (or a CI step) runs `fsgg ship --mode gate --profile standard` at the root of a repository
that has a declared `.fsgg` catalog and a base/head change. They get back, written to disk, an
`audit.json` describing the whole-change verdict — pass or fail, the exit-code basis, and the three-way
blockers/warnings/passing partition with each item's identity and full six-field enforcement detail —
plus a readable summary on the terminal, and a process exit code that is **zero when the change is clear
and a distinct non-zero "blocked" code when it may not merge**.

**Why this priority**: This is the MVP and the whole point of the row — the first time the Phase-2 cores
produce a real merge **decision** with a real **consequence** (a blocking exit code). It is the design's
"Run `fsgg ship --mode gate --profile standard --json` as a protected boundary" acceptance item. Without
it the enforcement/verdict/audit cores are inert; with it a protected branch can actually block a merge.

**Independent Test**: In a temporary git repository with a minimal valid `.fsgg` catalog and a change
that selects a base-blocking gate, run the command under `--mode gate --profile standard` and confirm
`audit.json` is written with `verdict: fail` and `exitCodeBasis: blocked`, the process exits with the
distinct blocked code, and the document matches the F025 projection of the same `Ship.rollup` inputs
byte-for-byte. Repeat with a change that selects only passing items and confirm `verdict: pass`,
`exitCodeBasis: clean`, and exit 0.

**Acceptance Scenarios**:

1. **Given** a repository whose change selects a gate that is blocking under the chosen mode/profile,
   **When** the command runs, **Then** `audit.json` is written with `verdict: fail` and `exitCodeBasis:
   blocked`, and the process exits with the distinct non-zero blocked code.
2. **Given** a repository whose change selects only advisory/passing items, **When** the command runs,
   **Then** `audit.json` is written with `verdict: pass` and `exitCodeBasis: clean`, and the process
   exits 0.
3. **Given** the same repository, **When** the command runs, **Then** the terminal summary states the
   verdict and lists the blockers, warnings, and passing items each with its identity and effective
   severity, and reports any unknown-governed-path findings carried into the decision.

---

### User Story 2 - Choose the run mode and profile (Priority: P1)

A developer or CI pipeline selects how strict the boundary is by passing `--mode` (e.g. `gate`) and
`--profile` (e.g. `standard`), and the command applies exactly those levers when rolling the selection
up — so the same change can be advisory in one context and blocking in another, and the audit document
records which levers produced the verdict.

**Why this priority**: The mode/profile levers are the design's enforcement dials and are named directly
in the command (`--mode gate --profile standard`); the verdict is meaningless without them. The no-hide
rule (F023/F024) means a relaxed profile must never silently hide an underlying blocking finding — it
appears as a warning carrying both base and effective severity. This is P1 because the verdict in US1 is
defined only relative to a chosen mode and profile.

**Independent Test**: Over one repository and one change, run the command with two different
mode/profile combinations and confirm the resulting `audit.json` verdict, partition, and exit code
reflect each combination — e.g. a base-blocking finding lands in `blockers` (fail/blocked) under a
strict combination and in `warnings` (pass/clean, but still self-explaining with base severity
`Blocking`) under a relaxed one — matching `Ship.rollup` of the same routed change under each lever set.

**Acceptance Scenarios**:

1. **Given** `--mode gate --profile standard`, **When** the command runs, **Then** the rollup applies
   that mode and profile and the audit document records them in every item's enforcement detail.
2. **Given** a profile that relaxes a base-blocking finding to advisory, **When** the command runs,
   **Then** that item appears in `warnings` carrying base severity `Blocking` and effective severity
   `Advisory` (the no-hide rule), the verdict is `pass`, and the exit is clean.
3. **Given** an unrecognized `--mode` or `--profile` value, **When** the command runs, **Then** it
   reports the unrecognized lever and exits with the usage-error code, writing no artifact.
4. **Given** neither flag, **When** the command runs, **Then** documented default levers are applied (a
   plan-time reconciliation), and the chosen defaults are recorded in the audit document.

---

### User Story 3 - Deterministic, machine-readable verdict for CI and agents (Priority: P2)

A CI job or an agent runs the command with `--json` and consumes its output programmatically. The
persisted `audit.json` and the JSON summary on stdout are deterministic — byte-for-byte identical for
identical repository inputs and levers — so they can be diffed, cached, and asserted against in fixtures,
and the exit code is a stable function of the verdict.

**Why this priority**: Determinism and a stable exit code are what make the command usable as a
protected-branch contract; it inherits and must not break the F025 byte-stability guarantee. It is P2
because US1/US2 already deliver the verdict and its consequence; this hardens it for automation.

**Independent Test**: Run the command twice over the same fixed repository inputs and levers and confirm
the persisted `audit.json` (and the `--json` stdout summary) are byte-for-byte identical across runs,
contain no wall-clock, host-absolute-path, or environment-derived value, carry the declared schema
version, and that the exit code is identical between runs.

**Acceptance Scenarios**:

1. **Given** fixed repository inputs and levers, **When** the command runs twice, **Then** the persisted
   `audit.json` and the exit code are identical between the two runs.
2. **Given** `--json`, **When** the command runs, **Then** stdout carries a deterministic JSON summary
   (the audit document and/or a verdict envelope) and the human text format is suppressed.
3. **Given** any run, **When** the persisted `audit.json` is inspected, **Then** it carries the declared
   schema version and contains no timestamp, machine-absolute path, or environment-derived value.

---

### User Story 4 - Clear, safe failure distinct from a blocked verdict (Priority: P1)

When the command cannot do its job — git is unavailable or the directory is not a repository, the
`.fsgg` catalog is missing or invalid, an unrecognized lever is supplied, or the output location cannot
be written — it reports a specific, actionable diagnostic and exits with a non-zero code for the
**tool-failure** category that is **distinct from the blocked-verdict code**, never writing a partial or
malformed `audit.json` and never emitting a false pass/fail verdict.

**Why this priority**: Safe failure and honest diagnostics are constitutional (Principle VI), and for a
protected-branch gate the distinction is load-bearing: CI must block on "the change may not merge"
(blocked) without conflating it with "the tool broke" (tool error) — and must never read a tool failure
as a passing merge. This is P1, not P2, precisely because the command now carries a blocking consequence;
a miscategorized exit code is a governance failure, not a cosmetic one.

**Independent Test**: Run the command against (a) a non-git directory, (b) a repository with a missing
required `.fsgg` file, (c) a repository with an invalid `.fsgg` file, (d) an unrecognized `--mode`, and
(e) an unwritable output location, and confirm each yields a distinct, descriptive diagnostic and a
non-zero exit code drawn from the tool-failure categories (usage error, input unavailable, tool error),
none of which equals the blocked-verdict code, and that no `audit.json` is written for input/usage
failures.

**Acceptance Scenarios**:

1. **Given** a directory that is not a git repository, **When** the command runs, **Then** it reports
   that git sensing is unavailable and exits with the input-unavailable code, writing no artifact.
2. **Given** a repository whose required `.fsgg` files are missing or fail validation, **When** the
   command runs, **Then** it reports the specific validation failures and exits with a tool-failure code
   distinct from the blocked-verdict code, writing no partial artifact.
3. **Given** an unrecognized `--mode` or `--profile`, **When** the command runs, **Then** it reports the
   unrecognized lever and exits with the usage-error code, writing no artifact.
4. **Given** an output location that cannot be written, **When** the command runs after a successful
   rollup, **Then** it reports the write failure and exits with a tool-error code — not a success and not
   the blocked-verdict code.

---

### Edge Cases

- **No changes in scope**: an empty base/head change routes nothing, selects no gates, and rolls up to an
  empty partition — `verdict: pass`, `exitCodeBasis: clean`, exit 0, with a valid `audit.json` whose
  blockers/warnings/passing arrays are present and empty.
- **Empty catalog**: a valid but empty `.fsgg` catalog yields an empty registry and selection; the rollup
  is a clean pass and the command exits 0 with a valid empty-partition `audit.json`.
- **Unknown governed paths**: paths under a declared governed root that match no capability surface are
  carried as findings into the rollup; whether such a finding blocks is decided entirely by F023/F024
  enforcement under the chosen mode/profile (never by this host edge), and the audit document explains
  why it does or does not block.
- **Relaxed profile, base-blocking finding**: appears as a warning carrying both base `Blocking` and
  effective `Advisory` (the no-hide rule); the verdict is pass and the exit is clean, but the document
  makes the relaxation observable.
- **Dirty working tree / untracked files**: sensed working-tree state is carried into routing exactly as
  the snapshot reports it; the command does not refuse to run on a dirty tree.
- **Re-running over an existing output**: persisting overwrites the prior `audit.json` deterministically;
  a re-run with identical inputs and levers produces an identical file and exit code.
- **Blocked verdict vs tool error**: a `fail`/`blocked` verdict (the change may not merge) and a tool
  failure (the tool could not run) MUST surface as distinct non-zero exit codes; neither may be reported
  as success.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide an `fsgg ship` command that, given a repository root, senses the
  base/head change, loads the declared `.fsgg` catalog, routes the change, selects the gates it reaches,
  rolls the selection up into a ship decision under a chosen run mode and profile, persists the verdict
  as `audit.json`, and exits with a code derived from the decision's exit-code basis.
- **FR-002**: The command MUST accept a run mode (`--mode`) and a profile (`--profile`) and apply exactly
  those levers when rolling up the selection, reusing the existing enforcement and rollup cores (F023,
  F024); it MUST NOT re-implement severity, mode, profile, or verdict logic.
- **FR-003**: The command MUST load and validate the project's `.fsgg` catalog and use the validated
  typed facts as the basis for routing, selection, and the rollup; it MUST NOT route or roll up from raw
  file text.
- **FR-004**: The command MUST produce the ship decision by composing the existing pure cores — routing,
  selection, unknown-governed-path findings, and the F024 rollup over the chosen mode/profile —
  re-deriving, re-sorting, and re-classifying nothing those cores already fixed.
- **FR-005**: The command MUST persist the verdict as an `audit.json` document that is exactly the
  existing deterministic F025 projection of the rolled-up `ShipDecision`; it MUST NOT re-serialize or
  re-shape the document.
- **FR-006**: The persisted `audit.json` MUST be byte-for-byte identical for identical repository inputs
  and levers, carry its declared schema version, and contain no wall-clock, machine-absolute path, or
  environment-derived value.
- **FR-007**: The command MUST print a deterministic summary of the verdict, the exit-code basis, and the
  blockers/warnings/passing partition (each item's identity and effective/base severity) plus any
  unknown-governed-path findings, in a human-readable text format by default and a machine-readable JSON
  format on `--json`.
- **FR-008**: The command MUST map the decision's exit-code basis to a numeric process exit code: `Clean`
  → 0 (success) and `Blocked` → a single distinct non-zero "blocked" code that is reserved for a blocked
  merge verdict and used for no other outcome.
- **FR-009**: The blocked-verdict exit code MUST be distinct from every tool-failure exit code (usage
  error, input unavailable, tool error); a blocked merge and a tool failure MUST be separately
  identifiable from the exit code alone, and a tool failure MUST never be reported as a success.
- **FR-010**: The command MUST fail with a distinct, descriptive diagnostic and a tool-failure exit code,
  writing no partial or malformed `audit.json`, when git sensing is unavailable, the directory is not a
  repository, the `.fsgg` catalog is missing or invalid, a supplied lever is unrecognized, or the output
  location cannot be written.
- **FR-011**: The command MUST honor the no-hide rule end to end: a base-blocking item relaxed by
  mode/maturity/profile MUST appear in the summary and the persisted document as a warning carrying both
  its base and effective severity, never silently dropped or downgraded without trace.
- **FR-012**: The command MUST treat routine unclassified paths as information (never a default-deny);
  whether an unknown-governed-path finding blocks MUST be decided solely by the enforcement/rollup cores
  under the chosen levers, not by this host edge.
- **FR-013**: All filesystem and git access MUST occur at an injected, fakeable boundary so the
  composition — including the verdict and the exit-code mapping — can be exercised in tests without a
  real working tree or git process; the pure composition and exit decision MUST be testable
  deterministically with faked sensing and writing.
- **FR-014**: The command MUST be deterministic in its observable behavior for fixed inputs and levers
  (same artifact, same summary, same exit code) and MUST never throw an unhandled exception — every
  failure surfaces as a diagnostic and an exit code.

### Key Entities *(include if feature involves data)*

- **Ship run request**: the normalized invocation — repository root, run mode and profile levers, output
  destination, and output format (text or JSON). (Scope sensing reuses the route command's base/head
  range; an explicit-paths/since selector is a plan-time reconciliation, not assumed here.)
- **Ship run result**: the outcome of one invocation — the rolled-up ship decision (verdict, exit-code
  basis, and the blockers/warnings/passing partition), the persisted `audit.json` path, the diagnostics,
  and the process exit decision.
- **`audit.json` (whole-change verdict view)**: the deterministic, versioned document carrying the
  verdict, exit-code basis, and the three-way partition with each item's identity and six-field
  enforcement detail (the F025 projection).
- **Exit decision**: the process-level outcome mapped to a numeric exit code. Unlike the route command's
  exit decision, this one **does** include a governed-blocking outcome (`Blocked` → distinct non-zero),
  kept distinct from the tool-failure categories (success, usage error, input unavailable, tool error).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a repository with a valid catalog and a change that selects a gate blocking under the
  chosen mode/profile, a single command run writes `audit.json` with `verdict: fail` /
  `exitCodeBasis: blocked` and the process exits with the distinct non-zero blocked code; a change with
  only passing items writes `verdict: pass` / `exitCodeBasis: clean` and exits 0.
- **SC-002**: Running the command twice over identical repository inputs and levers produces a
  byte-for-byte identical `audit.json` (and identical `--json` stdout) and an identical exit code.
- **SC-003**: The same change rolled up under two different mode/profile combinations produces the
  verdict, partition, and exit code appropriate to each combination, matching the rollup of the same
  routed change under each lever set.
- **SC-004**: A blocked-verdict exit code and each tool-failure exit code (not a repository / unavailable
  git, missing-or-invalid catalog, unrecognized lever, unwritable output) are all mutually distinct, and
  no input/usage failure writes an artifact.
- **SC-005**: The persisted `audit.json` contains the declared schema version and none of: a wall-clock
  value, a machine-absolute path, or an environment-derived value; and every base-blocking item relaxed
  by the profile appears as a warning carrying both base and effective severity.
- **SC-006**: A change touching only routine unclassified paths, and a valid empty catalog, each produce
  a clean-pass run with a valid empty-partition `audit.json` and exit 0.
- **SC-007**: The full composition — scope sensing, catalog load, route, select, rollup under the chosen
  levers, project, persist, summarize, and exit-code mapping — is exercised end-to-end through fakeable
  git and filesystem boundaries without requiring a real git process, and the artifact it writes matches
  the real F025 projection of the rolled-up decision.

## Assumptions

- **Slice boundary**: This row delivers the `fsgg ship` command. The remaining Phase-2 row — GitHub
  Actions branch-protection guidance (how to wire this exit code into a protected branch) — is explicitly
  **out of scope**, as are release/provenance attestation references (the `ShipDecision` carries none —
  deferred to the Release phase, exactly as F025 deferred them) and cache/freshness evaluation (Phase 11;
  freshness-key inputs are carried but no cache verdict is computed).
- **Reuse, don't re-derive**: The command composes the existing cores and sensing edges verbatim — the
  F022 route composition (snapshot sensing, catalog load+validate, routing, registry, findings,
  selection), the F023 enforcement levers and `recognize` parsers for `--mode`/`--profile`, the F024
  `Ship.rollup`, and the F025 `audit.json` projection. It adds the rollup wiring, the audit persistence
  edge, and the exit-code mapping — not new severity, verdict, or serialization logic.
- **Verdict is the new behavior**: Unlike `fsgg route` (which always exits 0 because selection is
  information), `fsgg ship` deliberately fails the process when the decision's exit-code basis is
  `Blocked`. The exact numeric value of the blocked code and of each tool-failure code is a plan-time
  reconciliation, subject to the constraint that the blocked code is distinct from all tool-failure codes
  (FR-009).
- **Default levers**: The design names `--mode gate --profile standard` as the canonical protected-branch
  invocation. Whether those are also the defaults when a flag is omitted, versus requiring explicit
  levers, is a plan-time reconciliation; either way the applied levers are recorded in the audit document.
- **Output location**: `audit.json` is written under a generated readiness location. The design's
  canonical per-change path is `readiness/<id>/audit.json`, but `<id>` comes from the SDD work-item model
  which does not exist in this Governance-only skeleton; a default deterministic destination is used (the
  `route.json` sibling location F022 established), overridable by the caller. The exact default path and
  any override flag are a plan-time reconciliation.
- **Project home / command surface**: Whether this host edge lands as a new Phase-2 host project (e.g. a
  `FS.GG.Governance.ShipCommand` mirroring `FS.GG.Governance.RouteCommand`) or extends an existing one,
  and the precise flag spelling (`--mode`, `--profile`, `--json`, output override), are plan-time
  reconciliations — consistent with how prior Phase-2 host rows deferred project home to plan. The
  older kernel-era `FS.GG.Governance.Cli` is a distinct lineage and is not assumed to be reused.
- **Boundary discipline**: Because the command performs multi-step external I/O (git sensing, catalog
  reads, artifact writes) and ends in a process exit code, it is a stateful host edge with injected,
  fakeable ports and a pure MVU core — the exact shape F022 established — not a single pure function.
- **Determinism inheritance**: Byte-stability of `audit.json` comes from reusing the F025 projection
  unchanged; this feature must not introduce any nondeterministic value (clock, absolute path,
  environment) into the persisted document, and the exit code must be a deterministic function of the
  verdict.
