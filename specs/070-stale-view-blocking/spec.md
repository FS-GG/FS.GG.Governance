# Feature Specification: Block Stale Generated Views at the Configured Governance Boundary

**Feature Branch**: `070-stale-view-blocking`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next backlog item" → the single remaining open functional roadmap row after `069-evidence-json-projection`: Phase 7's 🟡 row *"Block stale generated views at the configured Governance boundary"* (`docs/initial-implementation-plan.md` lines 832–863). The 2026-06-26 roadmap update names it as the last of three genuinely-open rows (rule-id closed by `068`, `evidence.json` closed by `069`); `069`'s own spec lists it as its out-of-scope successor (`specs/069-evidence-json-projection/spec.md` lines 24–25, 212–213). Design contract: `docs/initial-design.md` / `docs/initial-implementation-plan.md` Ground Rules — *"Generated views are outputs. Their presence is not proof of currency."*

## Overview

A *generated view* is an output Governance produces from declared sources — gate metadata, rule catalogs,
capability docs, skill references, API-surface docs, route projections, and baselines (the views `fsgg refresh`
manages and currency-checks). Today these can drift: a generated view can be **stale** — older than its declared
sources, or with a source-digest mismatch — and `fsgg refresh` already **determines** that currency. (The
`fsgg verify` "currency" surface today reports *evidence-reuse* freshness — a different concern from
generated-view staleness.) But a stale generated view can never **block** a merge: the refresh currency
determination is advisory at the merge boundary, so a change can ship with an out-of-date generated view and
nothing stops it.

This feature closes Phase 7's last row: it lets a project **configure** a stale generated-view currency finding
to fold into a **blocking** verdict at a chosen Governance boundary (PR / ship / release), so `fsgg verify` and
`fsgg ship` can fail when a view is stale. It does this by routing the existing currency finding through the
**existing enforcement truth table** (base severity × maturity × run mode × profile → effective severity) —
exactly as surface-check findings already do (F067), and in deliberate contrast to cost/cache findings (F25),
which stay advisory by construction. SDD reports generated-view currency; **Governance enforces** it at its own
boundary.

The change is **opt-in and default-advisory**. With no stale-view blocking configured, behavior is byte-identical
to today: currency findings remain advisory and no verdict, exit code, or existing artifact changes. A project
that dials staleness up gets a blocking verdict at exactly the boundary it chooses, with full no-hide honesty
(a relaxed finding stays a visible warning). It introduces **no new staleness detection, no new severity, and no
new truth-table branch** — it reuses the existing currency determination and the existing enforcement decision
verbatim.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Make a stale generated view block at the protected boundary (Priority: P1)

A maintainer wants the merge boundary to refuse a change whose Governance-generated views are out of date. They
declare, in project configuration, that stale generated views should block at ship (or PR, or release). Now when
a change includes a stale generated view — a gate registry, rule catalog, route projection, or baseline older
than its declared sources — `fsgg ship` (and `fsgg verify` when dialed to the PR boundary) returns a **Fail**
verdict with a blocked exit-code basis, and the blocker names the stale view and what makes it stale, so CI stops
the merge until the view is refreshed.

**Why this priority**: This is the entire point of the feature and the roadmap row — turning an advisory currency
signal into an enforceable protected-boundary verdict. It is the MVP and stands alone: a single configured
boundary that blocks on staleness already delivers the value.

**Independent Test**: Configure stale-view blocking to block at the merge boundary, route a fixture change whose
generated view is stale (source-digest mismatch against declared sources), run the verdict host, and assert a
Fail verdict with a blocked exit-code basis and a blocker that names the stale view.

**Acceptance Scenarios**:

1. **Given** a project configured to block stale generated views at ship, **When** a change with a stale
   generated view is shipped, **Then** `fsgg ship` returns a Fail verdict with a blocked exit-code basis and a
   blocker naming the stale view.
2. **Given** the same configuration and a change whose generated views are all current (fresh), **When** the
   change is shipped, **Then** the stale-view finding is absent, the verdict is unaffected by this feature, and
   the exit-code basis is clean (no false-positive block).
3. **Given** a project configured to block stale generated views at the PR boundary, **When** `fsgg verify` runs
   over a change with a stale generated view **under a `strict` profile** (the only profile that tightens the
   block-on-pr floor down to the verify run mode), **Then** verify surfaces the stale view as a blocker and
   fails. Under the default `standard` profile the same finding is a **warning**, not a blocker — the
   truth-table outcome of verify sitting below the untightened block-on-pr floor (see FR-009).

