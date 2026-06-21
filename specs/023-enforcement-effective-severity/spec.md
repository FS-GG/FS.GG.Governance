# Feature Specification: Enforcement Levers and Effective Severity

**Feature Branch**: `023-enforcement-effective-severity`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. Phase 2's remaining rows (`fsgg ship --mode gate`, `audit.json`)
both depend on a Phase-5 enforcement model that does not yet exist: run modes, profiles, maturity, and a
base→effective severity computation (the "truth-table dials"). Continuing this repo's maintainer-confirmed
**pure-core-first** rhythm (F015–F021 each landed a pure core before F022's host edge), this row is sliced
to the **first Phase-5 pure core**: the typed enforcement vocabulary and the total, deterministic function
that derives a finding's *effective* severity (and the reason for it) from its *base* severity, its rule
maturity, the run mode, and the active Governance profile — the prerequisite `fsgg ship` and `audit.json`
will consume. It computes **no** ship verdict, persists **no** artifact, and adds **no** CLI.

## Overview

Governance separates *what is true* from *how strict to be about it*. A rule produces a verdict and a
**base severity** (advisory or blocking) that never changes. Three independent **levers** then decide how
much that finding actually counts at the moment it is evaluated:

- **Run mode** — *where* the command runs and which boundary is being protected
  (`sandbox`, `inner`, `focused`, `verify`, `gate`, `release`).
- **Governance profile** — *how strict* the project chose to be at that boundary
  (`light`, `standard`, `strict`, `release`).
- **Rule maturity** — *whether the rule is trusted enough to block yet*
  (`observe`, `warn`, `block-on-pr`, `block-on-ship`, `block-on-release`).

None of the three levers changes truth. They combine to produce one derived value — the finding's
**effective severity** — together with a human-readable **reason** that names exactly which levers
produced it. This feature is that derivation, expressed as a pure, total core: model the three levers and
the two severities as closed typed values, and provide a single deterministic function that maps
`(base severity, maturity, run mode, profile)` to `(effective severity, reason)`. It is the design's
*Modes, profiles, and maturity* section (`docs/initial-design.md`) and the first four checkboxes of the
implementation plan's *Phase 5: Route Parity, Profiles, and Enforcement Fixtures*.

The core **carries base severity through unchanged and never receives or alters the finding's verdict
or identity** — a profile may change effective enforcement but MUST never hide the underlying verdict,
alter a rule's identity, or remove a finding. Because the verdict and the finding body are the caller's
to carry (they are not inputs to this core), that safety promise holds trivially here: this core only
assigns effective severity and explains it; it does **not** roll findings up into a
ship verdict, decide a merge/exit code, persist `audit.json`/`route.json`, evaluate cost or cache, parse
the project's `.fsgg/policy.yml`, or expose any command. Those are `fsgg ship` / `audit.json` / Phase 11 /
the Config loader / the CLI rows respectively — this core is the pure decision they each reuse, the same
way F018's registry and F019's selection were pure values consumed by later edges.

It reuses F014 `Config`'s already-typed `Maturity` (`observe`/`warn`/`block-on-pr`/`block-on-ship`/
`block-on-release`) and `ProfileId` verbatim, and introduces only the vocabulary F014 did not model: the
run **mode**, the base/effective **severity** value, and the four canonical profile **strictness**
levels. The four canonical profiles carry the design's documented strictness; project-authored per-class
profile dial overrides from `policy.yml` (the `unknownPaths`/`staleEvidence`/… map) are a later layer and
are out of scope here.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Derive a finding's effective severity and explain it (Priority: P1)

A Governance gate (later, `fsgg ship`) holds a finding with a fixed verdict and a declared **base
severity** and rule **maturity**, and is running under a known **run mode** and **profile**. It needs one
deterministic answer: *does this finding effectively block here, or is it advisory?* — and a reason it can
print and record, so a human or agent understands why a base-blocking finding is being treated as advisory
(or vice-versa) without re-reading the rule engine.

**Why this priority**: This is the feature's reason to exist. Without the effective-severity derivation
there is no way to turn truth into enforcement, and `fsgg ship`/`audit.json` cannot exist. With just this
story, every Governance finding can be classified and explained — the MVP.

