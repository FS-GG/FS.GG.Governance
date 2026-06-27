# Feature Specification: Fix the Slow Governance Solution Build

**Feature Branch**: `080-fix-slow-solution-build`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "fix the slow governance solution build."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A contributor builds the whole solution with fast feedback (Priority: P1)

A contributor changes one part of the governance codebase and wants to confirm the whole
solution still compiles before pushing. They run the standard one-line build of the full
solution and get a green (or a specific error) back quickly enough to stay in flow, rather
than waiting many minutes and context-switching away.

**Why this priority**: This is the everyday inner loop. The whole-solution build is the
gate every contributor hits before every push; today it is slow enough (a full build was
observed to run well over twenty minutes and had to be abandoned) that people skip it or
build only sub-pieces, which lets cross-project breakage slip in. Restoring a fast
whole-solution build is the single highest-value outcome.

**Independent Test**: From a clean checkout, run the documented full-solution build
command and measure wall-clock time to completion; confirm it finishes within the target
and reports the same success/failure verdict as today.

**Acceptance Scenarios**:

1. **Given** a clean checkout on a standard multi-core developer machine, **When** the
   contributor runs the full-solution build, **Then** it completes within the target time
   (see Success Criteria) with all projects compiled.
2. **Given** a previously built solution with a single small edit, **When** the contributor
   re-runs the build, **Then** the incremental build completes near-instantly (only the
   affected projects and their dependents rebuild).
3. **Given** an introduced compile error in one project, **When** the contributor runs the
   build, **Then** the failure is reported in the same form and detail as today, just
   sooner.

---

### User Story 2 - The full test suite is runnable as a delivery gate (Priority: P2)

A contributor or maintainer finishing a feature wants to run the **entire** test suite to
completion as the final delivery check — the step the team's process calls for — instead
of recording it as "skipped because the build/suite is too slow to finish here."

**Why this priority**: Recent feature deliveries have repeatedly had to mark the
full-solution verification step as skipped because the solution build alone does not finish
in a reasonable time. That erodes the value of the test suite as a regression gate. Once the
build is fast, running the whole suite to completion becomes practical again.

**Independent Test**: Run the full test suite for the whole solution end-to-end and confirm
it reaches completion (every test project executes and reports) within a single standard CI
job's time budget, with no projects manually excluded to make it finish.

**Acceptance Scenarios**:

1. **Given** the whole solution, **When** a contributor runs the full test suite, **Then**
   every test project executes and the run reaches a final pass/fail summary within the
   target time, without hand-excluding projects.
2. **Given** a feature is being delivered, **When** the delivery verification step runs the
   full suite, **Then** it can be honestly marked complete (executed to the end), not
   "skipped due to build/suite time."

---

### User Story 3 - Speed is gained without losing coverage or correctness (Priority: P3)

A maintainer reviewing the change wants assurance that the build got faster by working more
efficiently — not by dropping projects from the solution, weakening what is compiled, or
changing what the build produces.

**Why this priority**: A "fast" build that silently builds or tests less is worse than a
slow honest one. This story guards the fix against gaming the metric.

**Independent Test**: Compare the set of projects built and the produced outputs before and
after the change; confirm the same projects are present in the solution and their build
outputs are functionally equivalent.

**Acceptance Scenarios**:

1. **Given** the solution before and after the change, **When** the built project set is
   compared, **Then** no project has been removed from the solution to gain speed.
2. **Given** a successful build before and after, **When** the produced outputs are
   compared, **Then** they are functionally equivalent (same assemblies and observable
   behavior); the change alters how fast the build runs, not what it produces.

---

### Edge Cases

- **Resource-constrained runners**: On a CI container or machine with few cores or limited
  memory, the build should still complete and degrade gracefully (slower, but not hang or
  fail) rather than only meeting the target on a large workstation.
- **Clean vs. incremental**: The target must distinguish a clean from-scratch build from a
  no-op incremental rebuild; a no-op rebuild should be near-instant, not a full recompile.
- **Single pathological project or test**: If a specific project or a specific long-running
  integration test (e.g., the documented SDD template-generation integration test) dominates
  the time, it should be identified and isolated so it does not block the whole-solution
  result; whether to also speed up that individual item is a separate concern from the
  whole-solution build target.