### User Story 2 - Keep local authoring cheap: blocking is opt-in (Priority: P2)

A maintainer who has not configured stale-view blocking, or who is working in a low-strictness local loop, must
not be newly blocked or see any output change. With no stale-view blocking configuration, stale generated views
remain exactly as advisory as they are today, every existing verdict and exit code is unchanged, and every
existing `route.json` / `audit.json` / `verify.json` / `ship.json` golden is byte-identical.

**Why this priority**: The constitution and the whole codebase keep the authoring loop cheap and ship strictly
additive changes. Default-off, byte-identical-when-unconfigured is the safety contract that lets US1 land without
breaking anyone; it builds directly on US1's machinery (the same finding, simply left at its advisory floor).

**Independent Test**: With no stale-view blocking configuration, run the verdict hosts over both a fresh-view and
a stale-view fixture and assert the verdict, exit code, and every emitted artifact are byte-identical to the
pre-feature baseline.

**Acceptance Scenarios**:

1. **Given** a project with no stale-view blocking configuration, **When** a change with a stale generated view
   is verified or shipped, **Then** the stale view remains an advisory currency finding, the verdict and exit
   code are unchanged, and every existing artifact is byte-identical.
2. **Given** stale-view blocking is configured at `observe`/`warn` maturity, **When** a stale view is present,
   **Then** it never blocks — it is reported but the verdict is not failed by it.

### User Story 3 - No-hide honesty when a stale-view finding is relaxed (Priority: P3)

A maintainer running at a boundary or profile that does not (yet) block a configured stale-view finding must
still **see** it. When the configured maturity, run mode, or profile relaxes the stale-view finding at the active
boundary, it appears as a visible **warning** carrying both its base and effective severity — never silently
dropped, and the underlying currency truth is never altered. A current view, by contrast, produces no finding at
all.

**Why this priority**: Profiles must never hide failures (a standing project invariant); a relaxed blocker is
always a self-explaining warning. It is lower priority only because it refines US1/US2 rather than delivering new
capability, but it must ship for the verdict to be trustworthy.

**Independent Test**: For a stale-view finding configured to block at ship but evaluated under `fsgg verify`
(which cannot reach the merge verdict), assert the finding appears as a warning showing both base and effective
severity and is not dropped; for a fresh view, assert no finding is produced.

**Acceptance Scenarios**:

1. **Given** a stale-view finding configured to block only at ship, **When** `fsgg verify` runs (which stays at
   the verify run mode), **Then** the finding is shown as a warning carrying both base and effective severity and
   is not omitted.
2. **Given** a profile that relaxes the finding at the active boundary, **When** the verdict is produced, **Then**
   the relaxed finding is a visible warning and the underlying currency determination is unchanged.
3. **Given** a generated view that is current, **When** the verdict is produced, **Then** no stale-view finding
   exists in any partition.

### Edge Cases

- **Currency undeterminable** (an in-scope view with no declared sources, a missing currency-manifest entry, or an
  unreadable manifest): the system MUST NOT fabricate either "current" or "stale". It surfaces the unresolved
  condition as a finding (blocking at the configured boundary; absent — byte-identical — when unconfigured)
  and/or an operational diagnostic, and never silently passes a view it could not check (FR-008). A view
  deliberately out of currency scope is `NotEvaluated`, not undeterminable, and produces no finding.
- **Stale at verify, configured to block at ship**: the finding is a warning under `fsgg verify` and a blocker
  under `fsgg ship` — the natural truth-table outcome of the verify run mode being below the configured boundary;
  verify never escalates to the merge verdict (FR-009).
- **Multiple stale views**: each is its own finding and its own blocker/warning; none is collapsed or dropped, and
  the verdict partition stays disjoint and exhaustive.
- **Configured but all views current**: no stale-view blocker is produced; the output is byte-identical to the
  same change with the feature unconfigured (no false positives).
- **Stale-view finding relaxed by profile**: still visible as a warning with both severities (FR-006); the
  currency truth is unchanged.
- **Operational failure sensing currency** (manifest/lock unreadable): surfaced through the host's diagnostics and
  exit code as input-unavailable, never as a fabricated "all current" pass (FR-008).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let a project **configure** whether a stale generated-view currency finding
  contributes to a blocking verdict and at which Governance boundary (PR / ship / release), expressed through the
  **existing enforcement maturity vocabulary** (`observe` / `warn` / `block-on-pr` / `block-on-ship` /
  `block-on-release`).