**Independent Test**: Call the derivation with a finding that is base-blocking and `block-on-ship`
maturity under `inner` mode + `light` profile; assert the effective severity is advisory and the reason
names the levers that relaxed it (the design's worked example). Repeat under `release` mode + `release`
profile and assert it effectively blocks. Each output traces to its inputs; nothing is invented.

**Acceptance Scenarios**:

1. **Given** a finding with base severity blocking, maturity `block-on-ship`, **When** evaluated under run
   mode `inner` with profile `light`, **Then** the effective severity is advisory and the reason names the
   run mode and profile that relaxed it.
2. **Given** the same finding, **When** evaluated under run mode `gate` or `release`, **Then** the
   effective severity is blocking and the reason names the boundary that reached the maturity threshold.
3. **Given** a finding whose rule maturity is `observe` or `warn`, **When** evaluated under any run mode and
   any profile, **Then** the effective severity is advisory and the reason names the maturity that withholds
   blocking.
4. **Given** any finding, **When** it is evaluated, **Then** the output reports the unchanged base severity,
   the run mode, the profile, the maturity, the effective severity, and a non-empty reason — all six.

---

### User Story 2 - Name the canonical enforcement vocabulary as closed, total values (Priority: P2)

A later host edge (the `fsgg ship --mode gate --profile standard` CLI) and the policy loader must turn
caller-supplied or file-supplied strings (`"gate"`, `"standard"`) into the typed levers this core
consumes, and must reject anything outside the canonical sets without crashing. The vocabulary is a fixed,
closed contract shared across the system.

**Why this priority**: The levers are only useful if every consumer agrees on exactly which modes,
profiles, severities, and maturities exist and can map names onto them. This is small but load-bearing —
it is the shared dictionary US1's computation and the later CLI both depend on.

**Independent Test**: For each canonical mode and profile name, recognize it as its typed value; for an
unrecognized string, get a total "unrecognized" result (not an exception). Assert the recognized sets are
exactly the six modes and four profiles named in the design.

**Acceptance Scenarios**:

1. **Given** each canonical run-mode name (`sandbox`, `inner`, `focused`, `verify`, `gate`, `release`),
   **When** recognized, **Then** it maps to the corresponding typed run mode.
2. **Given** each canonical profile name (`light`, `standard`, `strict`, `release`), **When** recognized,
   **Then** it maps to the corresponding typed profile.
3. **Given** a string outside the canonical set, **When** recognition is attempted, **Then** the result is a
   total "unrecognized" outcome with the offending value, never an exception.

---

### User Story 3 - Guarantee profiles explain enforcement without hiding truth (Priority: P3)

A maintainer auditing the gate must trust that turning a profile up or down only ever changes *enforcement*
— it can never silently erase a failing verdict, rewrite a rule's base severity, or drop a finding from
the record. The relationship between base and effective severity must always be inspectable.

**Why this priority**: This is the design's central safety promise ("Profiles must never hide the
underlying verdict, alter rule hashes, or remove findings from JSON"). It hardens US1 against the failure
mode that would make Governance untrustworthy, but the core is already useful without it formalized.

**Independent Test**: Across a representative sweep of (base severity × maturity × mode × profile),
assert the base severity in the output always equals the base severity in the input, the finding's
identity is preserved, and every result carries a reason — no input is dropped and no base severity is
mutated.

**Acceptance Scenarios**:

1. **Given** any combination of base severity, maturity, run mode, and profile, **When** effective severity
   is derived, **Then** the reported base severity is byte-identical to the input base severity.
2. **Given** any finding, **When** evaluated under the most relaxed and the strictest profile, **Then** both
   results still report the same base severity, maturity, and run mode; only effective severity and reason
   differ. (The finding's verdict is the caller's to carry — it is not an input to or output of this core.)
3. **Given** a finding that is base-advisory, **When** evaluated under any mode and profile, **Then** the
   effective severity stays advisory (this core never escalates it) and the reason explains the
   non-escalation, naming the deferred per-class strictness dials (FR-015) as where escalation would live.

---

### Edge Cases

- **Maturity withholds blocking regardless of strictness**: `observe` and `warn` are always advisory; no
  run mode or profile can make them block. The reason must say so.
- **Boundary not yet reached**: a base-blocking, `block-on-ship` finding evaluated below the ship boundary
  (`sandbox`/`inner`/`focused`) is advisory; at or beyond it (`gate`/`release`) it blocks.
- **Stricter profile blocks earlier**: a stricter profile lowers the blocking boundary for a base-blocking
  finding — `strict` blocks one run mode earlier and `release` two earlier than `light`/`standard`, never
  later. The reason names the active profile and the boundary it produced. `light` and `standard` honour the
  maturity floor unchanged in this slice (they differ only in reason text; per-class relaxation is deferred,
  FR-015).
- **Strictest combination**: run mode `release` with profile `release` is the maximal boundary — anything
  whose maturity permits blocking there blocks.
- **Most relaxed combination**: run mode `sandbox` with profile `light` blocks the least; base-blocking
  findings whose maturity has not matured to this boundary remain advisory.
- **Unrecognized lever name**: an unknown mode or profile string surfaces as a total unrecognized outcome
  carrying the offending value — never an exception and never a silent fallback to a default lever.
- **Same inputs, same output**: the derivation is deterministic — identical inputs always yield identical
  effective severity and identical reason text.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST model run **mode** as a closed set of exactly the six canonical values
  `sandbox`, `inner`, `focused`, `verify`, `gate`, `release`, ordered from least to most protective boundary.
- **FR-002**: The system MUST model the Governance **profile** as a closed set of exactly the four canonical
  values `light`, `standard`, `strict`, `release`, ordered from least to most strict.
- **FR-003**: The system MUST reuse F014 `Config`'s `Maturity` (`observe`, `warn`, `block-on-pr`,
  `block-on-ship`, `block-on-release`) and `ProfileId` rather than redefining them.
