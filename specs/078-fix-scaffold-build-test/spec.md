# Feature Specification: Bound the scaffold real-evidence build test so the suite never hangs and the routine run stays fast

**Feature Branch**: `078-fix-scaffold-build-test`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "fix the pathological slow fs-gg-fullstack integrattion test."

## Context (why this feature exists)

> **Naming note.** The "fs-gg-fullstack" label in the 077 delivery summary was a
> *misdiagnosis*: a `dotnet new fs-gg-fullstack` process observed during that run
> belonged to an unrelated session, not to this repository (the string `fullstack`
> appears nowhere in the source). The actual pathologically-slow test is the
> SDD reference-provider **worked-example real-evidence build** introduced by feature
> 072 (`sdd-first-class-integration`): the test
> *"emitted skeleton `dotnet build`s first-attempt (real evidence)"* in
> `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/WorkedExampleTests.fs`,
> which shells out a `dotnet build` of a freshly-scaffolded `MyApp.sln` through
> `Support.dotnetBuild`.

That real-evidence build step has three compounding pathologies, all observed when
the full `dotnet test FS.GG.Governance.sln` run stalled for 25+ minutes and had to be
killed during the 077 delivery:

1. **Unbounded wait — can hang the whole suite forever.** `Support.dotnetBuild`
   captures the subprocess output and calls `WaitForExit()` with **no timeout**. If
   the nested `dotnet build` stalls — a contended NuGet lock, a cold restore waiting
   on the network, or the resource exhaustion below — the test waits indefinitely and
   the entire suite never finishes. This is the headline pathology: a single test can
   wedge CI and the local inner loop.
2. **Cold, full restore + compile every run.** Each invocation scaffolds a brand-new
   solution into a unique temp directory and builds it from scratch, so every run pays
   a full NuGet restore and a from-scratch F# compile (minutes), with no reuse of a
   warm package cache or prior build output.
3. **Resource contention when nested inside the solution test run.** Because it runs
   as a child `dotnet build` *inside* `dotnet test <solution>`, it spawns its own fan
   of MSBuild worker processes that contend with the parent run for CPU and process
   handles — producing the `Resource temporarily unavailable` / fork-exhaustion errors
   seen during 077, which further slow or wedge the build.

The cost is paid on **every** routine run even though the build step is heavyweight
real evidence that rarely needs to run in the fast inner loop. The fix is to make the
build step **bounded** (never an indefinite hang), keep the **routine run fast** (the
heavyweight build must not block the default suite), and **preserve the real-evidence
guarantee** (when the build does run, it still proves the emitted skeleton compiles
first-attempt, and a genuine compile failure still fails). The scaffold-correctness and
golden/determinism tests in the same file stay fast and unchanged.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The suite never hangs on the scaffold build (Priority: P1)

A developer or CI agent runs the repository test suite. The real-evidence build step
either completes within a bounded time budget or is cut off and reported as a **named
skip** with actionable detail — it **never** waits indefinitely on a stuck or contended
`dotnet build`. The suite always terminates.

**Why this priority**: This is the headline pathology — an unbounded subprocess wait
that wedged a 25-minute run. Bounding it restores the basic guarantee that the suite
finishes, which everything else depends on. Independently shippable: fixing only this
already removes the hang.

**Independent Test**: Run the worked-example test project under a forced-stall
condition (a build that would not return within the budget) and confirm the test ends
within the configured budget with a named, skipped outcome rather than hanging.

**Acceptance Scenarios**:

1. **Given** the real-evidence build subprocess does not return within the configured
   time budget, **When** the budget elapses, **Then** the test terminates the build
   process (and its descendants), ends within the budget plus a small margin, and
   reports a named skip stating the budget was exceeded — the suite continues and
   finishes.
2. **Given** a normal machine where the build returns well within the budget, **When**
   the test runs, **Then** it completes with its real outcome (pass or fail) and no
   skip is emitted.
3. **Given** the build step runs nested inside the full-solution test run, **When** it
   executes, **Then** it does not exhaust process/handle resources in a way that wedges
   the surrounding suite.

---

### User Story 2 - The routine run stays fast (Priority: P2)

A developer running the default/inner-loop suite gets a prompt result. The heavyweight
real-evidence build does not bog down the routine run: it is either fast enough to stay
within the default time budget or gated behind an explicit real-evidence opt-in so the
default run skips it with a named rationale. The scaffold-correctness and
golden/determinism tests still run on every default run.

