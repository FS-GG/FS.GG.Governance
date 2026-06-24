# Feature Specification: Execute Selected Gates In `fsgg route` And `fsgg ship` — Capture Their Evidence And Persist The Grown Reuse Store

**Feature Branch**: `052-route-ship-gate-execution`

**Created**: 2026-06-24

**Status**: Draft

**Input**: User description: "next item in the plan." — resolved (maintainer-confirmed this session, via
AskUserQuestion, and named explicitly as "the following row" by the merged F051 plan/spec) to the **host
wiring** that closes the evidence-reuse loop end-to-end: make the `fsgg route` (F022) and `fsgg ship` (F026)
host commands actually **run** each selected gate's process through the F051 gate-execution port, assemble its
F050/F032 command record, **capture** a real evidence reference (F049), fold it into the bounded reuse store,
and **persist** the grown store (F047/F048) — so that a gate the cache marks `reusable` is reused (not
re-run), and a gate's real execution outcome finally drives the `fsgg ship` verdict.

## Overview

Every pure core on the evidence-reuse path is merged, and as of F051 the one impure capability the whole
chain was missing exists too. F051 delivered `FS.GG.Governance.GateExecution` — the **first and only** place
in the codebase that starts a gate process: an injected `ExecutionPort`, the real `realPort` that drives
`System.Diagnostics.Process`, and `senseExecution` (edge I/O + the pure F050 `recordOf`) which runs one gate
command to a complete `CommandRecord`. F049 `referenceOf`/`capture` then derives a reproducible `EvidenceRef`
from such a record and folds the gate's freshness world into the bounded reuse store; F047/F048
serialise / prune / retain and persist that store across runs.

But the two commands people and CI actually run — `fsgg route` and `fsgg ship` — **never execute a gate**.
Today they sense freshness, load the reuse store **read-only**, evaluate cache eligibility (F046), and even
re-persist a pruned/retained copy of the store (F047/F048) — but because nothing ever runs a gate, that store
is never **grown**. The cache section therefore reports `mustRecompute / noPriorEvidence` for every gate on
every run, forever; the verdict the whole thread built toward never benefits from a real run, and `fsgg ship`
passes or fails without ever running the gates it selects.

This feature is the missing wire. It teaches **both** host commands to:

1. **Run** each selected gate that the cache marks `mustRecompute` through the F051 port (`senseExecution
   realPort <command>`), deriving the gate's command-to-run from its **declared** command spec (executable,
   arguments, working directory, environment, timeout) — reusing F051 verbatim and adding no new sensing.
2. **Skip** each selected gate the cache marks `reusable`: its prior captured outcome is reused, not
   re-executed — the genuine payoff of the cache thread (work is saved on a repeat run).
3. **Capture** each executed gate's evidence (F049 `capture`) into the store and **persist** the **grown**
   store via the existing F047/F048 prune / retain / serialise / persist path.

Two consequences are load-bearing, and they are a **deliberate departure** from the prior cache-only rows
(maintainer-confirmed this session):

- **A gate's real execution outcome now drives the `fsgg ship` verdict.** A gate whose process exits non-zero,
  times out, or fails to start is a **failed** gate; at its existing effective severity it becomes a blocker
  or a warning per the **existing** F023/F024 enforcement rules, and so contributes to the ship pass/fail
  verdict and the numeric exit code. This row introduces **no new severity scheme** — it supplies the **real
  pass/fail** the enforcement rollup previously had no way to obtain. A `reusable` gate contributes its
  **prior** recorded outcome to the verdict on the same terms.
- **`fsgg route` stays advisory.** Route executes the same gates (to capture evidence and grow the store) and
  reports each gate's execution outcome and whether it was *executed* or *reused* in `route.json`, but it
  **still always exits 0** and makes no merge decision — preserving its established planning-command contract.
  Enforcement lives in `fsgg ship`.

The cache loop now closes from a **real executed gate**: run → assemble record → `referenceOf` → `capture` →
persist grown store; the **next** run senses the same freshness world, finds a matching reference, marks the
gate `reusable`, and reuses it. The port is **total and safe** (F051): a missing executable, a timeout, or a
start failure is a *recorded* outcome (sentinel exit code), never a thrown exception that crashes the command;
a gate with **no declared command** is not executed and keeps its current rollup treatment.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - `fsgg ship` runs its selected gates and the verdict reflects real results (Priority: P1)

