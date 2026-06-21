# Feature Specification: `fsgg route` Host Command

**Feature Branch**: `022-fsgg-route-command`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved to the next
unstarted Phase-2 row in `docs/initial-implementation-plan.md`: "Add `fsgg route --paths ...`,
`fsgg route --since <rev>`, and `fsgg ship --mode gate --profile standard --json` (the CLI host edge
that persists route.json / gates.json to disk)." Sliced (consistent with every prior Phase-2 row) to
the **`fsgg route`** command alone — the first end-to-end host edge that composes the existing pure
cores and persists the two JSON artifacts — deferring the `fsgg ship` protected-branch **verdict** and
`audit.json` to a later row, because the ship verdict needs Phase-5 enforcement/profiles that do not yet
exist. The design itself separates the two: `fsgg route` "Show[s] selected gates and route trace for
paths, since-rev, or base/head," while `fsgg ship` "Recompute[s] merge policy … and emit[s] the
protected-branch verdict" (`docs/initial-design.md`).

## Overview

Every pure core of the Phase-2 ship-walking skeleton now exists in isolation — typed `.fsgg` facts
(F014), path→capability routing (F015), git/CI snapshot sensing (F016), unknown-governed-path findings
(F017), the typed gate registry (F018), per-change gate selection (F019), and the deterministic
`route.json` (F020) and `gates.json` (F021) projections — but **nothing has ever wired them together
over a real repository**. Each row landed a pure value or an isolated sensing edge; no command yet
senses a working tree, loads the declared catalog, routes the change, selects the gates, and writes the
artifacts a person or CI can read.

This feature is that first composition: the **`fsgg route` host command**. Pointed at a repository, it
senses which paths changed (over an explicit path list, a since-revision, or the default base/head
range), loads and validates the project's declared `.fsgg` catalog, routes the changed paths to their
capabilities, builds the whole-catalog gate registry, computes unknown-governed-path findings, selects
the gates the change reaches, and then **persists two deterministic documents to disk** — the
whole-catalog `gates.json` and the per-change `route.json` — while printing a deterministic human or
JSON summary of the selected gates and route trace. It is the design's "Route a local scoped change
cheaply and explain selected gates" acceptance item.

It composes already-typed, already-tested values; it **re-derives, re-sorts, and re-classifies
nothing**. Crucially, it computes **no ship verdict**: it shows which gates a change selects and why,
but it does not decide whether the change may merge, assign severity/profile/mode/enforcement, evaluate
cache eligibility, or set an exit-code basis from blockers. Those are `fsgg ship` / `audit.json` /
Phase 5 / Phase 11. `fsgg route` succeeds (exit 0) whenever it can sense the repo, load a valid catalog,
and write the artifacts — the change "selecting many gates" is information, never a failure.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Route the current change and persist the artifacts (Priority: P1)

A developer (or a CI step) runs `fsgg route` at the root of a repository that has a declared `.fsgg`
catalog and some changed files. They get back, written to disk, a `gates.json` describing the whole
declared gate catalog and a `route.json` describing exactly which gates this change selected, why each
was selected (the route trace: selecting path, domain, matched glob), which governed paths are
unclassified, and the route's rolled-up cost — plus a readable summary on the terminal.

**Why this priority**: This is the MVP and the whole point of the row — the first time the Phase-2 pure
cores produce a real, on-disk, machine-readable result for an actual change. Without it the cores are
inert; with it a generated product can route a scoped change before the full lifecycle exists.

**Independent Test**: In a temporary git repository with a minimal valid `.fsgg` catalog and a small
edit, run the command and confirm both files are written, `route.json` lists the gates whose domain the
edited path routes to, and `gates.json` lists the full declared catalog — matching the F020/F021
projection of the same inputs byte-for-byte.

**Acceptance Scenarios**:

1. **Given** a repository with a valid `.fsgg` catalog and one changed file under a declared capability
   surface, **When** the command runs, **Then** `route.json` and `gates.json` are written to disk, the
   selected-gate set in `route.json` is exactly the gates the changed path's domain declares, and the
   command exits successfully.
2. **Given** the same repository, **When** the command runs, **Then** the terminal summary lists each
   selected gate by id with its selecting path and the route's per-tier cost, and reports any
   unknown-governed-path findings.
