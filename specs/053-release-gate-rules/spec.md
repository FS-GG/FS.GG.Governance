# Feature Specification: Pure Release-Gate Readiness Rules Core

**Feature Branch**: `053-release-gate-rules`

**Created**: 2026-06-24

**Status**: Draft

**Input**: User description: "next item in the plan." — resolved (maintainer-confirmed this session, via AskUserQuestion) to the first row of the **Governance-owned `fsgg release` gate** thread (`docs/initial-implementation-plan.md`, Phase 13: *"Define Governance `fsgg verify` and `fsgg release` schemas and exit codes"* and *"Add release rules for version bumps, package metadata, template pins, publish plans, trusted publishing, and provenance"*). The cache-eligibility / evidence-reuse thread (Phase 11, F029–F052) is now complete end-to-end; the release gate is the next open Governance thread. Following the repo's pure-core-first rhythm, this row delivers **only the pure rule-evaluation core** — given declared release rules and the release facts they govern (the facts are **provided as input**, not sensed here), produce a deterministic per-rule finding and a release verdict, reusing the **existing** enforcement and verdict machinery verbatim. The impure halves — sensing the real facts from a repository, the `fsgg release` host command, and the `release.json` document projection — are **following rows and are out of scope here**.

## Context

Every release-shaped primitive the gate needs already exists in the merged model but is **unpopulated**: the enforcement core (F023) already recognizes the `Release` run mode and `Release` profile and derives effective severity through them; the config model (F014) already carries the `BlockOnRelease` maturity level, the `ReleaseSurface` surface class, and the `Release` environment class; the ship core (F024) already rolls a list of enforced items into a `Verdict` (Pass/Fail), a disjoint Blockers/Warnings/Passing partition, and a typed exit-code basis. What no row has yet supplied is the **release-specific rule vocabulary** and the pure evaluation that turns declared release expectations plus their governing facts into findings the existing enforcement machinery can roll up.