- **First-run restore**: A first run that must restore dependencies will be slower than
  subsequent runs; the target applies to the build step given a warm/restored package cache.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The full-solution build, invoked through the project's standard documented
  command, MUST complete within the target wall-clock time (see Success Criteria) on a
  standard multi-core developer machine.
- **FR-002**: The build MUST continue to compile every project currently in the solution;
  speed MUST NOT be obtained by removing projects from the solution or excluding them from
  the build.
- **FR-003**: A successful build MUST produce functionally equivalent outputs to today's
  build (same assemblies and observable behavior) — the change affects build duration, not
  build product.
- **FR-004**: The full test suite for the whole solution MUST be runnable to completion as a
  single delivery-gate step, without manually excluding test projects to make it finish.
- **FR-005**: The improvement MUST apply to both a clean from-scratch build and an
  incremental rebuild, with a no-op incremental rebuild completing near-instantly.
- **FR-006**: Build performance MUST scale with available hardware — faster on machines with
  more CPU cores — and MUST make effective use of the cores available rather than running
  effectively single-threaded.
- **FR-007**: The faster build MUST be achievable with the existing standard command and a
  normal checkout; it MUST NOT require per-developer manual machine tuning or undocumented
  local setup.
- **FR-008**: Any build configuration introduced to achieve the target MUST be checked into
  the repository and apply uniformly to every contributor and CI, not depend on per-machine
  environment variables or local overrides.
- **FR-009**: The build's correctness signal MUST be preserved — a real compile error MUST
  still fail the build and be reported with the same form and detail as today.
- **FR-010**: If a small number of individual projects or tests are found to dominate the
  time, they MUST be identified (and, where they cannot be sped up within this feature,
  explicitly isolated and documented) so the whole-solution build/suite target is still met.

### Non-Functional Requirements

- **NFR-001**: The change MUST be observable/measurable — the before/after build time and
  the contributing factors MUST be captured so the improvement is demonstrable, not
  anecdotal.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a standard multi-core (8+ core) developer machine with a warm package
  cache, a clean from-scratch build of the full solution completes in **under 5 minutes**
  (down from the current baseline of well over 20 minutes for the build alone).
- **SC-002**: A no-op incremental rebuild of the full solution (nothing changed) completes
  in **under 30 seconds**.
- **SC-003**: The full test suite for the whole solution runs to completion (every test
  project executes and a final summary is produced) within **a single standard CI job time
  budget (target under 20 minutes total, build + test)**, with **zero** test projects
  manually excluded to make it finish.
- **SC-004**: The set of projects in the solution is **unchanged or larger** after the fix
  (no project dropped to gain speed), and build outputs remain functionally equivalent.
- **SC-005**: Feature deliveries no longer record the full-solution verification step as
  "skipped due to build/suite time" — the step can be run to completion as part of normal
  delivery.
- **SC-006**: The whole-solution build is at least **4× faster** than the current baseline
  on the same hardware, demonstrating the improvement is structural rather than incidental.

## Assumptions

- **Baseline**: The solution contains ~162 projects (~79 production + ~82 test + 1 sample),
  built with the F#/.NET toolchain. A full `build` of the whole solution was observed to run
  for **over 22 minutes without completing** and had to be cancelled; the guard suite for a
  single feature, by contrast, runs in tens of milliseconds. The slowness is therefore in the
  whole-solution build fan-out, not in any single test's logic.
- **Likely dominant cost (to be confirmed in planning)**: per-project compiler invocation
  overhead across ~162 projects and/or under-utilised parallelism, rather than one
  pathological project — diagnosis is a planning-phase activity, not assumed here.
- **Target environment**: "Standard developer machine" means a modern multi-core (8+ logical
  cores) workstation or CI runner with a warm NuGet/package cache; absolute times are stated
  for that class of machine. Heavily constrained sandboxes may be proportionally slower while
  still benefiting from the same structural improvement.
- **Out of scope**: Reducing test coverage, deleting or merging projects purely to shrink the
  count (unless planning finds consolidation is the right structural fix and it preserves
  coverage), and separately speeding up the internals of the one documented
  pathologically-slow SDD template-generation integration test (its isolation so it does not
  block the suite is in scope; rewriting it is not).
- **Standard command**: Contributors and CI build/test the solution through the existing
  documented commands; this feature improves their speed, it does not introduce a new bespoke
  build tool that contributors must learn.
- **Correctness gate**: The existing tests and build are the correctness oracle; this feature
  must leave that signal intact while making it fast enough to actually run.