3. **Given** a repository whose change touches no declared capability surface (only routine,
   unclassified paths), **When** the command runs, **Then** it succeeds, `route.json` selects no gates,
   and the summary says so — routine unclassified files never produce a failure or a default-deny.

---

### User Story 2 - Scope the change to route (Priority: P1)

A developer wants to route a specific slice rather than whatever the working tree currently shows. They
pass an explicit list of paths, a since-revision, or rely on the default base/head range, and the
command routes exactly that slice.

**Why this priority**: Targeted, cheap routing is a core design promise ("Keep optional routing
targeted"); the plan row names `--paths` and `--since` explicitly. Without scoping the command is far
less useful for local authoring and CI.

**Independent Test**: In one repository, run the command three ways — with an explicit path list, with a
since-revision, and with neither — and confirm the routed/selected set reflects the requested scope
(the explicit list routes exactly those paths; the since-revision routes the paths changed since that
revision; the default routes the sensed base/head change).

**Acceptance Scenarios**:

1. **Given** an explicit list of paths, **When** the command runs with that list, **Then** routing and
   selection consider exactly those paths and ignore the working tree's other changes.
2. **Given** a since-revision argument, **When** the command runs, **Then** the changed-path set is the
   paths changed since that revision, routed and selected accordingly.
3. **Given** neither argument, **When** the command runs, **Then** it senses the default base/head
   change for the working tree and routes that.

---

### User Story 3 - Deterministic, machine-readable result for CI and agents (Priority: P2)

A CI job or an agent runs the command and consumes its output programmatically. The persisted `route.json`
and `gates.json`, and an optional JSON summary on stdout, are deterministic — byte-for-byte identical for
identical repository inputs — so they can be diffed, cached, and asserted against in fixtures.

**Why this priority**: Determinism is what makes the artifacts usable as a contract for downstream
consumers and golden-snapshot tests; it inherits and must not break the F020/F021 byte-stability
guarantee. It is P2 because US1/US2 already deliver value to a human; this hardens it for automation.

**Independent Test**: Run the command twice over the same fixed repository inputs and confirm the
persisted files (and the `--json` stdout summary) are byte-for-byte identical across runs, and contain
no wall-clock, host-absolute-path, or environment-derived value.

**Acceptance Scenarios**:

1. **Given** fixed repository inputs, **When** the command runs twice, **Then** the persisted
   `route.json` and `gates.json` are byte-for-byte identical between the two runs.
2. **Given** a request for JSON output, **When** the command runs, **Then** stdout carries a
   deterministic JSON summary and the human text format is suppressed.
3. **Given** any successful run, **When** the persisted artifacts are inspected, **Then** they carry the
   declared schema versions and contain no timestamp, machine-absolute path, or environment-derived
   value.

---

### User Story 4 - Clear, safe failure (Priority: P2)

When the command cannot do its job — git is unavailable or the directory is not a repository, the
`.fsgg` catalog is missing or invalid, a requested revision does not exist, or the output location
cannot be written — it reports a specific, actionable diagnostic and exits with a distinct non-zero code
for the failure category, never writing a partial or malformed artifact and never emitting a false
"blocking" or "passing" verdict.

**Why this priority**: Safe failure and honest diagnostics are constitutional (Principle VI) and are
what let CI distinguish "your change selects gates" from "the tool could not run." It is P2 because the
happy path (US1/US2) is the MVP, but a host edge is not shippable without it.

**Independent Test**: Run the command against (a) a non-git directory, (b) a repository with a missing
required `.fsgg` file, (c) a repository with an invalid `.fsgg` file, and (d) a since-revision that does
not exist, and confirm each yields a distinct, descriptive diagnostic and an appropriate non-zero exit
code with no artifact written.

**Acceptance Scenarios**:

1. **Given** a directory that is not a git repository, **When** the command runs, **Then** it reports
   that git sensing is unavailable and exits with an input-unavailable code, writing no artifact.
2. **Given** a repository whose required `.fsgg` files are missing or fail validation, **When** the
   command runs, **Then** it reports the specific validation failures (file, locator, reason) and exits
   non-zero without writing a partial artifact.
3. **Given** a since-revision that does not resolve, **When** the command runs, **Then** it reports the
   unknown revision and exits with an input-unavailable code.
4. **Given** an output location that cannot be written, **When** the command runs after a successful
   route, **Then** it reports the write failure and exits with a tool-error code, not a success.

---

### Edge Cases

- **No changes in scope**: an empty changed-path set routes nothing and selects no gates; the command
  succeeds and writes a valid `route.json` with an empty selected-gate set (and the full `gates.json`).
- **Empty catalog**: a valid but empty `.fsgg` catalog yields an empty gate registry; `gates.json` is a
  valid document with an empty gate list and `route.json` selects nothing — both succeed, neither errors.
- **Unknown governed paths**: paths under a declared governed root that match no capability surface are
  reported as findings in `route.json` and the summary, but do not by themselves fail the command (no
  ship verdict here — escalation/blocking is `fsgg ship`/Phase 5).
- **Dirty working tree / untracked files**: sensed working-tree state is carried into routing exactly as
  F016 reports it; the command does not refuse to run on a dirty tree.
- **Re-running over an existing output**: persisting overwrites the prior `route.json`/`gates.json`
  deterministically; a re-run with identical inputs produces identical files.
- **Catalog valid but git scope produces no diff** (e.g. since-revision equals head): a valid empty
  change, handled as "no changes in scope" above.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide an `fsgg route` command that, given a repository root, senses the
  changed paths, loads the declared `.fsgg` catalog, routes the change, selects the gates it reaches,
  and persists the result.
- **FR-002**: The command MUST determine the changed-path set from one of: an explicit list of paths, a
  since-revision, or (when neither is given) the default sensed base/head range.
- **FR-003**: The command MUST load and validate the project's `.fsgg` catalog and use the validated
  typed facts as the basis for routing and the gate registry; it MUST NOT route from raw file text.
- **FR-004**: The command MUST select gates using the existing pure selection over the routed change,
  the whole-catalog gate registry, and the unknown-governed-path findings — re-deriving, re-sorting, and
  re-classifying nothing those cores already fixed.
- **FR-005**: The command MUST persist a whole-catalog `gates.json` document and a per-change
  `route.json` document to disk, each being exactly the existing deterministic projection of its typed
  input (the F021 and F020 projections respectively).
- **FR-006**: The persisted artifacts MUST be byte-for-byte identical for identical repository inputs,
  carry their declared schema versions, and contain no wall-clock, machine-absolute path, or
  environment-derived value.
- **FR-007**: The command MUST print a deterministic summary of the selected gates, route trace, cost
  rollup, and any unknown-governed-path findings, in a human-readable text format by default and a
  machine-readable JSON format on request.
- **FR-008**: The command MUST NOT compute or emit a ship verdict, merge decision, severity, profile,
  mode, enforcement state, cache-eligibility verdict, blockers, warnings list, or exit-code basis; it
  carries each selected gate's freshness-key inputs forward but evaluates no cache eligibility.
- **FR-009**: The command MUST exit successfully (code 0) whenever it senses the repository, loads a
  valid catalog, selects gates, and writes the artifacts — regardless of how many gates were selected or
  how many findings were produced.
- **FR-010**: The command MUST fail with a distinct, descriptive diagnostic and an appropriate non-zero
  exit code, writing no partial or malformed artifact, when git sensing is unavailable, the directory is
  not a repository, a requested revision does not resolve, the `.fsgg` catalog is missing or invalid, or
  the output location cannot be written; failure categories (usage error, input unavailable, tool error)
  MUST map to distinct exit codes.
- **FR-011**: The command MUST treat routine unclassified paths and unknown-governed-path findings as
  information in the result — never as a default-deny or an automatic command failure.
- **FR-012**: All filesystem and git access MUST occur at an injected, fakeable boundary so the
  composition can be exercised in tests without a real working tree or git process; the pure composition
  decision MUST be testable deterministically with faked sensing and writing.
- **FR-013**: The command MUST be deterministic in its observable behavior for fixed inputs (same
  artifacts, same summary, same exit code) and MUST never throw an unhandled exception — every failure
  surfaces as a diagnostic and an exit code.

### Key Entities *(include if feature involves data)*

- **Route run request**: the normalized invocation — repository root, scope selector (explicit paths,
  since-revision, or default base/head), output destination, and output format (text or JSON).
- **Route run result**: the outcome of one invocation — the selected-gate set with route trace, the
  unknown-governed-path findings, the cost rollup, the two persisted document paths, the diagnostics,
  and the exit decision.
- **`gates.json` (whole-catalog view)**: the deterministic, versioned document listing every declared
  gate with its carried metadata and freshness-key inputs (the F021 projection).
- **`route.json` (per-change view)**: the deterministic, versioned document listing the gates this
  change selected, the route trace, the carried findings, and the cost rollup (the F020 projection).
- **Exit decision**: the process-level outcome category (success, input unavailable, usage error, tool
  error) mapped to a numeric exit code; deliberately does NOT include a governed-blocking verdict in this
  row.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a repository with a valid catalog and a change touching a declared capability surface, a
  single command run writes both `route.json` and `gates.json`, and the selected-gate set in `route.json`
  exactly equals the gates that change's routed domains declare — verified against the projection of the
  same typed inputs.
- **SC-002**: Running the command twice over identical repository inputs produces byte-for-byte identical
  `route.json` and `gates.json` (and identical JSON stdout when requested) across the two runs.
- **SC-003**: The same change can be routed three ways — explicit path list, since-revision, and default
  base/head — and each produces the selected-gate set appropriate to that scope.
- **SC-004**: Each of the four failure categories (not a repository / unavailable git, missing-or-invalid
  catalog, unresolved revision, unwritable output) yields a distinct, descriptive diagnostic and a
  category-appropriate non-zero exit code, with no artifact written for input/usage failures.
- **SC-005**: The persisted artifacts contain none of: a ship/merge verdict, severity, profile, mode,
  enforcement state, cache-eligibility verdict, blockers, warnings, exit-code basis, wall-clock value,
  machine-absolute path, or environment-derived value.
- **SC-006**: A change touching only routine unclassified paths, and a valid empty catalog, each produce
  a successful run with valid artifacts (empty selected-gate set / empty gate list) and a non-error exit.
- **SC-007**: The full composition — scope selection, catalog load, route, select, project, persist,
  summarize, and exit decision — is exercised end-to-end through fakeable git and filesystem boundaries
  without requiring a real git process in the test, and the artifacts it writes match the real
  projections.

## Assumptions

- **Slice boundary**: This row delivers `fsgg route` only. `fsgg ship --mode gate --profile standard
  --json` (the protected-branch **verdict**), `audit.json` (ship verdict, blockers, exit-code basis), and
  the GitHub Actions branch-protection guidance are explicitly **out of scope** — the ship verdict
  depends on Phase-5 enforcement/profiles that do not yet exist, and the design separates `fsgg route`
  (show route) from `fsgg ship` (verdict). They remain later Phase-2 / Phase-5 rows.
- **Reuse, don't re-derive**: The command composes the existing pure cores and isolated sensing edges
  (F014 catalog load+validate, F015 routing, F016 git/CI snapshot sensing, F017 findings, F018 registry,
  F019 selection, F020 route.json, F021 gates.json) verbatim. It adds the composition and the persistence
  edge, not new routing/selection/serialization logic.
- **Output location**: `gates.json` is written as the whole-catalog artifact under the project's `.fsgg`
  area, and `route.json` as the per-change artifact under a generated readiness location. The design's
  canonical per-change path is `readiness/<id>/route.json`, but `<id>` comes from the SDD work-item model
  which does not exist in this Governance-only skeleton; a default deterministic destination is used,
  overridable by the caller. The exact default path and any output-override flag are a plan-time
  reconciliation, not a change to this feature's intent.
- **Project home / command surface**: Whether this `fsgg route` host edge lands as a new Phase-2 host
  project or extends an existing one, and the precise flag spelling (`--paths`, `--since`, `--json`,
  output override), are plan-time reconciliations — consistent with how prior Phase-2 rows deferred
  project home to plan. The note that the repository already contains an older kernel-era
  `FS.GG.Governance.Cli` (route/explain/contract/evidence over the kernel MVU) is a distinct lineage;
  this feature does not assume reuse of it.
- **No cache/freshness evaluation**: Freshness-key inputs are carried into `route.json`/`gates.json` (as
  F020/F021 already do) but no cache-eligibility verdict is computed — that is Phase 11.
- **Determinism inheritance**: Byte-stability of the artifacts comes from reusing the F020/F021
  projections unchanged; this feature must not introduce any nondeterministic value (clock, absolute
  path, environment) into the persisted documents.
- **Boundary discipline**: Because the command performs multi-step external I/O (git sensing, catalog
  file reads, artifact writes), it is a stateful host edge with injected, fakeable ports — not a single
  pure function — consistent with how F016's sensing edge and the kernel host edge are structured.