- **FR-004**: The system MUST model **base severity** and **effective severity** as the same closed
  enumeration of exactly two values (advisory and blocking), so effective severity is directly comparable
  to base severity.
- **FR-005**: The system MUST provide a single **total** derivation from `(base severity, maturity, run
  mode, profile)` to `(effective severity, reason)` that is defined for every combination of inputs and
  never throws.
- **FR-006**: The derivation MUST be **deterministic** — identical inputs always produce identical effective
  severity and identical reason text, with no clock, environment, ordering, or host-path influence.
- **FR-007**: A finding whose maturity is `observe` or `warn` MUST always derive an advisory effective
  severity, regardless of run mode or profile.
- **FR-008**: A finding whose maturity permits blocking MUST derive a **blocking** effective severity only
  when the current run mode reaches or exceeds that maturity's **profile-adjusted** blocking boundary;
  otherwise advisory. The maturity sets the base boundary (FR-007 for `observe`/`warn`); a stricter profile
  MAY lower that boundary so the finding blocks one or more run modes earlier (FR-002, FR-009).
- **FR-009**: The profile MUST be able to change effective enforcement but MUST NEVER change the reported
  **base severity**, change the finding's **verdict** or identity, or remove a finding. In this slice the
  intrinsic profile lever only **tightens** (a stricter profile lowers the blocking boundary, FR-008);
  profile-driven *relaxation* below the maturity floor and *escalation* of a base-advisory finding are
  per-class behaviors deferred to the `.fsgg/policy.yml` dial layer (FR-015), out of scope here.
- **FR-010**: Every derivation result MUST carry all of: the unchanged base severity, the run mode, the
  profile, the maturity, the effective severity, and a **non-empty reason** that names the levers
  responsible for the effective severity.
- **FR-011**: The system MUST provide a total way to **recognize** a caller-supplied string as a canonical
  run mode or profile, yielding an explicit "unrecognized" outcome (carrying the offending value) for any
  string outside the canonical set — never an exception and never a silent default.
- **FR-012**: A finding that is treated as advisory because of mode/profile/maturity MUST still be reported
  (never dropped); enforcement relaxation is *reclassification*, not suppression.
- **FR-013**: The system MUST NOT compute a ship/merge verdict, blockers list, exit code, or any rollup
  across findings; the unit of this core is a single finding's effective severity.
