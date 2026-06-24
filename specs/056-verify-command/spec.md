# Feature Specification: The `fsgg verify` Host Command

**Feature Branch**: `056-verify-command`

**Created**: 2026-06-24

**Status**: Planned

**Input**: User description: "next item in the plan" — Phase 13 (Release And Distribution Readiness). The
release-rules row landed `fsgg release` as F055, and the Phase-13 row "Define Governance `fsgg verify` and
`fsgg release` schemas and exit codes" is now partial with the note **"`fsgg verify` remains pending."** This
feature is that pending command: the Governance host that runs profile-appropriate product verification before
a PR (or as explicit local validation), runs the selected checks, validates evidence/generated-view currency,
and emits a deterministic verification artifact and a process exit code.

Two scope decisions were confirmed with the requester at specification time (via the clarification step):

1. **`fsgg verify` runs the profile-appropriate selected gates over the local change AND validates
   evidence/generated-view currency.** This is the full design intent for the command — "run selected checks
   and validate generated views/evidence" (`docs/governance-design/speckit-in-the-system.md`) and "run
   profile-appropriate product verification before PR or explicit local validation" (`docs/initial-design.md`).
   The command does not stop at selecting gates (that is `fsgg route`); it executes them and reports currency.
2. **`fsgg verify` uses blocking-capable exit codes, mirroring `fsgg release`/`fsgg ship`.** The process result
   is one of five distinguishable codes — `0` Success, `1` Blocked, `2` UsageError, `3` InputUnavailable,
   `4` ToolError — so a pre-PR CI step can fail early when a blocking-severity check is unmet, while advisory
   findings never block.

## Overview

The Governance command suite can already **select** the gates a change warrants (`fsgg route`), **recompute the
protected merge-boundary verdict** (`fsgg ship --mode gate`), evaluate **cache eligibility** for evidence reuse
(`fsgg cache-eligibility`), and **gate a release** (`fsgg release`). As of F052 the host commands actually
**run** each selected gate through the F051 execution port, capture its evidence, and grow the reuse store.

What is still missing is the command developers run **before** they open a PR: a single `fsgg verify` that, for
a governed repository and the change under development, **selects the profile-appropriate checks**, **runs**
them (reusing fresh prior evidence, recomputing only what is stale), **validates that the change's evidence and
any generated readiness views are current** with respect to their declared sources, **reports** a clear verdict
(human text plus a deterministic `verify.json` artifact), and **exits** with a code that lets a pre-PR check
fail early when a blocking-severity check is unmet.

`fsgg verify` is the local pre-flight, not the merge authority. It tells a developer — quickly, locally, before
they push — whether the change would pass its gates and whether its evidence/views are up to date. The
protected-branch merge verdict remains `fsgg ship`'s responsibility; verify never replaces it. The two may share
evaluation machinery, but they serve different stages: verify is "am I ready to open a PR?", ship is "is this
merge allowed?".

This feature is a new standalone executable, `FS.GG.Governance.VerifyCommand`, built to the exact
pure-core + injected-edge shape of the existing `route`/`ship`/`cache-eligibility`/`release` commands (a pure
MVU `Loop` boundary, an `Interpreter` that binds real ports at the edge, a thin `Program.fs`), plus a
deterministic `verify.json` projection shipped as a separate pure library mirroring the
`AuditJson`/`RouteJson`/`GatesJson`/`ReleaseJson` precedent. It composes the existing selection, execution,
freshness/reuse, and severity cores; it adds no new sensing of the repository beyond what those cores already do.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Verify a change locally before opening a PR (Priority: P1)

A developer working in a governed repository runs `fsgg verify` against their in-progress change. The command
selects the profile-appropriate checks for the paths they touched, runs the ones whose evidence is stale (reusing
the ones whose evidence is still fresh), prints a clear pass/blocked verdict naming the blocking and advisory
findings, and exits non-zero when a blocking-severity check is unmet — so the developer fixes the problem before
pushing, instead of discovering it at the merge boundary.

**Why this priority**: This is the entire reason the command exists and the only slice that delivers end-to-end
value — the first time a developer can run the profile-appropriate verification for their change from one local
command and get a process exit code. Every other story refines or hardens this flow.

**Independent Test**: Point the command at a fixture repository and change whose selected checks all pass and
confirm it prints a passing verdict and exits `0`; point it at a fixture whose change trips a blocking-severity
check and confirm it prints a failing verdict naming that check as the blocker and exits with the distinct
"verification blocked" code (`1`).