**Why this priority**: The slowness (not just the hang) is the user's stated complaint.
Keeping the default run fast preserves inner-loop velocity, while the real-evidence
build still runs where it matters (CI / explicit opt-in). Depends on US1's bounding
being in place.

**Independent Test**: Run the worked-example test project in the default configuration
and confirm it completes within the fast budget; run it in the real-evidence
configuration and confirm the build actually executes.

**Acceptance Scenarios**:

1. **Given** the default (non-opt-in) configuration, **When** the worked-example test
   project runs, **Then** it completes within the fast default budget, and any
   not-run heavyweight build is reported as a named skip (never silently absent).
2. **Given** the scaffold-correctness test and the golden/determinism test, **When** the
   project runs in any configuration, **Then** both run and pass on every routine run
   (they are fast and do not shell out a build).
3. **Given** the real-evidence configuration (CI / explicit opt-in), **When** the
   project runs, **Then** the build step actually executes and is bounded per US1.

---

### User Story 3 - The real-evidence guarantee is preserved (Priority: P3)

When the real-evidence build runs, it still proves the emitted skeleton builds
first-attempt with no hand-editing, and a genuine compile failure still fails the
suite. The bounding and gating must not weaken the evidence the 072 feature established;
they only prevent indefinite hangs and routine-run slowness.

**Why this priority**: The build step exists to give real (not synthetic) evidence that
the scaffolded skeleton compiles (072 FR-004 / SC-002). The fix must not silently turn a
real failure into a pass. Lower priority because it constrains how US1/US2 are
implemented rather than adding new user-visible behavior.

**Independent Test**: With the SDK present and the real-evidence configuration active,
run against a deliberately-broken skeleton and confirm the test fails (not skipped);
run against the correct skeleton and confirm it passes first-attempt.

**Acceptance Scenarios**:

1. **Given** the SDK is present and the build runs within budget, **When** the emitted
   skeleton compiles, **Then** the test passes on the first build attempt (exit 0).
2. **Given** the SDK is present and the build runs within budget, **When** the emitted
   skeleton does **not** compile, **Then** the test fails with the captured build output
   — a real regression is never masked by a timeout or SDK-missing skip.
3. **Given** the .NET SDK is absent, **When** the test runs, **Then** it reports the
   existing named missing-SDK skip (unchanged), distinct from the timeout skip.

---

### Edge Cases

- **Build hangs / never returns** (NuGet lock, network-blocked restore, deadlock):
  bounded by the time budget → named timeout skip, never an indefinite hang.
- **Build is merely slow but finishes just under the budget**: completes with its real
  outcome; no skip.
- **Build subprocess spawns children** (MSBuild workers, restore): on timeout the whole
  process tree is terminated, so no orphaned workers keep consuming resources after the
  skip (the 90-minute orphan seen during 077 must not be possible from this test).
- **SDK absent**: existing named missing-SDK skip preserved and kept distinct from the
  timeout skip.
- **Genuine first-attempt build failure**: still a real test failure — not absorbed by
  the timeout or SDK-missing skip paths.
- **Default vs real-evidence configuration**: a not-run heavyweight build is always a
  *named* skip, never silently green and never silently absent (a reader can always tell
  "passed" from "skipped: why" from "failed").
- **Golden stability**: the scaffold manifest golden and the two non-build worked-example
  tests stay byte-identical and behavior-unchanged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The real-evidence build step MUST be bounded by an explicit, finite time
  budget; it MUST NOT wait indefinitely on the build subprocess.
- **FR-002**: When the time budget is exceeded, the step MUST terminate the build
  subprocess **and all of its descendant processes**, end promptly, and degrade to a
  **named skip** carrying actionable detail (at least the budget that was exceeded),
  distinguishable from a genuine build failure and from the missing-SDK skip — never an
  indefinite hang and never a silent pass (Constitution Principle VI).
- **FR-003**: A genuine first-attempt build failure (SDK present, build returns a
  non-zero exit within budget) MUST still **fail** the test; the bounding/gating MUST NOT
  mask real compile regressions (preserves 072 FR-004 / SC-002).
- **FR-004**: The existing missing-SDK named-skip behavior MUST be preserved and remain
  distinguishable from the new timeout skip.
- **FR-005**: The default/inner-loop suite run MUST NOT be blocked or substantially
  slowed by the heavyweight real-evidence build — it MUST either run fast enough to stay
  within the default budget or be gated behind an explicit, discoverable real-evidence
  opt-in (so the default run skips it with a named rationale). The real-evidence build
  MUST still run in the canonical full-evidence (CI) configuration so the 072 guarantee
  is exercised on the authoritative path.