A maintainer runs `fsgg ship` under a chosen mode/profile against a protected branch. For each selected gate
that must recompute, `fsgg ship` now **runs the gate's declared command** through the execution port,
assembles its command record, and folds the real outcome into enforcement: a gate whose process exits cleanly
passes; a gate whose process exits non-zero, times out, or fails to start **fails**, and — at its existing
effective severity — becomes a blocker or a warning exactly per the current mode/profile rules. The ship
pass/fail verdict and numeric exit code now reflect the gates actually running, not a selection-only rollup.
Each executed gate's evidence is captured and the grown reuse store is persisted.

**Why this priority**: This is the headline value of the whole evidence-reuse thread and the first time
`fsgg ship` enforces what it selects by actually running it. It is also the highest-risk change (it alters the
safety-critical ship verdict and exit code), so it is the primary slice this row must get right and is a
viable MVP on its own.

**Independent Test**: Run `fsgg ship` against a fixture repo whose selected gates map to deterministic
temp-script commands (one exits 0, one exits non-zero) through an injected execution port; assert the
non-zero-exiting blocking gate is partitioned as a blocker, the ship verdict and exit code reflect it, the
clean gate passes, and each executed gate's evidence is captured into the persisted grown store.

**Acceptance Scenarios**:

1. **Given** a selected blocking gate whose declared command exits non-zero and no prior reusable evidence,
   **When** `fsgg ship` runs, **Then** that gate is executed once, recorded as failed, partitioned as a
   blocker, and the ship verdict is fail with a non-zero exit code.
2. **Given** a selected gate whose declared command exits 0, **When** `fsgg ship` runs, **Then** that gate is
   executed once, recorded as clean, and does not become a blocker on account of its execution.
3. **Given** any set of executed gates, **When** `fsgg ship` completes, **Then** each executed gate's evidence
   reference has been folded into the reuse store and the grown store has been persisted (pruned and retained
   to the bound) at the conventional store path.

---

### User Story 2 - A reusable gate is skipped, closing the cache loop (Priority: P1)