- **FR-002**: When stale-view blocking is so configured **and** a generated view is stale at the active boundary,
  the system MUST fold the stale-view currency finding into the verdict as a **blocker**, causing `fsgg verify` /
  `fsgg ship` to return a Fail verdict with the blocked exit-code basis.
- **FR-003**: The blocking decision MUST reuse the **existing enforcement truth table verbatim** (base severity ×
  maturity × run mode × profile → effective severity). It MUST introduce no new blocking semantics, no new
  severity, run-mode, profile, or maturity value, and no new truth-table branch.
- **FR-004**: Absent any stale-view blocking configuration, behavior MUST be **byte-identical to today**:
  stale-view currency findings remain advisory; no verdict, exit-code basis, or enforcement truth table changes;
  and every existing `route.json` / `audit.json` / `verify.json` / `ship.json` golden is unchanged. Stale-view
  blocking is **opt-in and default-advisory**.
- **FR-005**: A blocking stale-view finding MUST name the **stale view** and the **out-of-date relationship**
  (which generated view, and what makes it stale — older than declared sources / source-digest mismatch), so a
  consumer can fix it (refresh the view) without consulting another artifact.
- **FR-006**: The system MUST honor the **no-hide rule**: when the configured maturity, run mode, or profile
  relaxes a stale-view finding at the active boundary, the finding MUST remain visible as a **warning** carrying
  both its base and effective severity. It MUST NOT be dropped or hidden, and relaxing it MUST NOT change the
  underlying currency determination.
- **FR-007**: Staleness MUST be the **existing currency determination** (the `fsgg refresh` / source-digest
  comparison that today runs inside the `fsgg refresh` host). The feature MUST introduce **no new staleness
  detection, sensing, or currency representation** — it consumes the determination that already exists, newly
  **sensing it at the `fsgg verify` / `fsgg ship` edge** (reusing the refresh comparator, distinct from the
  evidence-reuse "currency" notes `fsgg verify` already shows). A view that is current MUST produce no finding
  and no verdict effect.
- **FR-008**: When currency cannot be determined for an **in-scope** view (no declared sources, missing
  currency-manifest entry, or unreadable manifest), the system MUST NOT fabricate "current" or "stale". It MUST
  surface the unresolved condition as a finding — which, when stale-view blocking is configured, folds through
  the truth table exactly as a stale view does (blocking at the configured boundary); when **unconfigured**, no
  finding is added and behavior stays byte-identical (FR-004) — and/or an operational diagnostic, and MUST NOT
  silently pass an unchecked in-scope view. A view deliberately **out of currency scope** is `NotEvaluated`
  (not unresolved) and produces no finding.
- **FR-009**: `fsgg verify` MUST remain unable to escalate to the merge (gate) verdict: a stale-view finding
  configured to block only at ship/release surfaces as a **warning** under verify and blocks only at the
  configured higher boundary.
- **FR-010**: The feature MUST be **purely additive**: no existing public projection signature changes; any new
  configuration field and any new JSON detail are additive (existing fields neither removed nor reordered); and
  the stale-view finding participates in the **existing** verdict partition (blockers / warnings / passing) and
  exit-code basis rather than a parallel verdict.

### Key Entities

- **Stale generated view**: a Governance-generated view (gate metadata, rule catalog, capability docs, skill
  references, API-surface docs, route projection, baseline) whose recorded currency is out of date relative to its
  declared sources — older than its sources, or a source-digest mismatch — as already determined by the
  refresh/currency machinery.
- **Stale-view currency finding**: the generated-view currency determination `fsgg refresh` already makes, naming
  the stale view and its cause; this feature **senses it at the verify/ship edge** and gives it a configurable
  base severity + maturity so it can enter the verdict. (Distinct from the evidence-reuse "currency" notes
  `fsgg verify` already emits.)
- **Stale-view blocking configuration**: the project-declared posture — the maturity dial
  (`observe` / `warn` / `block-on-pr` / `block-on-ship` / `block-on-release`) — that selects whether and at which
  boundary a stale view blocks. Default keeps it advisory.
- **Effective-severity decision**: the existing enforcement decision (base severity × maturity × run mode ×
  profile → effective severity, with a lever-naming reason) that turns the configured finding into a blocker or a
  relaxed warning.