**Acceptance Scenarios**:

1. **Given** a governed repository and a change whose profile-appropriate selected checks all pass, **When** a
   developer runs `fsgg verify`, **Then** the command prints a passing verdict listing the checks it ran (or
   reused) and exits `0`.
2. **Given** a change that trips a blocking-severity check, **When** a developer runs `fsgg verify`, **Then** the
   command prints a failing verdict naming the unmet blocking check and exits `1` (Blocked) — a code distinct
   from every failure-to-run code.
3. **Given** a change that trips only an advisory-severity check, **When** a developer runs `fsgg verify`,
   **Then** the command surfaces the advisory finding as a warning, the verdict is still passing, and the command
   exits `0`.
4. **Given** a change that touches no governed path (nothing to verify), **When** a developer runs `fsgg verify`,
   **Then** the command reports that there is nothing to verify and exits `0`.

---

### User Story 2 - Reuse fresh evidence and report what is stale (Priority: P2)

When a developer re-runs `fsgg verify` after changing only part of their work, the command reuses the still-fresh
evidence from selected checks whose inputs did not change and only re-runs the checks whose evidence is now stale.
The verdict report tells the developer, per selected check, whether its evidence was reused (fresh) or recomputed
(stale), and flags any generated readiness view that is out of date relative to its declared sources.

**Why this priority**: This is the payoff that makes local verification fast enough to run often, and it is the
"validate generated views/evidence currency" half of the command's design intent. It builds directly on Story 1
and is independently testable once Story 1 exists.

**Independent Test**: Run `fsgg verify` twice against the same fixture with no change between runs and confirm the
second run reuses every selected check's prior evidence (recomputes none) and reports each as fresh; then change
one selected check's inputs and confirm only that check is recomputed and is reported as stale-then-recomputed.

**Acceptance Scenarios**:

1. **Given** a selected check whose evidence is still fresh (its freshness inputs are unchanged since the prior
   capture), **When** the developer runs `fsgg verify`, **Then** the command reuses the prior evidence, does not
   re-run that check, and reports it as fresh/reused.
2. **Given** a selected check whose freshness inputs changed since the prior capture, **When** the developer runs
   `fsgg verify`, **Then** the command recomputes that check and reports it as stale-then-recomputed.
3. **Given** a generated readiness view that is out of date relative to its declared sources, **When** the
   developer runs `fsgg verify`, **Then** the command flags the view as stale in its currency findings, carrying
   the severity the enforcement dials assign it.

---

### User Story 3 - Deterministic `verify.json` for tooling and pre-PR CI (Priority: P3)

A pre-PR CI step (or another tool) runs `fsgg verify` and consumes a `verify.json` artifact that projects the
verdict, the per-check execution outcomes, the evidence/view currency findings, and the blocking/advisory split.
For identical repository state and identical check outcomes the artifact is byte-for-byte identical, so it can be
diffed, cached, and asserted against a golden baseline; the process exit code lets the CI step block on a
blocking-severity failure.

**Why this priority**: Deterministic machine output is what lets verification be wired into automation and
regression-tested, but it depends on Stories 1–2 producing the verdict it projects.

**Independent Test**: Run `fsgg verify` twice with the requested artifact over a fixture with identical state and
identical check outcomes and confirm the two `verify.json` files are byte-identical; confirm the artifact omits
timestamps, absolute paths, and machine-specific content; confirm the printed machine output equals the persisted
file.

**Acceptance Scenarios**:

1. **Given** identical repository state and identical check outcomes across two runs, **When** `fsgg verify` is
   asked to emit `verify.json` each time, **Then** the two files are byte-for-byte identical.
2. **Given** a request for machine output, **When** `fsgg verify` runs, **Then** the machine output it prints is
   the verbatim content of the persisted `verify.json` (one source of truth).
3. **Given** the artifact is generated, **When** it is inspected, **Then** it carries a versioned schema
   identifier and contains no timestamp, absolute path, username, or other machine-specific content.

---

### Edge Cases

- **No declared configuration / no resolvable profile**: the repository lacks the declared sources `fsgg verify`
  needs to select checks (e.g. absent or malformed governing configuration) ⇒ `InputUnavailable` (exit `3`),
  with a diagnostic that distinguishes missing input from a tool defect. No partial artifact is left behind.
- **Nothing selected**: the change touches no governed path, so no check is selected ⇒ Success (exit `0`) with a
  clear "nothing to verify" report (an empty selection is not an error).