On a second run over an unchanged gate, the cache-eligibility step (F046) marks that gate `reusable` because
the store captured on the prior run holds a reference matching the gate's current freshness world. `fsgg
route` / `fsgg ship` then **does not re-run that gate**: it reuses the prior captured outcome — for both the
evidence it carries and (in `fsgg ship`) the verdict it contributes — and runs only the gates marked
`mustRecompute`. The expensive work is performed once and reused, which is the entire point of the cache.

**Why this priority**: Reuse-driven skipping is the defining behavior of the evidence-reuse thread; without it
the prior fourteen rows (F029–F051) deliver no saved work. It is equal in importance to US1 — together they
are the closed loop.

**Independent Test**: Run a command twice against the same fixture repository state with a writable store: on
the first run the gate is executed and its evidence persisted; on the second run, with the gate's freshness
world unchanged, assert the gate is reported `reusable`, is **not** executed (no second process spawn), and
its reused outcome appears in the document and (for ship) the verdict.

**Acceptance Scenarios**:

1. **Given** a gate executed and captured on a prior run and an unchanged freshness world, **When** the
   command runs again, **Then** the cache marks the gate `reusable`, the gate's process is **not** spawned a
   second time, and the document reports the gate as reused.
2. **Given** a gate marked `reusable`, **When** `fsgg ship` runs, **Then** the reused gate contributes its
   prior recorded outcome to the verdict on the same terms an executed outcome would.
3. **Given** a gate whose freshness world changed since capture (so the cache marks it `mustRecompute`),
   **When** the command runs, **Then** the gate **is** executed (the stale reference is not reused) and the
   fresh evidence is captured.

---

### User Story 3 - `fsgg route` runs and reports gates while staying advisory (Priority: P2)

A developer runs `fsgg route` against a working tree with pending changes. Route now runs each selected
must-recompute gate (to capture evidence and grow the store) and reports, on each selected-gate entry in
`route.json`, that gate's execution outcome (its real exit code and output digests) and whether it was
**executed** or **reused** — beside the cache verdict F046 already renders. Route nonetheless **still always
exits 0** and makes no merge decision: the execution outcome is information on the route artifact, not a
verdict. The grown store is persisted exactly as in `fsgg ship`.

**Why this priority**: Route is the lower-risk command (it makes no merge decision), so reporting execution
there is valuable for developers previewing what ship will enforce, but it is secondary to delivering and
de-risking the verdict-affecting execution in `fsgg ship` (US1) and the reuse loop (US2).

**Independent Test**: Run `fsgg route` against a fixture repo whose gates map to deterministic temp-script
commands through an injected port; assert each selected-gate entry in `route.json` carries the gate's
execution outcome and an executed-vs-reused disposition, that the grown store is persisted, and that the
command still exits 0 regardless of any gate's exit code.

**Acceptance Scenarios**:

1. **Given** a routed change selecting gates with declared commands, **When** `fsgg route` runs, **Then** each
   selected-gate entry in `route.json` carries that gate's execution outcome (exit code + output digests) and
   whether it was executed or reused.
2. **Given** a selected gate whose command exits non-zero, **When** `fsgg route` runs, **Then** the failure is
   reported on the gate's entry and the command still exits 0.
3. **Given** a routed change, **When** `fsgg route` completes, **Then** every other field of `route.json`
   (selected gates, route trace, findings, cost rollup, cache section, schema version) is exactly what
   `fsgg route` produced before this wiring, except the new per-gate execution outcome.

---

### User Story 4 - Safe failure and totality when a gate cannot run cleanly (Priority: P2)

A selected gate's declared command names a missing executable, overruns its timeout, or otherwise fails to
start. The command does **not** crash: the F051 port reifies the failure as an ordinary recorded outcome
carrying a named sentinel exit code (start-failure or timeout) and a captured diagnostic. An overrunning gate
is terminated and recorded within a bounded time of its timeout, never left to hang. A selected gate with
**no** declared command is simply not executed and keeps its current rollup treatment. Store-read or
store-persist problems are surfaced honestly and never lose the run's already-computed verdict.

**Why this priority**: Totality and safe failure are the correctness contract that makes it safe to run
arbitrary gate processes inside the safety-critical commands. Essential, but it builds on the execution path
US1 establishes.

**Independent Test**: Run each command against fixtures where a gate's command is a missing executable, a
script that sleeps past a short timeout, and a script that exits non-zero; assert no command crashes, each
failure is a recorded outcome with the correct sentinel/exit code, the timeout returns within a bounded time,
and a gate with no declared command is skipped (not executed) with its prior treatment intact.

**Acceptance Scenarios**:

1. **Given** a selected gate whose declared command names a missing executable, **When** the command runs,
   **Then** the gate's run is a recorded start-failure outcome (sentinel exit code + captured diagnostic),
   nothing is thrown, and in `fsgg ship` the gate is treated as failed per its effective severity.
2. **Given** a selected gate whose command overruns its declared timeout, **When** the command runs, **Then**
   the gate is terminated and recorded (timeout sentinel, partial output, elapsed duration) within a bounded
   time of the limit; the command does not hang.
3. **Given** a selected gate with no declared command, **When** either command runs, **Then** the gate is not
   executed, no evidence is captured for it, and its existing rollup treatment is unchanged.
4. **Given** a reuse store path that exists but cannot be read, **When** either command runs, **Then** the
   command proceeds as if the store were empty (every gate `mustRecompute`, so executed), surfaces the read
   failure honestly, and does not crash.

---

### User Story 5 - Deterministic, reproducible evidence and a bounded persisted store (Priority: P3)

Running the same deterministic gate over the same world twice produces a **byte-identical** command-record
identity (`canonicalId`) despite differing measured durations, so the second run's freshness world matches the
captured reference and the gate is reused. The persisted reuse store is deterministic and **bounded**: it is
pruned of superseded entries and retained to the standard cap before serialisation, and an identical
repository state yields a byte-stable store and byte-stable documents (apart from each gate's measured
duration, which is excluded from identity).

**Why this priority**: Reproducible identity is what makes reuse sound and the artifacts diffable, but it is a
property the merged F050/F049/F047 cores already guarantee; this row need only preserve it by composing them
verbatim.

**Independent Test**: Execute the same deterministic gate twice; assert the two command records share a
byte-identical `canonicalId` (so the second run reuses), that perturbing any reproducible fact (an output
byte, an argument, the working directory, an env-delta entry, the exit code) changes it while a duration-only
difference does not, and that the persisted store is pruned and retained to the bound.

**Acceptance Scenarios**:

1. **Given** a deterministic gate run twice over the same world, **When** its records are compared, **Then**
   their `canonicalId` is byte-identical and the second run reuses the first run's evidence.
2. **Given** a captured store exceeding the retention bound, **When** it is persisted, **Then** it is pruned of
   superseded entries and retained to the standard cap.
3. **Given** a fixed repository state, **When** either command runs twice, **Then** the persisted store and the
   emitted documents are byte-stable apart from each gate's excluded measured duration.

---

### Edge Cases

- **Gate with no declared command** (no `RequiresCommand` prerequisite): not executed, no evidence captured;
  its existing rollup/enforcement treatment is unchanged.
- **Empty route / no selected gates**: nothing is executed; the store is re-persisted (pruned/retained) as
  today; the ship verdict and route exit code are unchanged from a no-gate run.
- **Gate marked `reusable` but its prior outcome is not recoverable** from the store: the gate is conservatively
  **recomputed** (executed), never silently treated as passed — reuse requires a recoverable prior outcome.
- **Gate command exits non-zero**: recorded verbatim; a blocker/warning in `fsgg ship` per effective severity;
  reported but exit-0-preserving in `fsgg route`.
- **Gate command times out**: terminated and recorded (timeout sentinel, partial output, elapsed duration)
  within a bounded time; treated as a failed gate in `fsgg ship`.
- **Missing executable / process start failure**: recorded (start-failure sentinel + captured diagnostic), not
  thrown; treated as a failed gate in `fsgg ship`.
- **Empty / binary / very large gate output**: captured and digested in full, with no truncation or decoding.
- **Reuse store absent** (a fresh repo): treated as the empty store ⇒ every gate `mustRecompute` ⇒ executed;
  after the run the store exists and holds the captured references.
- **Reuse store present but unreadable**: degrades to the empty store (all gates executed), read failure
  surfaced honestly, command does not newly crash.
- **Store persist failure**: surfaced honestly; it does not corrupt a partially written store and does not
  change the already-computed ship verdict / route exit code for the current run.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `fsgg route` and `fsgg ship` MUST, for each selected gate the cache-eligibility step marks
  `mustRecompute` and that declares a command, run that gate exactly once through the F051 execution port
  (`senseExecution` over the real port), assembling its complete F032/F050 command record — reusing F051
  verbatim and adding no new process-spawning or sensing technique.
- **FR-002**: The command-to-run for a gate MUST be derived from that gate's **declared** command spec
  (executable, arguments, working directory, environment, timeout); the commands MUST NOT fabricate a command,
  alter the declared timeout, or diff the ambient process environment into the run.
- **FR-003**: A selected gate the cache marks `reusable` MUST NOT be re-executed; its prior captured outcome
  MUST be reused for the evidence it carries and (in `fsgg ship`) for the verdict it contributes. Only gates
  marked `mustRecompute` are executed.
- **FR-004**: A gate marked `reusable` whose prior outcome is not recoverable from the store MUST be
  conservatively recomputed (executed); no gate may be treated as passed/reused without a recoverable prior
  outcome.
- **FR-005**: A selected gate with **no** declared command MUST NOT be executed and MUST retain its current
  rollup/enforcement treatment unchanged; no evidence is captured for it.
- **FR-006**: In `fsgg ship`, each gate's execution outcome MUST feed the **existing** F023/F024 effective-
  severity enforcement as the gate's pass/fail result — a clean exit is a pass; a non-zero exit, a timeout, or
  a start failure is a fail — so that a failing gate becomes a blocker or warning at its existing effective
  severity, contributing to the ship verdict and the numeric exit code. This row MUST introduce **no new**
  severity scheme, mode, profile, or enforcement rule.
- **FR-007**: A `reusable` gate's prior recorded outcome MUST contribute to the `fsgg ship` verdict on the
  same terms an executed outcome would (same pass/fail mapping, same effective severity).
- **FR-008**: `fsgg route` MUST remain advisory: it MUST run the selected must-recompute gates and report, on
  each selected-gate entry of `route.json`, that gate's execution outcome (real exit code and output digests)
  and whether it was executed or reused, but it MUST still always exit 0 and make no merge decision.
- **FR-009**: Every non-execution field of `route.json` (selected gates, route trace, findings, cost rollup,
  cache section, schema version) and every non-execution field of `audit.json` MUST be exactly what each
  command produced before this wiring, except the new per-gate execution outcome and any verdict change in
  `fsgg ship` that follows directly from a gate's real pass/fail result.
- **FR-010**: After running, each command MUST capture each executed gate's evidence into the reuse store via
  F049 `capture` (folding the gate's resolved freshness world and the record's evidence reference) and MUST
  persist the **grown** store via the existing F047/F048 path (prune, retain to the standard bound, serialise,
  write to the conventional store path) — reusing those cores verbatim and bumping no schema.
- **FR-011**: Gate execution MUST be **total and safe**: a missing executable, a process start failure, or a
  timeout MUST be a recorded outcome carrying the F051 sentinel exit code (and, for a start failure, a captured
  diagnostic), never a thrown exception; an overrunning gate MUST be terminated and recorded within a bounded
  time of its declared timeout; the command MUST NOT hang.
- **FR-012**: Captured gate output of **any** size and **any** byte content (empty, binary / non-UTF-8, large)
  MUST be captured and digested in full, with no truncation, decoding, or normalization — relying on the F050
  digest the F051 port already applies.
- **FR-013**: A reuse store that is absent MUST be treated as the empty store (every gate `mustRecompute`,
  hence executed); a store present but unreadable MUST degrade to the empty store with the read failure
  surfaced honestly, and a store-persist failure MUST be surfaced honestly — none of these MUST crash the
  command or change the already-computed `fsgg ship` verdict / `fsgg route` exit code for the current run.
- **FR-014**: The captured evidence and command-record identity MUST be deterministic and reproducible: two
  runs of the same deterministic gate over the same freshness world MUST yield a byte-identical `canonicalId`
  (the measured duration excluded), so the second run is reused; the persisted store MUST be deterministic and
  bounded.
- **FR-015**: The commands MUST NOT dereference the opaque evidence reference, recompute any freshness key or
  digest themselves, render any raw freshness input, or invent any record/outcome shape — they compose the
  merged F049/F050/F051 cores verbatim.
- **FR-016**: Each command's human / JSON summary output MUST reflect the execution outcome (which gates were
  executed vs reused, which passed/failed and how, any sentinel outcome, and any store read/persist failure),
  consistent with the emitted document.
- **FR-017**: This row MUST NOT edit any merged pure core (F023/F024 enforcement, F041–F050, the F045 embed,
  the F049/F050/F051 surfaces) nor re-bless their golden baselines beyond the ship/route command tests that
  legitimately recompute their expected documents to reflect real gate execution; it MUST add no new
  third-party dependency and bump no schema version.

### Key Entities *(include if feature involves data)*

- **Gate command-to-run**: the reproducible inputs for one gate execution (executable, ordered arguments,
  working directory, environment delta, timeout, captured-output target), derived from the gate's declared
  command spec. The F051 `GateCommand`; carries no bytes.
- **Execution outcome / command record**: the sensed result of one run (raw stdout/stderr, exit code, measured
  duration) assembled into a complete F050/F032 `CommandRecord` with output digests. Produced by F051
  `senseExecution`.
- **Gate disposition**: per selected gate, whether it was **executed** (with its real outcome) or **reused**
  (with its prior recorded outcome), or **not executed** (no declared command).
- **Captured evidence reference / grown reuse store**: the F049 `EvidenceRef` folded into the bounded reuse
  store, persisted (pruned + retained) via F047/F048 at the conventional store path.
- **Enriched route.json / audit.json**: the existing documents, now carrying each selected gate's execution
  outcome and executed-vs-reused disposition; `audit.json`'s gate items additionally reflect any verdict change
  that follows from a gate's real pass/fail result. Every other field unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Running `fsgg ship` against a routed change executes each selected must-recompute gate exactly
  once, and a gate whose declared command exits non-zero is partitioned as a blocker/warning at its existing
  effective severity, changing the ship verdict and numeric exit code accordingly.
- **SC-002**: A clean-exiting gate does not become a blocker on account of its execution; the ship verdict
  reflects real gate results, not selection alone.
- **SC-003**: Running a command twice against the same repository state with a writable store executes each
  gate on the first run and **reuses** it (no second process spawn) on the second run, with the document
  reporting the gate as reused.
- **SC-004**: `fsgg route` reports each selected gate's execution outcome and executed-vs-reused disposition in
  `route.json` and still exits 0 regardless of any gate's exit code.
- **SC-005**: A missing executable, a timed-out gate, and a start failure each produce a recorded outcome with
  the correct F051 sentinel, never crash the command, and (in `fsgg ship`) are treated as failed gates; the
  timeout returns within a bounded time of its limit.
- **SC-006**: A selected gate with no declared command is not executed, captures no evidence, and retains its
  prior rollup treatment; an empty selection executes nothing.
- **SC-007**: After a run, the grown reuse store has been persisted (pruned + retained to the standard bound)
  at the conventional path; an absent store ends the run present-and-populated, an unreadable store and a
  persist failure are surfaced honestly without crashing or changing the run's verdict.
- **SC-008**: Two runs of the same deterministic gate yield a byte-identical `canonicalId` (measured duration
  excluded), and the persisted store is deterministic and bounded.
- **SC-009**: The full solution builds clean and all existing projects' tests stay green; no merged pure core
  is edited and no schema is bumped; the only document changes are the new per-gate execution outcome and the
  `fsgg ship` verdict changes that follow directly from real gate results.

## Assumptions

- **Both commands execute gates; the ship verdict is execution-driven; reusable gates are skipped**
  (maintainer-confirmed this session via AskUserQuestion). This row deliberately **departs** from the prior
  cache-only invariant ("cache is information, not a verdict") for `fsgg ship`: a gate's real execution outcome
  now feeds the existing enforcement and so the ship verdict and exit code. `fsgg route` keeps its advisory
  always-exit-0 contract and reports execution as information only.
- **The command-to-run is derived from the gate's declared command spec.** A selected gate references a
  command by id (its `RequiresCommand` prerequisite); the command spec (the shell command line, timeout, and
  environment class) is resolved from the loaded catalog/config and translated into the F051 `GateCommand`.
  The exact parsing of the declared command string into executable + arguments + working directory, and the
  mapping of the declared environment class into an environment delta, is a plan-time mechanism decision; the
  spec requires only that the **declared** inputs are applied verbatim and recorded, with no ambient-env leak.
- **Reuse requires a recoverable prior outcome.** A gate is treated as `reusable` (skipped) only when the
  store yields a prior outcome sufficient to both carry as evidence and contribute to the verdict; otherwise
  the gate is recomputed. Exactly how much of the prior outcome the store must carry to support a reused
  verdict (e.g. the prior exit code) is a plan-time decision; the safe default — recompute when in doubt —
  holds regardless.
- **Freshness sensing, cache eligibility, and store load/persist reuse the existing F044/F046/F047/F048
  technique verbatim** (the conventional store path, absent ⇒ empty, prune + retain to `defaultRetentionBound`
  before serialise). No new sensing, eviction, or expiry policy is introduced.
- **Gate execution uses the F051 real port** (BCL `System.Diagnostics.Process`) and adds no new third-party
  dependency. Tests drive execution through a deterministic **fake** port and **real** temp-script fixtures,
  reaching the real port only for the edge tests — so the bulk of the semantic tests touch no network and no
  governed repository, even though a real run by definition runs whatever the gate's command does.
- **Standard determinism / no-clock discipline** applies to the reproducible facts and the persisted store;
  the wall-clock duration is the sole sensed, excluded fact (F050 FR-006).
- **The command tests recompute their expected `route.json` / `audit.json` live**, so the new per-gate
  execution outcome and the execution-driven ship verdict are verified against recomputed expectations; the
  F028 enforcement truth-table fixtures and any golden snapshots projected without execution stay on their
  existing path unless a fixture legitimately exercises execution.

## Out of Scope

- **Sandboxing or resource limits beyond the declared timeout** — CPU/memory caps, filesystem or network
  isolation, privilege dropping. Each gate runs as an ordinary child process bounded only by its timeout.
- **Parallel / concurrent gate execution, scheduling, retries, or partial-run resumption** — gates are run
  one outcome at a time.
- **A captured-output file subsystem** — writing a gate's output to a file and locating it — beyond carrying
  the declared captured-output target verbatim into the record.
- **New eviction / expiry / TTL policy for the reuse store** beyond the existing F047 prune + retain bound.
- **Any new cache-derived severity, mode, profile, or enforcement rule** — the only new verdict input is a
  gate's real pass/fail; the severity/mode/profile machinery is reused unchanged.
- **Any change to the F049 `EvidenceCapture`, F050 `ExecutionRecord`, F051 `GateExecution`, F045 embed, or
  enforcement surfaces, or to the `fsgg.evidence-reuse-store/v1`, `route.json`, or `audit.json` schemas.**
- **Editing merged F023/F024/F029–F051 cores, their golden baselines, or anything in Phase 13 (Release &
  Distribution Readiness).**