- **FR-006**: The scaffold-correctness test and the golden/determinism test in the same
  worked-example suite MUST continue to run and pass on **every** routine run and MUST
  remain fast (they perform no subprocess build).
- **FR-007**: When the real-evidence build runs, it MUST avoid the resource-contention
  pathology (uncontrolled parallel build/process fan-out) that wedges or errors the
  surrounding suite, so a real-evidence run completes within a bounded, predictable time.
- **FR-008**: The committed scaffold manifest golden and the observable behavior of the
  two non-build worked-example tests MUST remain byte-identical after the change (no
  golden re-bless).
- **FR-009**: Every not-run or cut-off outcome MUST be **observable**: a skip (timeout,
  missing SDK, or opt-out) MUST state which condition caused it, so a reader can always
  distinguish "passed" from "skipped — because X" from "failed."
- **FR-010**: The bounding/gating mechanism MUST be deterministic and self-contained to
  the test support (no reliance on a wall-clock-dependent or network-dependent outcome
  for the pass/fail/skip decision beyond the build itself), so the test's own
  classification is reproducible.

### Key Entities *(include if feature involves data)*

- **Real-evidence build attempt**: the outcome of trying to build the emitted skeleton —
  one of *built (with exit code + captured output)*, *SDK missing (named skip)*, or, new,
  *timed out (named skip, process tree terminated)*.
- **Time budget**: the finite maximum the build step may consume before it is cut off and
  classified as a timeout skip.
- **Run configuration**: whether the current run is the fast default (heavyweight build
  gated off, named skip) or the real-evidence configuration (build runs, bounded).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The full repository test suite always terminates; no run hangs indefinitely
  on the scaffold build step — a stuck build is cut off within the configured budget plus
  a small margin, in 100% of runs.
- **SC-002**: In the default run configuration, the worked-example test project completes
  in under 30 seconds (scaffold-correctness + golden/determinism tests, with the
  heavyweight build skipped or bounded), versus the prior multi-minute / unbounded
  behavior.
- **SC-003**: When the real-evidence build runs with the SDK present, a correct skeleton
  passes first-attempt (exit 0) and a broken skeleton fails — the real-evidence guarantee
  is intact and a real failure is never converted to a skip or pass.
- **SC-004**: Every timeout, missing-SDK, or opt-out outcome is reported as a named skip
  with actionable detail; zero outcomes are silently green and zero are indefinite hangs.
- **SC-005**: No golden/transcript drift: the scaffold manifest golden and the two
  non-build worked-example tests are byte-identical, and the rest of the suite's
  per-project test counts are unchanged except for additive skip/configuration deltas.

## Assumptions

- **Real test identified.** The pathological test is the worked-example real-evidence
  build in `FS.GG.Governance.Sample.SddReferenceProvider.Tests` (feature 072), not a
  "fs-gg-fullstack" test (which does not exist in this repository). The fix is scoped to
  that test and its `Support.dotnetBuild` runner; no other suite is in scope.
- **Default = fast, opt-in = real evidence.** Absent a contrary instruction, the routine
  default run keeps the heavyweight build **off** (named skip) for inner-loop speed, and
  the real-evidence build runs under an explicit opt-in / CI configuration. The
  alternative — keep the build always-on but strictly time-bounded — also satisfies US1
  and US3; it is rejected as the default only because it still pays minutes on every
  routine run (US2). The opt-in lane is the assumed default; the plan may revisit this.
- **Time budget default.** A real-evidence build budget on the order of ~120 seconds is a
  reasonable default ceiling (a from-scratch restore + compile of a tiny skeleton on a
  warm cache completes well within this); the exact value is a plan-level dial. The
  binding requirement is that the budget is finite and enforced (FR-001/FR-002).
- **Real-evidence preservation.** The 072 real-evidence guarantee (SC-002 of 072) remains
  in force on the authoritative CI path; gating only moves *where* it runs, not *whether*
  it runs.
- **No production behavior change.** Only test/test-support code and run configuration are
  affected; the scaffold seam, the SDD reference provider, and all shipped surfaces are
  untouched. This is consistent with a small, low-risk change classification.
- **Cross-project parity.** This mirrors the precedent of other heavyweight real-evidence
  build/pack tests in the repository (e.g. the CLI `dotnet pack` packaging test and the
  release `RealPackTests`) that are already known to be slow/flaky under contention; this
  feature addresses the worst offender and may inform the same treatment for those.