- **A check process fails to start or the tool itself defects** (e.g. an unwritable artifact path, an
  execution-port failure that is a tool defect rather than a check verdict) ⇒ `ToolError` (exit `4`), kept
  distinct from a check that ran and reported a failing verdict (which is Blocked or advisory). No partial
  `verify.json` is left behind on a tool error.
- **A selected check reports `Uncertain`/unrecoverable rather than pass or fail**: it is surfaced as such and is
  never silently coerced to passing; a blocking-severity unmet/uncertain result drives Blocked.
- **Invalid command-line arguments** (unknown flag, missing value, mutually exclusive scope selectors,
  unrecognized profile) ⇒ `UsageError` (exit `2`) with usage guidance; a typo writes no artifact.
- **Advisory-only findings present, no blocking findings**: verdict is passing; exit `0`; advisory findings are
  reported as warnings.
- **Stale generated view or stale evidence**: reported in the currency findings carrying the severity the
  enforcement dials assign; a blocking-severity currency finding contributes to Blocked, an advisory one is a
  warning only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single host command, `fsgg verify`, that takes a governed repository and a
  change scope and produces a verification verdict and a process exit code.
- **FR-002**: The system MUST select the **profile-appropriate** set of checks for the change scope, reusing the
  existing gate-selection/routing core rather than introducing a new selection mechanism.
- **FR-003**: The system MUST **run** each selected check whose evidence is stale, and MUST **reuse** the prior
  captured evidence of each selected check whose evidence is still fresh, reusing the existing
  freshness/reuse/execution cores rather than adding new sensing.
- **FR-004**: The system MUST **validate currency** — reporting, per selected check, whether its evidence was
  reused (fresh) or recomputed (stale), and flagging any generated readiness view that is out of date relative to
  its declared sources.
- **FR-005**: The system MUST partition findings into **blocking** and **advisory** using the established
  enforcement-severity dials, and MUST never silently coerce an uncertain/unrecoverable result into a pass.
- **FR-006**: The system MUST report a verdict in human-readable text by default, and MUST emit a deterministic
  machine artifact (`verify.json`) when requested, projecting the verdict, the per-check execution outcomes, the
  currency findings, and the blocking/advisory split.
- **FR-007**: The machine artifact MUST be **byte-for-byte identical** for identical repository state and
  identical check outcomes, and MUST contain no timestamp, absolute path, username, or other machine-specific
  content; the printed machine output MUST equal the persisted file verbatim (one source of truth).
- **FR-008**: The machine artifact MUST carry a **versioned schema identifier**.
- **FR-009**: The system MUST exit with one of five distinguishable codes: `0` Success (no effective-blocking
  check unmet), `1` Blocked (≥1 effective-blocking check unmet — distinct from every failure-to-run code), `2`
  UsageError (invalid arguments), `3` InputUnavailable (absent/invalid governing inputs the host cannot proceed
  past), `4` ToolError (a genuine tool/IO defect).
- **FR-010**: The system MUST be **fail-safe**: a missing, unreadable, or unexpected input, or a check that
  cannot be evaluated, MUST resolve to an unmet/uncertain finding or a distinct non-success exit — never a
  fabricated passing verdict and never a crash.
- **FR-011**: The system MUST be **product-neutral**: it MUST NOT hardcode any product identity, version, field,
  path, profile name, or check identity; all of these come from the governed repository's declared sources and
  the existing cores.
- **FR-012**: The system MUST treat an **empty selection** (the change touches no governed path) as Success with
  a "nothing to verify" report, not as an error.
- **FR-013**: The system MUST NOT mutate the governed repository except for the explicitly-requested `verify.json`
  artifact (written atomically) and any opt-in persistence of the evidence-reuse store that the shared cores
  already perform; on a tool error it MUST leave no partial artifact behind.
- **FR-014**: The system MUST be **network-free** in its own logic (verifiable by a scope guard), beyond whatever
  the declared checks themselves do when executed.
- **FR-015**: The system MUST accept the same change-scope selectors the existing host commands accept (an
  explicit path list, a since-revision, or a base/head range), reject mutually exclusive selectors as a usage
  error, and apply a documented default scope when none is given.
- **FR-016**: Diagnostics MUST distinguish a missing/malformed **input** from a **tool defect** in both message
  and exit code, and MUST name the offending source so a developer can fix it.