- **FR-014**: The system MUST NOT perform I/O — no file reads or writes, no `.fsgg/policy.yml` parsing, no
  artifact persistence, and no CLI surface; it is a pure value-to-value computation.
- **FR-015**: The four canonical profiles MUST carry the design's documented strictness semantics intrinsically;
  parsing project-authored per-class profile dial overrides (the `unknownPaths`/`staleEvidence`/… map) from
  `.fsgg/policy.yml` is out of scope for this feature.

### Key Entities *(include if feature involves data)*

- **Run mode**: where a Governance command is running and which boundary it protects; a closed, ordered set
  of six values. Higher modes protect later, stricter boundaries.
- **Profile**: how strict the project chose to be; a closed, ordered set of four values referencing F014's
  `ProfileId`. Higher profiles enforce more.
- **Maturity** (reused from F014): whether a rule is trusted enough to block, and at which boundary.
- **Base severity**: the rule's intrinsic, immutable severity for a finding (advisory or blocking) — an
  input, never altered by this core.
- **Effective severity**: the derived enforcement level for a finding under the current levers — the output,
  paired with a reason; the same enumeration as base severity so the two are directly comparable.
- **Enforcement decision**: the result value bundling base severity, run mode, profile, maturity, effective
  severity, and the human-readable reason — the explainable record a finding carries forward.
- **Reason**: a deterministic, non-empty explanation naming the levers that produced the effective severity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every one of the full cross-product of base severities × maturities × run modes ×
  profiles, the derivation returns a defined `(effective severity, reason)` result without throwing
  (totality over the complete enforcement truth table).
- **SC-002**: The design's worked example reproduces exactly: base blocking + `block-on-ship` + `inner` +
  `light` ⇒ effective **advisory**, with a reason naming the run mode and profile.
- **SC-003**: For 100% of inputs, the output's base severity equals the input's base severity (profiles
  never alter base severity) — verifiable across a property sweep.
- **SC-004**: Running the derivation twice on identical inputs yields byte-identical effective severity and
  reason text in 100% of cases (determinism).
- **SC-005**: Every canonical mode and profile name is recognized as its typed value, and every
  non-canonical string yields a total "unrecognized" outcome — across the full canonical set plus
  representative invalid strings, with zero exceptions.
- **SC-006**: No finding is dropped by any lever combination: the count of findings out equals the count of
  findings in for every evaluation (reclassification, not suppression).

## Assumptions

- **Pure-core-first slice**: Following the maintainer-confirmed F014–F021 rhythm, this row is the first
  Phase-5 pure core only. `fsgg ship`, `audit.json`, blockers, profile-adjusted exit codes, and golden
  enforcement *JSON snapshots* are later rows that consume this core.
- **Base severity and verdict are inputs**: This core does not derive a finding's verdict or base severity
  (those come from the rule engine / earlier Phase-2 cores); it consumes them and computes effective
  severity. Findings carry a base severity advisory-or-blocking value as their input contract.
- **Four canonical profiles are intrinsic**: `light`/`standard`/`strict`/`release` carry the strictness
  documented in `docs/initial-design.md`. Project-authored per-class dial overrides from `.fsgg/policy.yml`
  are deferred to a later Config + integration layer (F014 today types only profile *names* and a default).
- **Maturity → boundary mapping**: each maturity names the earliest run-mode boundary at which a rule may
  block (`observe`/`warn` never; `block-on-pr` at the PR/gate boundary; `block-on-ship` at the ship/gate
  boundary; `block-on-release` only at release). The exact mode-ordinal mapping is a plan-time
  reconciliation against the design's run-mode ladder.
- **Effective severity domain**: effective severity uses the same enumeration as base severity (advisory /
  blocking) so the two are directly comparable; any finer "block-at-gate" nuance from the profile dials is
  resolved against the current run mode into advisory-or-blocking, not surfaced as a third base value.
- **Reuse over redefine**: `Maturity` and `ProfileId` are referenced from F014 `Config`; this core adds
  only run mode, severity, and profile-strictness. No new third-party dependency is introduced.
- **Boundary discipline**: the core is a pure, total, side-effect-free computation (Constitution Principle
  IV) — no Elmish/MVU edge, since there is no I/O; it slots beside F015/F017/F018/F019 as a pure leaf.
