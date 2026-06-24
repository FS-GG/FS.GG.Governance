# Feature Specification: Run A Gate's Process Behind An Injected Execution Port And Assemble Its Command Record

**Feature Branch**: `051-gate-execution-port`

**Created**: 2026-06-24

**Status**: Draft

**Input**: User description: "next item in the plan." — resolved (the F050 plan and spec name the next row
explicitly: "the impure **gate-execution port** … the first process-spawning capability, behind an injected
port — the Snapshot `GitPort` / F046 `realSensor` precedent") to the **gate-execution port**: the first
impure capability that actually *runs* a gate's process, captures its real stdout/stderr bytes, senses its
exit code and wall-clock duration, **enforces** the supplied timeout, and assembles a complete F032
`CommandRecord` from that outcome by composing the pure F050 `ExecutionRecord.recordOf`. Two scope decisions
were maintainer-confirmed this session (via AskUserQuestion): the port's composition **returns an assembled
`CommandRecord`** (not a raw outcome), and it **enforces** the `TimeoutLimit` (kill + record), never hanging.

## Overview

The cache/evidence-reuse thread is pure all the way from a `CommandRecord` onward: F050 `recordOf` digests a
gate's captured output bytes into the `OutputDigest`s F032 requires and assembles a complete `CommandRecord`
from an *already-captured* execution outcome; F049 `referenceOf`/`capture` derive a reproducible `EvidenceRef`
from that record and fold the gate's freshness world into the bounded reuse store; F047/F048 serialise and
persist the grown store across runs. Every step is pure — **except the one that does not yet exist**: nothing
in the codebase ever *runs a gate's process*. F050 consumes "already-captured bytes and already-sensed F032
facts"; producing those — actually spawning the process, reading its real stdout/stderr, timing the run,
sensing the exit code — is, in F050's own words, "the **following** row (the gate-execution port)."

This feature delivers that row: a new **impure edge** library, `FS.GG.Governance.GateExecution`, the **first
and only** place in the codebase that starts a gate process. It follows the established edge pattern exactly
(the Snapshot `GitPort` / F046 `realSensor` precedent): an **injected execution port** isolates all process
I/O behind a function value (the real port drives `System.Diagnostics.Process`; tests supply a deterministic
fake plus real temp-script fixtures), and a single composition — `senseExecution` — runs one gate command
through the port, captures the outcome, and applies the pure core. Concretely it mirrors `senseSnapshot`
(edge I/O + the pure `Snapshot.assemble`): `senseExecution` is **edge I/O + the pure F050 `recordOf`**.

Given a gate command-to-run (its executable, ordered arguments, working directory, environment delta, timeout
limit, and captured-output target — the reproducible inputs, all F032 vocabulary), `senseExecution` spawns the
process **once**, captures its raw stdout and stderr **bytes** verbatim (no decoding, normalization, or
truncation), senses its exit code and wall-clock duration, **enforces** the timeout (a process that overruns
is terminated and its run recorded as an ordinary terminated outcome), and — by delegating to F050
`recordOf` — returns a complete F032 `CommandRecord`. Of the record's ten facts, only the exit code, the two
output digests (derived from the sensed bytes), and the `SensedDuration` come from *running*; every other
reproducible fact is carried verbatim from the command-to-run. The duration is the sole non-deterministic
fact and is excluded from the canonical identity (F050 FR-006), so two runs of a deterministic gate over the
same world assemble to the **byte-identical** `canonicalId`.

Because the result is an ordinary F032 record, the chain finally closes from a **real executed gate**:
`senseExecution` (this row) → F049 `referenceOf` → F049 `capture` → F047 `serialise`/persist runs from a gate
the system actually ran. The port is **total and safe**: a non-zero exit is *recorded, not rejected* (a
failed gate is evidence, F032 FR-003); a missing executable becomes a recorded failure outcome, not a thrown
exception; an overrunning gate is killed and recorded, never left to hang. The library is **referenced by
nothing on landing** (exactly as F047/F049/F050 were); the **host wiring** that runs gates during a real
`fsgg route` / `fsgg ship` and persists the grown store is the *following* row and is out of scope here.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run a real gate and obtain its assembled command record (Priority: P1)

A maintainer points the port at a gate command-to-run (an executable, ordered arguments, a working directory,
an environment delta, a timeout limit, and a captured-output target) and runs it through `senseExecution`
against the real execution port. The gate's process is spawned **once**, its real stdout and stderr bytes are
captured, its exit code and duration are sensed, and a complete F032 `CommandRecord` is returned — its
`StdoutDigest` is the digest of the captured stdout bytes, its `StderrDigest` the digest of the captured
stderr bytes, and every other reproducible fact matches the command-to-run. No digest is written by hand.

**Why this priority**: This is the whole point of the row — the missing bridge from *a process that actually
ran* to *a `CommandRecord`*. Without it the entire evidence-reuse thread can only ever operate on
hand-fabricated outcomes; with it, the chain to F049 `referenceOf`/`capture` runs from a genuinely executed
gate. It is the MVP: if only this story ships, the codebase can produce a real command record from a real run.

**Independent Test**: Drive `senseExecution` with a real temp-script fixture (a tiny script that prints known
bytes to stdout and stderr and exits zero) behind the real port — or a deterministic fake port returning known
bytes — and assert the returned record's `StdoutDigest` / `StderrDigest` equal F050 `digestOf` of those bytes,
its reproducible facts equal the command-to-run, and `CommandRecord.canonicalId` of it is defined. No
governed repository, no network.

**Acceptance Scenarios**:

1. **Given** a command-to-run whose script writes known bytes to stdout and known bytes to stderr and exits
   `0`, **When** `senseExecution` runs it, **Then** the returned record's `StdoutDigest = digestOf <captured
   stdout>`, `StderrDigest = digestOf <captured stderr>`, and `ExitCode = ExitCode 0`.
2. **Given** the same command-to-run, **When** the record is assembled, **Then** its `Executable`, ordered
   `Arguments`, `WorkingDirectory`, `Environment` delta (its three classes preserved), `Timeout`, and
   `CapturedOutput` equal those of the command-to-run verbatim — none swapped, dropped, or normalized.
3. **Given** the assembled record, **When** F049 `referenceOf` is applied to it, **Then** it yields a defined,
   reproducible `EvidenceRef` and F049 `capture` makes that gate's freshness world reusable — the chain closes
   from a real run.

---

### User Story 2 - A failed, missing, or overrunning gate is recorded, never thrown (Priority: P1)

A gate may exit non-zero, name an executable that does not exist, or run forever. In every case the port
returns an ordinary `CommandRecord` describing what happened — it never throws out of `senseExecution`, and it
never hangs. A non-zero exit is recorded with its real exit code and output digests; a missing executable
becomes a recorded failure outcome (a sentinel exit code and a captured diagnostic); a gate that overruns its
`TimeoutLimit` is terminated and recorded with whatever output was captured and the elapsed duration.

**Why this priority**: Safe failure and totality are not optional for the first process-spawning edge — a
governance run cannot crash or hang because a gate misbehaved, and a *failed* gate is exactly the evidence the
thread exists to record (F032 FR-003). This story is co-critical with US1; the MVP must be safe, not just
happy-path.

**Independent Test**: Run three fixtures through `senseExecution` — a script exiting non-zero, a non-existent
executable, and a script that sleeps past a short `TimeoutLimit` — and assert each returns an ordinary record
(real or sentinel exit code, captured output digested) within a bounded time, with no exception escaping.

**Acceptance Scenarios**:

1. **Given** a command-to-run whose script exits `7` after writing output, **When** `senseExecution` runs it,
   **Then** it returns a complete record with `ExitCode = ExitCode 7` and the captured output digested — the
   run is recorded, not rejected, and no success/exit-code gating is applied.
2. **Given** a command-to-run naming an executable that does not exist, **When** `senseExecution` runs it,
   **Then** it returns an ordinary record carrying a sentinel failure exit code and a captured diagnostic —
   `senseExecution` does not throw.
3. **Given** a command-to-run whose script sleeps far longer than its `TimeoutLimit`, **When**
   `senseExecution` runs it, **Then** the process is terminated and an ordinary record is returned within a
   bounded time (not the full sleep) carrying a terminated exit outcome, the captured partial output, and the
   elapsed duration — `senseExecution` never hangs.

---

### User Story 3 - The reproducible identity is stable across runs; only duration varies (Priority: P2)

Running the same deterministic gate over the same world twice yields two records that differ only in their
sensed `SensedDuration` — their reproducible facts, and therefore their `canonicalId`, are byte-identical. The
port leaks no clock reading, GUID, absolute temp path, locale, or ambient environment into the reproducible
facts; the duration is the sole sensed fact and is excluded from the identity.

**Why this priority**: Reproducible identity is what makes the downstream reuse store *work* — if two runs of
the same gate produced different `canonicalId`s, no evidence could ever be reused. It validates that the
impure edge confines its non-determinism to the duration alone. It depends on US1 and so is P2.

**Independent Test**: Run a deterministic fixture through `senseExecution` twice and assert the two records'
`canonicalId` are byte-identical while their `Duration` may differ; then perturb each reproducible input in
turn (one output byte, an argument, the working directory, an env entry, the exit code) and assert each
perturbation changes `canonicalId`, while a duration-only difference does not.

**Acceptance Scenarios**:

1. **Given** a deterministic command-to-run, **When** `senseExecution` runs it twice, **Then** the two
   records' `CommandRecord.canonicalId` are byte-identical even though their `Duration` values may differ.
2. **Given** two runs differing in exactly one reproducible input (one output byte, one argument, the working
   directory, one env-delta entry, or the exit code), **When** their records are assembled, **Then** their
   `canonicalId` differ.
3. **Given** two runs identical in every reproducible fact and differing only in measured duration, **When**
   their records are assembled, **Then** their `canonicalId` (and therefore, via F049, their `EvidenceRef`)
   are byte-identical.

---

### Edge Cases

- **Executable not found / process fails to start**: caught and reified as a recorded failure outcome (a
  sentinel exit code + a captured diagnostic), never thrown out of `senseExecution` (FR-007, FR-008).
- **Non-zero exit code**: recorded verbatim as an ordinary complete record — a failed gate is evidence, not an
  error (FR-005).
- **Gate overruns its timeout / hangs**: the process is terminated and the run recorded within a bounded time;
  `senseExecution` returns rather than hanging (FR-006).
- **Empty output**: a gate that writes nothing yields a record whose digest is the fixed empty-bytes digest
  (F050 FR-003) — an ordinary value.
- **Binary / non-UTF-8 output**: captured and digested as raw bytes with no decoding, locale, or normalization
  (FR-002, FR-009).
- **Large output**: captured and digested in full with no truncation, regardless of size (SC-008).
- **stdout and stderr identical**: equal captured bytes yield equal digests (content alone — F050).
- **Empty environment delta**: a run with no env changes records an entirely empty three-class delta — an
  ordinary value, not an error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `senseExecution` MUST spawn the gate's process **exactly once** per call, applying the
  command-to-run's executable, ordered arguments, working directory, and environment delta. It issues no
  command other than the one supplied and starts no other process.
- **FR-002**: The port MUST capture the process's stdout and stderr as **raw byte buffers** — no decoding,
  text normalization, locale handling, or truncation — and its **integer exit code**.
- **FR-003**: The port MUST measure the run's wall-clock duration and carry it as the record's
  `SensedDuration` — the sole non-deterministic fact, held apart from the reproducible facts and excluded from
  `canonicalId` (F050 FR-006).
- **FR-004**: `senseExecution` MUST assemble a complete F032 `CommandRecord` by **delegating to F050
  `ExecutionRecord.recordOf`** — digesting the captured stdout into `StdoutDigest`, the captured stderr into
  `StderrDigest` (never swapped), and carrying every other reproducible fact (executable, ordered arguments,
  working directory, the env delta's three classes, timeout, exit code, captured-output target) verbatim from
  the command-to-run. It introduces **no** new record representation, normalization, or digest scheme.
- **FR-005**: A **non-zero** exit code MUST be recorded as an ordinary complete record — a failed gate is
  recorded, not rejected (F032 FR-003). `senseExecution` applies **no** success/exit-code/reuse policy.
- **FR-006**: The port MUST **enforce** the supplied `TimeoutLimit`: a process that exceeds it is **terminated**
  and its run recorded as an ordinary outcome (a terminated/sentinel exit code, whatever output was captured,
  and the elapsed duration). `senseExecution` MUST NOT hang — it returns within a bounded time of the limit.
- **FR-007**: A **process-start failure** (e.g. the executable does not exist) MUST be caught and reified as a
  recorded failure outcome (a sentinel exit code + a captured diagnostic), **never thrown** out of
  `senseExecution`.
- **FR-008** (totality / safe failure): `senseExecution` MUST be **total** — for every command-to-run and
  every process outcome (clean exit, non-zero exit, start failure, timeout, and empty / binary / large output)
  it returns an ordinary `CommandRecord` and never throws.
- **FR-009** (deterministic reproducible identity): The record's **reproducible facts** — and therefore
  `canonicalId` — MUST be a function only of the command-to-run and the captured output bytes. No clock
  reading, GUID, absolute temp path, locale, process id, or ambient environment may leak into them. The
  duration is the only sensed fact and is excluded from `canonicalId`; two runs of a deterministic gate over
  the same world yield a **byte-identical** `canonicalId`.
- **FR-010** (port isolation): The **real port** MUST be the **only** place this feature starts a process or
  reads a process's streams. All process I/O is injected behind a port value so tests drive `senseExecution`
  with a deterministic fake port (and real temp-script fixtures) and reach **no** network and **no** governed
  repository.
- **FR-011** (additive): This is a **new** edge library reusing F050/F032 vocabulary verbatim. It references
  **only** F050 `ExecutionRecord` (F032 `CommandRecord` and F014 `Config`/`TimeoutLimit` arriving
  transitively); adds **no** new third-party dependency; bumps **no** schema version; edits **no** existing
  core, host command, or golden baseline; introduces **no** new persisted artifact; and is **referenced by
  nothing on landing**.
- **FR-012**: `senseExecution` MUST perform **no** persistence and reach **no** network of its own — it runs
  the one supplied gate command and returns a value; capturing evidence (F049), persisting the store
  (F047/F048), and any reuse/success policy are downstream host-row concerns.

### Key Entities *(include if feature involves data)*

- **Gate command-to-run**: the **reproducible inputs** for one gate execution — its `Executable`, ordered
  `Argument` list, `WorkingDirectory`, `EnvironmentDelta`, `TimeoutLimit` (F014), and captured-output target
  (`CapturedOutput`). All F032/F014 vocabulary reused verbatim; carries no bytes, clock reading, or product
  vocabulary.
- **Execution port (injected)**: a function value that runs one gate command and yields its **captured
  execution outcome** (raw stdout bytes, raw stderr bytes, exit code) or a start failure. The real port drives
  `System.Diagnostics.Process`; tests supply a deterministic fake and real temp-script fixtures.
- **Captured execution outcome**: the *sensed* result of one run — the raw stdout/stderr bytes, the exit code
  (or sentinel for start failure / timeout), and the measured wall-clock duration. The input F050 `recordOf`
  consumes; produced here for the first time from a real process.
- **Assembled command record**: the complete F032 `CommandRecord` `senseExecution` returns — the bridge from a
  real run to the F049/F047 reuse chain. Reused verbatim from F050/F032; this row introduces no new type for
  it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (close the loop from a real run): A developer can run a real gate process through
  `senseExecution` and receive a `CommandRecord` whose `StdoutDigest` equals F050 `digestOf` of the captured
  stdout bytes and whose `canonicalId` is defined — then derive an F049 `EvidenceRef` from it and `capture`
  the gate's world — **without hand-writing any digest or fabricating any outcome**.
- **SC-002** (failure recorded): A gate exiting non-zero produces a complete record carrying its real exit
  code and the digests of its captured output — the run is recorded, never rejected.
- **SC-003** (timeout bounded): A gate that overruns its `TimeoutLimit` is terminated and recorded within a
  bounded time of the limit; `senseExecution` returns rather than hanging.
- **SC-004** (no throw on start failure): A command-to-run naming a non-existent executable yields a recorded
  failure outcome, not a thrown exception.
- **SC-005** (identity stable across runs): Two runs of the same deterministic gate over the same world
  produce **byte-identical** `canonicalId` despite differing measured durations.
- **SC-006** (identity sensitivity): Every reproducible-fact perturbation (one output byte, one argument, the
  working directory, one env-delta entry, the exit code) changes `canonicalId`; a duration-only difference
  does **not**.
- **SC-007** (port isolation in tests): Every semantic test reaches process I/O **only** through the injected
  port — driven by a deterministic fake and real temp-script fixtures — touching **no** network and **no**
  governed repository.
- **SC-008** (any size, any bytes): Output of any size and any byte content (empty, binary / non-UTF-8, large)
  is captured and digested in full, with no truncation or decoding.

## Assumptions

- **Gate-execution-port scope is intentional and maintainer-confirmed** (this session, via AskUserQuestion,
  and named explicitly as "the following row" by the merged F050 plan/spec): this row is the **first impure
  process-spawning edge** — the gate-execution port — that produces the captured execution outcome F050
  consumes. The **host wiring** that runs gates inside `fsgg route` / `fsgg ship` and persists the grown store
  is the *following* row and is out of scope here.
- **`senseExecution` composes F050 `recordOf` and returns an assembled `CommandRecord`** (maintainer-confirmed
  this session) — mirroring the Snapshot precedent where `senseSnapshot` composes edge I/O with the pure
  `Snapshot.assemble`. The alternative (returning a raw outcome for the caller to assemble) was declined.
- **The port enforces the supplied `TimeoutLimit`** by terminating an overrunning process and recording an
  ordinary terminated outcome (maintainer-confirmed this session) — guaranteeing totality (the port never
  hangs). The alternative (unbounded run, timeout carried only as a fact) was declined.
- **The command-to-run carries the reproducible inputs; the port applies them and records them verbatim.** The
  port does **not** diff the entire process environment into the delta — it records the **declared**
  environment delta it applied — keeping the reproducible facts deterministic and free of ambient-environment
  leakage (FR-009).
- **Captured output is held in memory; the captured-output target is whatever the command-to-run declares**
  (`NoCapturedOutput` in the common case) and is carried into the record verbatim. Introducing a
  captured-output *file* subsystem (writing gate output to disk and locating it) is out of scope.
- **Standard determinism / no-clock discipline** applies to the reproducible facts, exactly as for every core
  in this thread; the wall-clock duration is the sole sensed, excluded fact (F050 FR-006).
- **Reuse / success policy over the recorded outcome** — whether a *failed* gate's evidence should be captured
  or suppressed — is a host-row decision, out of scope, preserving F049/F050's no-new-policy guarantee.
- **The real port uses BCL `System.Diagnostics.Process`** (the Snapshot interpreter precedent) and adds no new
  third-party dependency.

## Out of Scope

- **Wiring into `fsgg route` / `fsgg ship`** to run each selected gate, capture evidence (F049), and persist
  the grown store (F047/F048) during a real run — the host-wiring row.
- **Reuse / success policy over captured evidence** — whether a *failed* (non-zero exit) or timed-out gate's
  outcome should be recorded or suppressed, and any success/exit-code gating. This port records what it ran.
- **A captured-output file subsystem** — writing a gate's output to a file and locating it — beyond carrying
  the declared `CapturedOutput` target verbatim into the record.
- **Sandboxing or resource limits beyond the timeout** — CPU/memory caps, filesystem or network isolation,
  privilege dropping. The port runs the gate as an ordinary child process bounded only by the `TimeoutLimit`.
- **Parallel / concurrent gate execution, scheduling, or retries** — `senseExecution` runs one gate command to
  one outcome.
- **Any change to the F050 `ExecutionRecord` surface, the F032 `CommandRecord` / `canonicalId` surface, the
  F049 `EvidenceCapture` surface, the `fsgg.evidence-reuse-store/v1` schema, or the `route.json` / `audit.json`
  schema or content.**
- **Editing merged F029–F050 cores, their golden baselines, or anything in Phase 13.**