- **FR-017**: `fsgg verify` MUST NOT be presented or used as the protected merge-boundary authority; its verdict
  is advisory-stage (pre-PR/local validation) and does not replace `fsgg ship`. A passing `fsgg verify` does not
  by itself authorize a merge.

### Key Entities *(include if feature involves data)*

- **Verification request**: the governed repository, the change scope (paths / since-revision / base-head), the
  resolved profile, the output format, and the optional `verify.json` output path.
- **Selected check**: a profile-appropriate gate chosen for the change scope (identity, severity, command spec),
  reused from the existing selection core.
- **Check execution outcome**: per selected check — whether it ran, was reused (fresh), or recomputed (stale),
  and its pass/fail/uncertain verdict with evidence reference.
- **Currency finding**: the freshness/staleness status of a selected check's evidence and of any generated
  readiness view, each carrying an enforcement-assigned severity.
- **Verification verdict**: the overall pass/blocked outcome plus the blocking findings, advisory findings, and
  currency findings, each with identity and base/effective severity.
- **`verify.json` artifact**: the deterministic, versioned projection of the verdict, per-check outcomes, and
  currency findings.
- **Exit decision**: the five-way process-result category (Success / Blocked / UsageError / InputUnavailable /
  ToolError).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can verify an in-progress change against its profile-appropriate checks with a single
  command and receive a clear pass/blocked verdict.
- **SC-002**: When a blocking-severity check is unmet, the command exits `1` (Blocked) — a code distinct from
  every failure-to-run code (`2`/`3`/`4`) — so a pre-PR CI step can fail early; when only advisory checks are
  unmet, it exits `0`.
- **SC-003**: A re-run with no change between runs reuses 100% of the prior selected-check evidence and recomputes
  none; changing one check's inputs recomputes only that check.
- **SC-004**: For identical repository state and identical check outcomes, two `verify.json` artifacts are
  byte-for-byte identical, contain no timestamp/absolute-path/machine-specific content, and equal the printed
  machine output verbatim.
- **SC-005**: Every failure mode resolves to the correct one of the five exit codes; no input or evaluation
  failure produces a fabricated passing verdict, and no tool error leaves a partial artifact behind.
- **SC-006**: The command introduces no hardcoded product identity, version, path, profile, or check identity —
  all such values come from the governed repository's declared sources (verifiable by inspection).
- **SC-007**: The command's own logic performs no network access (verifiable by a scope guard).

## Assumptions

- **Next item resolution**: "next item in the plan" is the Phase-13 row "Define Governance `fsgg verify` and
  `fsgg release` schemas and exit codes," whose `fsgg release` half landed as F055 and whose explicitly-named
  pending half is `fsgg verify`. This feature is that command.
- **Composition, not new sensing**: `fsgg verify` composes the already-merged gate-selection/routing, gate
  execution, freshness/evidence-reuse, and enforcement-severity cores (the same ones `fsgg route`/`fsgg ship`
  use). It adds no new repository sensing; "validate generated views/evidence currency" is the existing
  freshness/reuse evaluation surfaced (and acted on) by this command.
- **Run stage**: verify is the pre-PR / local-validation stage. It reports a blocking-capable verdict so a
  developer sees, before pushing, whether the merge gate would block — but it is not the authoritative merge
  boundary; `fsgg ship` remains that (FR-017).
- **Exit-code parity**: the five-way exit-code contract mirrors `fsgg release`/`fsgg ship` (`Success` / `Blocked`
  / `UsageError` / `InputUnavailable` / `ToolError`) so CI usage is consistent across the command suite.
- **Default scope**: when no scope selector is given, verify defaults to the locally-changed set for the
  repository (the same default posture the existing commands use); explicit `--paths` / `--since` / base-head
  selectors override it, and mutually exclusive selectors are a usage error.
- **Currency severity**: a stale generated view or stale evidence is reported as a currency finding carrying the
  severity the enforcement dials assign; a blocking-severity currency finding contributes to Blocked, an advisory
  one is a warning only. Blocked is otherwise driven by blocking-severity check outcomes.
- **Row-local surface, frozen cores untouched**: like F055, any new declaration or projection surface this row
  needs is row-local; the frozen F014 configuration schema and the F051/F052/F053/F054 cores are reused verbatim,
  not edited. (The precise project layout is a planning decision, deferred to `/speckit-plan`.)
- **No central dispatcher**: as with the other host commands, no central `fsgg` dispatcher is assumed or
  introduced; `fsgg verify` is a standalone executable. A leading bare `verify` token is tolerated/handled per
  the existing command precedent.
