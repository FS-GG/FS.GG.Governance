# Feature Specification: GitHub Actions Branch-Protection Guidance for `fsgg ship`

**Feature Branch**: `027-branch-protection-guidance`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved to the last
unstarted Phase-2 row in `docs/initial-implementation-plan.md`: *"Publish the first GitHub Actions
guidance for branch protection."* This is the closing row of the Phase-2 *Governance Ship Walking
Skeleton*. Every pure core and host edge it depends on is merged: the `fsgg ship` protected-branch
command (F026) already turns a routed change into a pass/fail merge **verdict**, writes the
deterministic `audit.json` (F025), and **exits with a numeric code CI can block on** — `Clean → 0`,
`Blocked → 1` (the single code reserved for a blocked merge), and the tool-failure codes `2` (usage
error), `3` (input unavailable), `4` (tool error), each kept distinct from the blocked code. What no
artifact yet does is **tell an adopting project how to wire that exit code into a GitHub protected
branch** so a blocked verdict actually prevents a merge. That wiring guidance is this row.

## Overview

The design names a single protected-branch boundary: *"Run `fsgg ship --mode gate --profile standard
--json` as a protected boundary"* — *"That command becomes the protected-branch gate."* F026 delivered
the command and its blocking exit code; this row delivers the **published, reusable guidance and a
copyable GitHub Actions workflow template** that turn that command into an enforced merge gate on a
GitHub repository.

The deliverable is **documentation plus a workflow template**, not a compiled library or a new pure
core — consistent with the repository's tooling-strategy graduation rule (shell/YAML are allowed for
documentation examples and CI wrappers; they graduate to compiled F# only when they need to own stable
contracts, which the *exit-code and audit.json contracts already do* — and those already live in the
compiled `fsgg ship` command this guidance merely **wires**). This row re-defines, re-derives, and
re-implements **nothing** about the verdict, the audit document, or the exit-code taxonomy: it documents
and demonstrates how to consume the contract F026 fixed.

Concretely, the guidance must show an adopter how to (1) run `fsgg ship --mode gate --profile standard
--json` in a GitHub Actions job against a pull request's base/head, (2) let the command's **exit code**
drive the job's pass/fail status, (3) register that job as a **required status check** in branch
protection so a non-clean result blocks the merge, (4) keep a **blocked merge verdict** (exit `1`)
diagnosably distinct from a **tool failure** (exit `2`/`3`/`4`) while ensuring neither is ever silently
treated as a passing merge (fail-closed), (5) **surface the `audit.json` verdict and its
blockers/warnings/passing partition** to reviewers from the CI run without a local rerun, and (6) state
the honesty boundary the design requires — **protected-branch blocking comes only from the deterministic
verdict; advisory or agent-reviewed findings are reported but never the basis for blocking** until
calibration exists.

The repository currently has **no `.github/` workflows at all**, so this is literally the *first*
GitHub Actions guidance the project publishes. Because `fsgg ship` is not yet a packed tool (the
single-packed-`fsgg`-tool unification that would dispatch the `route` and `ship` verbs is an explicitly
deferred follow-up from F026), the guidance must also address — honestly, without overclaiming a
shipping install path — **how a CI job obtains and invokes the command today** versus the canonical
`fsgg ship` surface the design names.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Make a blocked verdict block the merge (Priority: P1)

A maintainer adopting Governance follows the published guidance and the workflow template to add a CI
job that runs `fsgg ship --mode gate --profile standard --json` on every pull request targeting their
protected branch, then registers that job as a **required status check**. When a pull request contains a
change that selects a gate that is blocking under `--mode gate --profile standard`, the job fails and the
pull request **cannot be merged**; when the change is clean, the job passes and the merge is allowed.

**Why this priority**: This is the whole point of the row and the design's protected-branch acceptance
item — the first time the Phase-2 merge **verdict** becomes an **enforced** merge gate on a real
platform. Without it the F026 blocking exit code has no consumer and the walking skeleton stops one step
short of protecting a branch.

**Independent Test**: Following only the published guidance and template, configure a sandbox repository
so the ship job is a required status check; open one pull request whose change selects a base-blocking
gate and confirm the check is red and merge is blocked; open another with only passing items and confirm
the check is green and merge is allowed.

**Acceptance Scenarios**:

1. **Given** a repository whose protected branch requires the ship status check, **When** a pull request
   selects a gate that is blocking under `--mode gate --profile standard`, **Then** the ship job exits
   non-zero (the blocked code), the required check is failing, and the merge is blocked.
2. **Given** the same repository, **When** a pull request's change selects only advisory/passing items,
   **Then** the ship job exits `0`, the required check passes, and the merge is allowed.
3. **Given** the published guidance, **When** a maintainer follows it end to end, **Then** the steps to
   add the workflow and to mark the check required in branch protection are complete and unambiguous
   enough to enforce the gate without further design decisions.

---

### User Story 2 - Distinguish a blocked merge from a broken tool (Priority: P1)

The CI wiring honors the F026 exit-code taxonomy: a **blocked verdict** (exit `1`) and a **tool failure**
(exit `2` usage error, `3` input unavailable, `4` tool error) both fail the check — and therefore both
block the merge by default — but they remain **separately diagnosable** from the run, and a tool failure
is **never** reported as a passing merge.

**Why this priority**: For a protected-branch gate the distinction is load-bearing and constitutional
(Principle VI, safe failure): CI must block on "the change may not merge" without conflating it with "the
tool broke," and must never read a tool failure as a green merge. A miscategorized exit code is a
governance failure, not a cosmetic one. This is P1 because the gate's credibility depends on it.

**Independent Test**: In a sandbox, trigger each outcome — a blocked verdict, a not-a-git/shallow-context
failure, a missing-or-invalid catalog, and an unrecognized lever — and confirm each makes the check
**fail** (blocking the merge) while the run output and exit code let a reader tell *which* category
occurred, and confirm no scenario yields a green check.

**Acceptance Scenarios**:

1. **Given** a pull request whose verdict is blocked, **When** the job runs, **Then** the check fails
   with the blocked exit code and the run identifies the outcome as a *blocked merge verdict* (not a tool
   error).
2. **Given** a job that cannot sense base/head (e.g. an insufficient checkout) or whose catalog is
   missing/invalid or whose lever is unrecognized, **When** the job runs, **Then** the check fails with
   the corresponding tool-failure code and the run identifies it as a *tool failure*, distinct from a
   blocked verdict.
3. **Given** any tool failure, **When** the job finishes, **Then** the check is never green; a tool
   failure is never surfaced as a passing merge.

---

### User Story 3 - Show reviewers why a merge is blocked (Priority: P2)

When the ship check fails, a reviewer can see **why** directly from the CI run — the `audit.json`
verdict, exit-code basis, and the blockers/warnings/passing partition (each item's identity and base +
effective severity, including any unknown-governed-path findings and any no-hide warnings) are surfaced
from the job, so the reviewer does not have to clone and rerun locally to understand the decision.

**Why this priority**: The audit document is the design's protected-branch readiness artifact
(`readiness/<id>/audit.json` — verdict, blockers, warnings, exit-code basis). Surfacing it closes the
loop from "merge blocked" to "here is exactly what to fix." It is P2 because US1/US2 already enforce the
gate; this makes the enforcement legible.

**Independent Test**: Trigger a blocked pull request and confirm a reviewer can read the verdict and the
blockers/warnings/passing partition from the CI run (an uploaded `audit.json` artifact and/or a rendered
job summary) without running anything locally, and that the surfaced content matches the document the
command wrote.

**Acceptance Scenarios**:

1. **Given** a failed (blocked) ship job, **When** a reviewer opens the run, **Then** the `audit.json`
   verdict and its blockers/warnings/passing partition are available from the run (as an artifact and/or
   a job summary).
2. **Given** a no-hide warning (a base-blocking item relaxed by the profile), **When** the reviewer reads
   the surfaced audit, **Then** that item is visible as a warning carrying both base and effective
   severity — never silently dropped.
3. **Given** the surfaced audit content, **When** compared to the `audit.json` the command wrote, **Then**
   they are the same document (the guidance surfaces it, it does not re-shape it).

---

### User Story 4 - Honest scope: only deterministic verdicts block (Priority: P2)

The guidance states plainly what is and is not allowed to block a protected branch: **the deterministic
`fsgg ship` verdict blocks**; advisory or agent-reviewed findings may be *reported* but are **never** the
basis for protected-branch blocking until calibration exists, and the guidance does not instruct adopters
to gate on uncalibrated agent judgement, on missing-but-optional checks, or on anything outside the
command's deterministic exit code.

**Why this priority**: The design is explicit — *"protected-branch blocking should come from
deterministic checks ... until calibration exists"* and *"Protected-branch blocking does not depend on
uncalibrated agent judgement."* Publishing guidance that quietly violated this would institutionalize
exactly the failure mode the design warns against. P2 because it is a correctness/honesty boundary on the
already-defined gate rather than new enforcement.

**Independent Test**: Read the guidance and confirm it (a) ties branch-protection blocking solely to the
command's deterministic exit code, (b) explicitly excludes uncalibrated agent-reviewed findings from
blocking while allowing them to be reported, and (c) makes no claim of compliance or attestation the
skeleton does not yet produce.

**Acceptance Scenarios**:

1. **Given** the guidance, **When** an adopter reads the blocking model, **Then** it ties blocking solely
   to the deterministic `fsgg ship` exit code and names no other blocking source.
2. **Given** the guidance, **When** it mentions agent-reviewed or advisory findings, **Then** it states
   they are reported, not blocking, until calibration exists.
3. **Given** the guidance, **When** it describes what the gate proves, **Then** it does not overclaim
   provenance/attestation/compliance the Phase-2 skeleton does not yet produce.

---

### Edge Cases

- **Shallow / detached checkout**: GitHub's default checkout may not expose the base ref needed for
  base/head sensing; the guidance must call out the required checkout depth / base-ref fetch so the
  command can sense the change rather than failing as a tool error on every run.
- **Empty change or empty catalog**: a pull request that routes nothing, or a valid empty catalog, rolls
  up to a clean pass — the check is green and the merge is allowed; the gate does not fail closed on "no
  governed change."
- **Tool not yet packaged**: `fsgg ship` is not currently a packed tool; the guidance must show how a CI
  job obtains and invokes the command today and must not present an install path that does not yet exist
  as if it ships.
- **Fork pull requests / restricted token**: surfacing `audit.json` (artifact upload, job summary,
  annotations) may be constrained for fork PRs; the guidance must not require a permission that would make
  the *gate itself* (the pass/fail check) silently skip on forks — blocking must not be bypassable by
  opening from a fork.
- **Re-run determinism**: re-running the check over the same commit yields the same verdict, the same
  exit code, and the same check outcome (inherited from F026/F025 determinism); the gate is not flaky.
- **Required-but-not-run**: if branch protection requires the check but the workflow never runs (path
  filters, disabled workflow), the merge stays blocked pending the check — the guidance must warn against
  configuring path/event filters that let a governed change skip the required check entirely.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The deliverable MUST publish written guidance plus a copyable GitHub Actions workflow
  template that runs `fsgg ship --mode gate --profile standard --json` against a pull request's base/head
  on a protected branch.
- **FR-002**: The guidance MUST instruct the adopter to register the ship job as a **required status
  check** in GitHub branch protection so that a non-clean result blocks the merge.
- **FR-003**: The workflow MUST let the command's process **exit code** determine the job's pass/fail
  status: exit `0` (clean) → passing check / merge allowed; exit `1` (blocked) → failing check / merge
  blocked; exit `2`/`3`/`4` (tool failure) → failing check / merge blocked. It MUST NOT translate, mask,
  or override these codes.
- **FR-004**: The guidance and workflow MUST keep a **blocked merge verdict** (exit `1`) diagnosably
  distinct from a **tool failure** (exit `2`/`3`/`4`) in what the run reports, while both fail the check.
- **FR-005**: The wiring MUST be **fail-closed**: a tool failure MUST NOT be reported as a passing merge,
  and the required check MUST NOT be bypassable for a governed change (e.g. via fork PRs or event/path
  filters that skip the check).
- **FR-006**: The deliverable MUST surface the `audit.json` verdict and its blockers/warnings/passing
  partition (each item's identity and base + effective severity, including unknown-governed-path findings
  and no-hide warnings) to reviewers from the CI run — as an uploaded artifact and/or a rendered job
  summary — without requiring a local rerun.
- **FR-007**: The surfaced audit content MUST be the exact `audit.json` the command wrote; the guidance
  MUST NOT re-derive, re-sort, or re-shape the verdict or its partition.
- **FR-008**: The guidance MUST tie protected-branch **blocking** solely to the deterministic `fsgg ship`
  exit code; it MUST state that advisory or agent-reviewed findings are reported but **not** blocking
  until calibration exists, and MUST NOT instruct adopters to block on uncalibrated agent judgement.
- **FR-009**: The guidance MUST document the **checkout requirements** (sufficient fetch depth / base-ref
  availability) needed for base/head sensing to succeed, so the gate fails on genuine verdicts rather
  than on every run for lack of a base ref.
- **FR-010**: The guidance MUST honestly document **how a CI job obtains and invokes** `fsgg ship` given
  current packaging (the command is not yet a packed tool), and MUST NOT present a not-yet-shipping
  install path as if it were available.
- **FR-011**: The deliverable MUST NOT re-implement or alter the verdict, the `audit.json` document, or
  the exit-code taxonomy; it consumes the contract the merged `fsgg ship` command (F026) and the
  `audit.json` projection (F025) already fixed.
- **FR-012**: The published workflow template MUST be valid GitHub Actions workflow content that an
  adopter can copy with minimal, clearly-marked substitutions (e.g. protected-branch name, invocation
  step), and the guidance MUST identify exactly what the adopter must change.
- **FR-013**: The guidance MUST avoid overclaiming: it MUST NOT assert provenance, attestation, or
  compliance guarantees the Phase-2 skeleton does not yet produce.
- **FR-014**: The result MUST be deterministic in its gate behavior: re-running the check over the same
  commit yields the same verdict, exit code, and check outcome (inherited from F026/F025); the guidance
  MUST NOT introduce a step whose pass/fail depends on wall-clock or environment.

### Key Entities *(include if feature involves data)*

- **Branch-protection guidance**: the published documentation explaining how to enforce the `fsgg ship`
  merge verdict as a required status check — blocking model, exit-code mapping, checkout requirements,
  audit surfacing, invocation, and the honesty boundary.
- **Ship CI workflow template**: a copyable GitHub Actions workflow that runs `fsgg ship --mode gate
  --profile standard --json` on pull requests against a protected branch, lets the exit code drive the
  job status, and surfaces `audit.json`.
- **Exit-code → check-status mapping**: the contract the wiring documents — `0` clean → pass, `1` blocked
  → fail (merge blocked), `2`/`3`/`4` tool failure → fail (merge blocked, distinct from blocked), never a
  false pass. (Defined by F026; this row only wires it.)
- **`audit.json` (whole-change verdict view)**: the deterministic, versioned document (F025) the workflow
  surfaces to reviewers — verdict, exit-code basis, and the blockers/warnings/passing partition with
  per-item identity and six-field enforcement detail.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A maintainer who follows only the published guidance and template can configure a
  repository so that a pull request selecting a base-blocking gate cannot be merged (the required check is
  red) and a clean pull request can be merged (the check is green), with no further design decisions
  required.
- **SC-002**: From a failed ship check, a reader can determine whether the cause was a **blocked merge
  verdict** or a **tool failure** (and which tool-failure category) using only the run's output and exit
  code; no scenario produces a green check for a tool failure.
- **SC-003**: A reviewer can read the verdict and the blockers/warnings/passing partition (with base and
  effective severities, including no-hide warnings and unknown-path findings) from a blocked run without
  cloning or rerunning, and the surfaced content matches the `audit.json` the command wrote.
- **SC-004**: The guidance states the blocking model in terms of the deterministic exit code only and
  explicitly excludes uncalibrated agent-reviewed findings from blocking; an independent reader can
  confirm no non-deterministic or advisory source is configured to block.
- **SC-005**: Re-running the check over the same commit produces the same verdict, exit code, and check
  outcome; the documented gate is not flaky.
- **SC-006**: The published workflow template is valid GitHub Actions content, runs the canonical
  `fsgg ship --mode gate --profile standard --json` invocation, and the guidance names every value an
  adopter must substitute and the checkout depth required for base/head sensing.

## Assumptions

- **Slice boundary — closes Phase 2**: This row delivers the *branch-protection guidance and workflow
  template only*. It adds no new pure core and no new compiled host command — it **wires** the merged
  `fsgg ship` (F026) exit-code/audit contract. Release/provenance attestation references (Release phase)
  and cache/freshness evaluation (Phase 11) remain out of scope, exactly as F024/F025/F026 deferred them.
- **Platform**: The row is named for **GitHub Actions** and GitHub branch protection (required status
  checks); other CI systems are out of scope for this first guidance. ("Publish the *first* GitHub
  Actions guidance for branch protection.")
- **Canonical invocation**: The documented command is the design's canonical
  `fsgg ship --mode gate --profile standard --json`. The exit-code taxonomy it wires is the merged F026
  contract — `Clean → 0`, `Blocked → 1`, usage error `2`, input unavailable `3`, tool error `4`.
- **Invocation surface (plan-time reconciliation)**: Because `fsgg ship` is not yet a packed tool (the
  single-packed-`fsgg`-tool unification is a deferred F026 follow-up), exactly how the workflow obtains
  and runs the command today — build-from-source (`dotnet run`/`dotnet`) versus a future packed-tool
  install presented as a clearly-marked placeholder — is a plan-time reconciliation. The guidance commits
  to documenting it **honestly** (FR-010) either way.
- **Deliverable home (plan-time reconciliation)**: The guidance lands as published documentation (e.g.
  under `docs/` and/or `README`) plus a copyable workflow template (e.g. under `.github/workflows/` or a
  documented examples location); the repository's `.github/` is currently empty. The exact file paths and
  whether the template is a ready-to-run file or a fenced example are plan-time reconciliations.
- **Self-hosting is out of scope (maintainer-confirmed 2026-06-21)**: This row publishes
  *consumer-facing guidance + a copyable workflow template only*. It does **not** add a live workflow that
  gates THIS repository's own `main` branch — this repo develops via standard Spec Kit on `main` and
  `fsgg ship` is not yet packaged, so dogfooding the gate against itself is deferred (not part of this
  slice and not scoped as a named follow-up row here).
- **Testing a documentation/template deliverable**: "Tests" for this row mean the workflow template is
  valid, copyable GitHub Actions content and the documented exit-code/check mapping matches the actual
  F026 command contract (Principle V real-evidence discipline applied to a docs+YAML artifact), rather
  than F#/FsCheck unit tests of a new pure core. The exact validation approach (e.g. YAML/schema lint,
  a contract-cross-check of documented codes against the command) is a plan-time reconciliation.
- **Honesty over completeness**: Per the design, protected-branch blocking is deterministic-only; the
  guidance reports advisory/agent-reviewed findings without making them block, and claims no
  attestation/compliance the skeleton does not produce.