This row is that pure core. It introduces the closed set of release rule kinds the roadmap names — **version bump, package metadata, template pins, publish plan, trusted publishing, and provenance** — and a single pure evaluation: for each declared rule, compare it against the provided fact, emit exactly one finding (satisfied or violated, with a self-explaining reason and the rule's declared base severity), then roll the findings up into a release verdict and exit-code basis by reusing F023's effective-severity derivation and F024's partition rule **unchanged**. The core is pure, total, and deterministic: facts in, findings and verdict out, no I/O, no process, no document. Because the verdict reuses the existing levers, a release that violates a blocking rule fails and explains exactly why, an advisory violation warns without blocking, and no satisfied or relaxed rule is ever hidden.

The pure core is the first of several release-gate rows. The fact **sensing** (reading the real version, package metadata, template pins, publish plan, publishing configuration, and provenance from a governed repository) is the next row; the `fsgg release` **host command** that wires sensing → this core → enforcement → exit code, and the additive `release.json` **projection**, are following rows — exactly the cadence the cache thread followed (pure decision core → projection → host wiring).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Evaluate declared release rules against provided facts (Priority: P1)

A release author (or the future `fsgg release` host) has a set of declared release rules — one per release expectation the project must meet — and the facts describing the current state of the release. They evaluate the rules against the facts and get back exactly one finding per declared rule, each saying whether the rule is **satisfied** or **violated** and why, with the rule's declared base severity attached.

**Why this priority**: This is the irreducible core of the release gate. Without a deterministic rule-to-finding evaluation there is nothing for any verdict, projection, or host command to consume. It is independently valuable as the answer to "which release expectations are met and which are not."

**Independent Test**: Construct a rule set covering each release rule kind plus a matching facts value, evaluate, and assert one finding per rule with the correct satisfied/violated classification and reason. No I/O, no host command.

**Acceptance Scenarios**:

1. **Given** a declared rule of each kind (version bump, package metadata, template pins, publish plan, trusted publishing, provenance) and a facts value where every governing fact is met, **When** the rules are evaluated, **Then** every rule produces a finding classified **satisfied** with a reason naming the rule, and the finding count equals the rule count.
2. **Given** a declared blocking rule (e.g. "version bump present") and a facts value where that fact is **not** met, **When** the rules are evaluated, **Then** that rule produces a **violated** finding carrying its declared base severity and a reason that names the unmet expectation.
3. **Given** a declared rule whose governing fact is **absent / unrecoverable** in the provided facts, **When** the rules are evaluated, **Then** the rule produces a **violated** finding (fail-safe), never a silently satisfied one.

---

### User Story 2 - Roll findings up into a release verdict and exit-code basis (Priority: P2)

Given the per-rule findings, a consumer needs a single whole-release answer: does the release pass or fail, what is the exit-code basis, and which findings are blockers, which are warnings, and which passed — derived from the **existing** enforcement levers under the release mode and profile, not a new scheme.

**Why this priority**: The verdict is what a protected-boundary gate ultimately enforces, but it is mechanically a reuse of F023/F024 over the US1 findings, so it layers on top of the P1 evaluation rather than standing alone.

**Independent Test**: Feed a mixed finding set (some blocking violations, some advisory violations, some satisfied) through the rollup and assert the verdict, the exit-code basis, and the disjoint Blockers/Warnings/Passing partition.

**Acceptance Scenarios**:

1. **Given** a finding set containing at least one **violated** rule whose effective severity is blocking under the release mode/profile, **When** the findings are rolled up, **Then** the verdict is **Fail** and the exit-code basis reflects a blocked release.
2. **Given** a finding set where every rule is **satisfied**, **When** the findings are rolled up, **Then** the verdict is **Pass**, the exit-code basis reflects a clean release, and the Blockers set is empty.
3. **Given** a finding set containing a violated rule whose declared maturity relaxes it below blocking (advisory), **When** the findings are rolled up, **Then** the verdict is **Pass** (no blockers) but the violated rule is visibly present as a **Warning**, never dropped.

---

### User Story 3 - No-hide visibility and determinism (Priority: P3)

A maintainer auditing release decisions must trust that the gate hides nothing and is reproducible: every declared rule appears in the output exactly once (satisfied rules included), and two evaluations over identical input produce byte-identical results.

**Why this priority**: These are the integrity guarantees that make the gate auditable, but they are properties of the P1/P2 behavior rather than a separable feature.

**Independent Test**: Evaluate the same rule set + facts twice and assert byte-identical findings and verdict; assert the output rule-kind multiset equals the declared rule-kind multiset (no drops, no fabrications).

**Acceptance Scenarios**:

1. **Given** any declared rule set and facts, **When** the rules are evaluated twice, **Then** the two finding lists and the two verdicts are byte-identical.
2. **Given** a rule set mixing satisfied and violated rules, **When** the rules are evaluated, **Then** the satisfied rules are present in the output (as passing/satisfied findings) — never silently omitted — and no finding exists that does not correspond to a declared rule.

---

### Edge Cases

- **Empty rule set**: evaluating zero rules yields zero findings and a **Pass** verdict with a clean exit-code basis (a release with nothing to check is trivially ready), deterministically.
- **Absent governing fact**: a rule whose fact is missing or unrecoverable is **violated** (fail-safe), distinguishing "no recoverable evidence" from "satisfied" — never treated as passed.
- **Duplicate rules of the same kind**: each declared rule is evaluated and reported independently (the output is per declared rule, not per rule kind), so duplicates each yield their own finding.
- **Extra / unrecognized facts**: facts not governed by any declared rule are ignored and never invent a finding.
- **All-advisory violations**: every violation relaxed to advisory by its declared maturity yields a **Pass** verdict with the violations visible as warnings.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The core MUST evaluate each declared release rule against the provided release facts and emit **exactly one** finding per declared rule, classified **satisfied** or **violated**, with a self-explaining reason.
- **FR-002**: The core MUST recognize the closed set of release rule kinds named by the roadmap: **version bump, package metadata, template pins, publish plan, trusted publishing, and provenance**. (Additional kinds, if introduced later, extend this set additively.)
- **FR-003**: Each finding MUST carry the rule's **declared base severity / maturity**, and effective severity MUST be derived through the **existing** enforcement machinery (F023) under the `Release` run mode and `Release` profile — no new severity scheme, mode, or profile is introduced.
- **FR-004**: The core MUST roll the findings up into a release **verdict** (Pass / Fail) and a typed **exit-code basis**, where the verdict is **Fail** if and only if at least one finding is a blocking violation. The partition re-applies the F024 (base severity, effective severity) rule **unchanged to the violated findings**, with **satisfied** findings short-circuiting to **Passing** (F024 has no satisfied/violated concept, so a satisfied rule is never a concern regardless of its severity); the F024 `Verdict`/`ExitCodeBasis` result vocabulary is reused verbatim.
- **FR-005**: A rule whose governing fact is **absent or unrecoverable** in the provided facts MUST yield a **violated** finding (fail-safe); the core MUST NOT treat a missing fact as satisfied.
- **FR-006**: The core MUST NOT drop any declared rule from its output (no-hide); **satisfied** rules and rules relaxed below blocking MUST remain visible in the findings and partition.
- **FR-007**: The core MUST be **pure, total, and deterministic** — facts in, findings and verdict out — performing no I/O and producing **byte-identical** output across repeated evaluations over identical input.
- **FR-008**: The core MUST NOT sense facts from a repository, spawn any process, or emit a JSON/document artifact; release facts are **supplied as typed input** (sensing, the `fsgg release` host command, and the `release.json` projection are following rows).
- **FR-009**: The core MUST NOT edit, relax, or duplicate any frozen core — the F023 `Enforcement` decision algebra, the F024 `Ship` verdict rollup, or the F014 `Config` model — and MUST reuse their existing release primitives (`Release` mode/profile, `BlockOnRelease` maturity, `ReleaseSurface` / `Release` environment classes) rather than redefining them.
- **FR-010**: Whether a violated rule blocks or merely warns MUST be governed **solely** by its declared maturity/severity through the existing enforcement levers, so a release author can relax a rule to advisory without changing the rule's truth (its satisfied/violated classification) or its visibility.

### Key Entities

- **Release rule kind**: the closed enumeration of release expectation families this core recognizes — version bump, package metadata, template pins, publish plan, trusted publishing, provenance.
- **Release rule (declared)**: a single declared expectation — a rule kind, the identity/surface it governs, and its declared severity/maturity. Provided as input.
- **Release facts**: the provided, typed description of the current release state against which rules are evaluated (e.g. whether a version bump is present, package metadata is complete, template pins are resolved, a publish plan is declared, trusted publishing is configured, provenance is present). Provided as input; sensing is a later row.
- **Release finding**: the per-rule outcome — the rule kind and governed identity, a satisfied/violated classification, the declared base severity, and a reason. One per declared rule.
- **Release verdict / decision**: the whole-release rollup — Pass/Fail, the disjoint Blockers/Warnings/Passing partition of findings, and the typed exit-code basis, derived by reusing the existing enforcement and ship machinery.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any declared rule set, the number of findings produced equals the number of declared rules (one-in, one-out) — 100% of rules accounted for, none dropped or fabricated.
- **SC-002**: A rule set containing at least one blocking violation yields a **Fail** verdict with a blocked exit-code basis in 100% of cases; a rule set with no blocking violations yields a **Pass** verdict with a clean exit-code basis.
- **SC-003**: Two evaluations of the same rule set over the same facts produce **byte-identical** findings and verdict (full determinism), verified by repeated evaluation in test.
- **SC-004**: Every test exercises the core with **no network access, no governed repository, and no process spawn** — facts are supplied as in-memory input (the carried-forward real-evidence-without-I/O discipline, SC-007 of prior rows).
- **SC-005**: A rule whose governing fact is absent or unrecoverable never produces a satisfied finding — provable by a fixture in which the fact is missing and the resulting finding is violated.
- **SC-006**: Relaxing a violated rule to advisory (via its declared maturity) changes only its effective severity and the verdict's blocker count — never its satisfied/violated truth and never its presence in the output (no-hide), provable by a paired fixture.

## Assumptions

- **Facts are provided, not sensed.** Release facts are supplied as typed input so the core stays pure; reading the real version/package/pins/publish/publishing/provenance state from a governed repository is the **next** row.
- **The rule-kind set mirrors the roadmap's named families** (version bump, package metadata, template pins, publish plan, trusted publishing, provenance). If the config schema later declares additional release rule kinds, the enumeration extends additively without changing existing behavior.
- **The verdict reuses existing enforcement.** The release verdict, partition, effective severity, and exit-code basis are produced by reusing the merged F023 `Enforcement` and F024 `Ship` machinery under the existing `Release` mode/profile and `BlockOnRelease` maturity — this row introduces **no** new severity scheme, mode, or profile.
- **Projection and host command are separate following rows.** The additive `release.json` document projection and the `fsgg release` host command (which wires sensing → this core → exit code, mirroring F052's wiring of `fsgg route`/`fsgg ship`) are out of scope here.
- **`fsgg verify` is a sibling concern.** The Phase 13 roadmap pairs `fsgg verify` and `fsgg release` schemas; this row addresses the **release** gate core. The verify gate is its own thread/row.
- **No new third-party dependency** and **no schema version bump** are expected; the core is a new pure library layered on the existing merged thread (the constitution's "heavier capabilities layer on top, not into the core").

## Out of Scope / Deferred to Later Rows

- **Sensing real release facts** from a governed repository (version, package metadata, template pins, publish plan, trusted-publishing configuration, provenance/attestations) — the **next** row.
- **The `fsgg release` host command** wiring sensing → this core → enforcement → exit code, and the run-mode/profile CLI flags — a following row.
- **The additive `release.json` document projection** of the release findings and verdict — a following projection row (the RouteJson/AuditJson/CacheEligibilityJson precedent).
- **Attestation / trusted-publishing execution** (emitting SLSA/in-toto-shaped provenance summaries, performing a publish) — later rows; this core only evaluates whether the declared provenance/publishing expectations are **met**.
- **The `fsgg verify` gate schema** — the sibling Phase 13 deliverable, its own thread.