- **Verdict partition**: the existing ship/verify rollup (blockers / warnings / passing + exit-code basis) the
  stale-view finding now participates in.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With stale-view blocking configured to block at the merge boundary, a change containing **≥1** stale
  generated view yields a **Fail** verdict and a blocked exit-code basis from `fsgg ship` (and from `fsgg verify`
  when dialed to the PR boundary) — **100%** of configured-blocking stale views become blockers.
- **SC-002**: With **no** stale-view blocking configuration, **0 bytes** of every existing `route.json` /
  `audit.json` / `verify.json` / `ship.json` golden change, and **no** verdict or exit-code basis changes.
- **SC-003**: Across the enforcement dials (every maturity × run mode × profile), the stale-view finding's
  effective severity matches the existing truth table exactly — **0** new truth-table cases — proven by a fixture
  sweep.
- **SC-004**: For every stale-view finding relaxed by the active mode/profile, the finding remains visible as a
  warning showing both base and effective severity — **100%** no-hide, **0** dropped.
- **SC-005**: For every blocking stale-view finding, a reader can identify the stale view and why it is stale from
  the verdict output alone — **100%** self-describing.
- **SC-006**: A current (fresh) generated view never produces a stale-view finding or verdict effect — across the
  all-fresh fixtures, **0** false-positive blockers.

## Assumptions

- **The "configured boundary" is the existing maturity dial.** "PR / ship / release" is expressed through the
  existing enforcement maturity vocabulary (`observe` / `warn` / `block-on-pr` / `block-on-ship` /
  `block-on-release`) and resolved by the existing `deriveEffectiveSeverity` against the run mode of `fsgg verify`
  (verify) and `fsgg ship` (gate). No new boundary concept or enforcement core is introduced — the maturity value
  IS the boundary selector. The exact configuration file and key (most likely the F057 `.fsgg/refresh.yml`
  currency manifest that already declares generated views and their currency gate, or `.fsgg/policy.yml`) is
  locked by the plan.
- **Default posture is advisory / opt-in.** Absent explicit configuration, stale-view findings stay advisory and
  all existing outputs are byte-identical — matching every recent additive Governance feature.
- **Staleness reuses the existing determination.** "Stale generated view" means a Governance-generated view whose
  currency is already decided by the F057 refresh / source-digest machinery and already surfaced as the
  `fsgg verify` currency finding. This feature consumes that determination; it adds no new staleness detection.
- **The folding mirrors the F067 surface-check precedent** (sense finding → map to an enforcement input → fold
  through the existing `deriveEffectiveSeverity` → partition into the existing verdict) and deliberately contrasts
  with the F25 cost-finding floor (fixed-advisory, never blocks). No new pure decision core and no new truth table
  are introduced.
- **Enforcement happens at `fsgg verify` and `fsgg ship`.** `fsgg verify` stays fixed at the verify run mode and
  cannot produce the merge (gate) verdict; `fsgg ship` owns the merge boundary.
- **SDD-owned generated-view currency remains SDD's reporting concern** (SDD readiness views report their own
  staleness via `fsgg-sdd refresh`); Governance enforces only at its own protected boundary.

## Out of Scope

- Any **new staleness detection** or new currency representation — the determination is the existing
  `fsgg refresh` / source-digest comparison; this feature only enforces it.
- **Auto-refreshing or regenerating** a stale view as part of the gate — regeneration is `fsgg refresh`; this
  feature blocks, it does not fix.
- Changing the **enforcement truth table** or adding any new severity, run mode, profile, or maturity value.
- **Per-finding severity levers beyond the existing maturity vocabulary** — general per-class strictness dials
  remain deferred (per the existing enforcement reason text).
- **SDD-side** generated-view reporting and the SDD `fsgg-sdd ship` readiness verdict.
- Blocking on **evidence freshness / cache eligibility** (the cache-eligibility thread, already complete) or on
  **ineffective evidence** (`069`'s evidence world) — this row is specifically about **stale generated views**.

## Change Classification

**Tier 1 (contracted change).** It adds an opt-in configuration field (the stale-view maturity dial) and lets the
stale-view finding participate in the verify/ship verdict partition and any additive JSON detail. It reuses the
existing enforcement truth table, verdict rollup, and currency determination verbatim — no new pure decision core,
no new severity/mode/profile/maturity value, no truth-table branch — and is byte-identical when unconfigured. The
full artifact chain applies: spec, plan, `.fsi` updates for the new/changed surface, surface-area baselines, test
evidence (including the truth-table sweep and the unconfigured byte-identity guard), and the docs flip of the
Phase-7 stale-view-blocking row.
