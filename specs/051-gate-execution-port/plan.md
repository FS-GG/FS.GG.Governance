# Implementation Plan: Run A Gate's Process Behind An Injected Execution Port And Assemble Its Command Record

**Branch**: `051-gate-execution-port` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/051-gate-execution-port/spec.md`

## Summary

The cache/evidence-reuse thread (F029–F050) is now pure all the way **from a `CommandRecord` onward**: F050
`ExecutionRecord.recordOf` digests captured output bytes into the two `OutputDigest`s F032 requires and
assembles a complete `CommandRecord` from an *already-captured* execution outcome; F049
`referenceOf`/`capture` derive a reproducible `EvidenceRef` from that record and fold the gate's freshness
world into the bounded reuse store; F047/F048 serialise and persist the grown store across runs. Every step is
pure — **except the one that does not yet exist**: nothing in the codebase ever *runs a gate's process*. F050
consumes "already-captured bytes and already-sensed F032 facts"; producing those — spawning the process,
reading its real stdout/stderr, timing the run, sensing the exit code — is, in F050's own words, "the
**following** row (the gate-execution port)."

This row delivers that row as a new **impure edge** library, `FS.GG.Governance.GateExecution` — the **first
and only** place in the codebase that starts a gate process. It follows the established edge pattern exactly
(the `Snapshot` `realPorts`/`senseSnapshot` and F046 `FreshnessSensing` `realSensor` precedents): an **injected
execution port** isolates all process I/O behind a function value, and a single composition —
`senseExecution` — runs one gate command through the port and applies the pure core. Concretely it mirrors
`senseSnapshot` (edge I/O + the pure `Snapshot.assemble`): **`senseExecution` is edge I/O + the pure F050
`recordOf`**.

Two pieces, over already-merged F050/F032/F014 vocabulary:

1. **A small new domain vocabulary** (`Model`): `GateCommand` — the reproducible inputs for one gate
   execution (`Executable`, ordered `Argument list`, `WorkingDirectory`, `EnvironmentDelta`, `TimeoutLimit`,
   `CapturedOutput`, all F032/F014 types reused verbatim, carrying no bytes); `ExecutionOutcome` — the sensed
   result of one run (raw stdout/stderr `byte[]`, the `ExitCode`, the measured `SensedDuration`); and
   `ExecutionPort = GateCommand -> ExecutionOutcome`, the injected port type. The port is **total**: a start
   failure or timeout is reified by the real port into an ordinary outcome carrying a sentinel exit code,
   never an exception.

2. **The edge** (`Interpreter`): `realPort` — the one place that drives `System.Diagnostics.Process`,
   applying the command's executable/arguments/working-directory/env delta, capturing raw stdout/stderr bytes
   verbatim, sensing the integer exit code and wall-clock duration, **enforcing** the `TimeoutLimit` (kill +
   `timeoutExitCode`), and catching a start failure into `startFailureExitCode` + a captured diagnostic; and
   `senseExecution port command` — runs the command through the port and delegates to F050 `recordOf`,
   returning a complete F032 `CommandRecord`. `senseExecution` is **pure given the port** (it starts no
   process itself), so tests drive it with a deterministic fake port — and exercise `realPort` against real
   temp-script fixtures — reaching no network and no governed repository.

Because the result is an ordinary F032 record, the chain finally closes from a **real executed gate**:
`senseExecution` (this row) → F049 `referenceOf` → F049 `capture` → F047 `serialise`/persist runs from a gate
the system actually ran (FR-004, SC-001). The port is **total and safe**: a non-zero exit is *recorded, not
rejected* (FR-005); a missing executable becomes a recorded failure outcome, not a thrown exception (FR-007);
an overrunning gate is killed and recorded, never left to hang (FR-006). The library is **referenced by
nothing on landing** (exactly as F047/F049/F050 were); the **host wiring** that runs gates during a real
`fsgg route` / `fsgg ship` and persists the grown store is the *following* row and is out of scope here
(FR-011, FR-012).

The committed contract (`Model.fsi` + `Interpreter.fsi`) lives in [contracts/](./contracts/); the port and
composition semantics in [data-model.md](./data-model.md); the build / exercise / test walkthrough in
[quickstart.md](./quickstart.md); and the resolved decisions in [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`,
`WarnOn=3390;1182` from `Directory.Build.props`). This row adds one new **impure edge library**,
`FS.GG.Governance.GateExecution`, in the same packable shape as F050 `ExecutionRecord` and the `Snapshot`
sensing edge. No new command, no host edit.

**Primary Dependencies**: `ProjectReference`s only; **no new third-party `PackageReference`**. The library
references exactly one on-graph project: `FS.GG.Governance.ExecutionRecord` (F050 — `recordOf`, and `digestOf`
transitively). `FS.GG.Governance.CommandRecord` (F032 — `CommandRecord`, the reproducible-fact types,
`ExitCode`, `CapturedOutput`, `SensedDuration`) and `FS.GG.Governance.Config` (F014 — `TimeoutLimit`) arrive
**transitively** through F050. Its own edge code is BCL + FSharp.Core only: the process is started through
`System.Diagnostics.Process` / `ProcessStartInfo` and timed with `System.Diagnostics.Stopwatch` — both BCL,
both the established `Snapshot` interpreter precedent. Test frameworks unchanged (Expecto, Expecto.FsCheck,
FsCheck, Microsoft.NET.Test.Sdk, YoloDev.Expecto.TestSdk).

**Storage**: N/A. This library reads and writes no governance file and introduces no persisted artifact. It
captures the running process's output **in memory** as `byte[]`; the captured-output *target* is whatever the
`GateCommand` declares (`NoCapturedOutput` in the common case), carried into the record verbatim — a
captured-output *file* subsystem is out of scope (Assumptions, Out of Scope). The persistence round-trip
referenced by the close-the-loop story (SC-001) is exercised by reusing already-merged F049
`referenceOf`/`capture` against a record this library assembled — no new persistence is introduced here.

**Testing**: Expecto + FsCheck, in a new `FS.GG.Governance.GateExecution.Tests` project. Two test surfaces
mirror Principle IV's two sides: (a) the **pure-given-the-port** side — drive `senseExecution` with a
deterministic **fake port** returning literal `byte[]` and a fixed `ExitCode`/`SensedDuration`, asserting over
the returned record, its digests, and `CommandRecord.canonicalId` with **no** process at all; (b) the **edge**
side — drive `realPort` against **real temp-script fixtures** (tiny `/bin/sh` scripts that print known bytes
and exit with a chosen code; a guaranteed-missing executable; a script that sleeps past a short
`TimeoutLimit`), asserting real capture, real exit codes, the sentinel outcomes, totality, and bounded timeout
return. The close-the-loop tests drive the **real** F049 `EvidenceCapture.referenceOf`/`capture` over a record
`senseExecution` assembled. Output digests under test are **derived from real captured bytes**
(`ExecutionRecord.digestOf <captured>`), never `Synthetic` literals — that is the whole point of the row. The
new public surface is guarded by a reflective surface-drift baseline
(`surface/FS.GG.Governance.GateExecution.surface.txt`). No network, no governed repository (SC-007).

**Target Platform**: Developer / CI .NET SDK running `dotnet test` on Linux (the repo's CI shell). The edge
spawns ordinary child processes through the BCL; the real temp-script fixtures are `/bin/sh` scripts, matching
the `Snapshot` tests' use of real `git`. No OS-specific surface in the library itself — the platform-specific
detail is confined to the fixtures, not the contract.

**Project Type**: A single new **impure edge library** — the first process-spawning capability in the
codebase. **Principle IV applies and is satisfied by the injected-port / interpreter boundary** (see
Constitution Check): `senseExecution` is pure given the port, I/O is represented as an injected effect
function value, and interpretation (`realPort` starting the process) happens only at the edge — the same
separation the merged `Snapshot` `senseSnapshot`/`realPorts` and F046 `realSensor` edges use. A one-shot
"run one process → one outcome → one record" has no durable `Model`/`Msg` stream, so a full Elmish `Program`
would be ceremony Principle III discourages.

**Performance Goals**: N/A. The added cost is one child-process spawn plus one SHA-256 pass over each captured
stream (via F050 `recordOf`) plus one F032 `build`. Output of **any size** is captured and digested in full
with no truncation (SC-008). The contracts are totality, safe failure, **bounded** timeout return, and
deterministic reproducible identity — not latency. The single timing concern is FR-006: the edge MUST return
within a bounded time of the `TimeoutLimit`, not run for the full overrun.

**Constraints**: Spawn-once (FR-001): exactly one process per `senseExecution` call, applying the supplied
command and starting no other. Raw-bytes capture (FR-002): stdout/stderr captured as `byte[]` verbatim — no
decoding, normalization, locale, or truncation — plus the integer exit code. Duration apart (FR-003): the
wall-clock `SensedDuration` is the sole non-deterministic fact, carried only in `record.Duration`, excluded
from `canonicalId` (F050 FR-006). Verbatim delegation (FR-004): the record is assembled **only** by F050
`recordOf` — no new record shape, normalization, or digest scheme; stdout's digest in `StdoutDigest`,
stderr's in `StderrDigest`, never swapped; every reproducible fact carried from the command. Record-not-reject
(FR-005): a non-zero exit is an ordinary complete record; no success/exit-code/reuse policy. Enforced timeout
(FR-006): an overrunning process is **terminated** and recorded (`timeoutExitCode`, partial output, elapsed
duration); `senseExecution` never hangs. Safe start failure (FR-007): a process-start failure is caught and
reified (`startFailureExitCode` + captured diagnostic), never thrown. Total (FR-008): for every command and
every outcome (clean / non-zero / start-failure / timeout / empty / binary / large output) an ordinary
`CommandRecord` is returned and nothing throws. Deterministic identity (FR-009): the reproducible facts — and
`canonicalId` — are a function only of the command-to-run and the captured bytes; no clock/GUID/abs-temp-path/
locale/pid/ambient-env leaks in; two deterministic runs over the same world yield a byte-identical
`canonicalId`. Port isolation (FR-010): `realPort` is the only place a process starts or a stream is read;
all process I/O is injected behind the port value. Additive (FR-011): a new edge library reusing F050/F032/F014
vocabulary verbatim, one new `ProjectReference` (F050), no new third-party package, no schema bump, no edit to
any core/host/golden-baseline, no new persisted artifact, referenced by nothing on landing. No persistence /
no network (FR-012): the library runs the one supplied gate and returns a value; capture, persistence, and
reuse/success policy are downstream host concerns.

**Scale/Scope**: Additive only. New files: `src/FS.GG.Governance.GateExecution/` (`Model.fsi`, `Model.fs`,
`Interpreter.fsi`, `Interpreter.fs`, `.fsproj`), `tests/FS.GG.Governance.GateExecution.Tests/` (`.fsproj` +
test files), `surface/FS.GG.Governance.GateExecution.surface.txt`, a `scripts/prelude.fsx` section, the two
`.sln` entries, and the `CLAUDE.md` plan pointer. **Zero** edits to F029/F030/F032/F041–F050 cores, host
commands, or their golden baselines; **zero** new third-party dependencies; **no** schema bump; **no** change
to the F050 `ExecutionRecord` / F032 `CommandRecord` / F049 `EvidenceCapture` surfaces or the
`fsgg.evidence-reuse-store/v1` / `route.json` / `audit.json` schemas.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle | Status | Justification |
|-----------|--------|---------------|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | FSI-first is satisfied by committing `contracts/Model.fsi` + `contracts/Interpreter.fsi` **before any `.fs` body** and writing public-surface semantic tests (driving `senseExecution` through a fake port and `realPort` against real temp-script fixtures, plus `CommandRecord.canonicalId` and F049 `referenceOf`/`capture`, never private helpers) that fail before implementation. The `scripts/prelude.fsx` F051 section is the documentation-of-record FSI transcript — the runnable honest-audience exercise of the shipped surface (pure-given-fake-port demo + a real-edge demo) — not the design-time sketch (the two `.fsi` are that). |
| II. Visibility lives in `.fsi` | PASS | Every public symbol is declared in the curated `Model.fsi` (`GateCommand`, `ExecutionOutcome`, `ExecutionPort`) and `Interpreter.fsi` (`startFailureExitCode`, `timeoutExitCode`, `realPort`, `senseExecution`); the `.fs` files carry no `private`/`internal`/`public` modifiers (process-spawning helpers live unexported in `Interpreter.fs`, kept off the surface by absence from the `.fsi`); the new `surface/FS.GG.Governance.GateExecution.surface.txt` baseline is guarded by the existing reflective drift-test pattern, plus a scope-hygiene assertion. |
| III. Idiomatic Simplicity | PASS | The plainest F#: `Model` is three declarations; `senseExecution` is one expression (`port command` then `ExecutionRecord.recordOf …` over the command's facts and the outcome's bytes/exit/duration); `realPort` is a single `try`/`with` BCL pipeline over `ProcessStartInfo` + `Stopwatch` + `WaitForExit(timeout)` mirroring the merged `Snapshot` `runGit`. Local **mutation is disclosed and confined to the edge** — `MemoryStream`/`Process` are inherently mutable BCL objects, and async stream draining uses ordinary `Task` reads; no custom operators, SRTP, reflection (outside tests), type providers, recursion, or non-trivial CEs. Process spawning is the BCL standard library reused from the `Snapshot` precedent, not a clever abstraction. |
| IV. Elmish/MVU boundary | PASS (satisfied by the injected-port / interpreter boundary) | This is the codebase's first I/O edge in this thread, so Principle IV is **live** — and met the way the constitution explicitly permits for "libraries, CLIs, and small tools": a local effect boundary where I/O is represented as data (the injected `ExecutionPort` function value), the logic is pure given that value (`senseExecution` = `port` ∘ pure `recordOf`, starting no process itself), and interpretation happens **only at the edge** (`realPort` drives `System.Diagnostics.Process`). This is the **same** boundary the merged `Snapshot` `senseSnapshot`/`realPorts` and F046 `FreshnessSensing` `realSensor` edges use. A one-shot run (one process → one outcome → one record) owns no durable `Model` and accepts no `Msg` stream, so a full Elmish `Program`/`update` would be the ceremony Principle III warns against. Both sides are tested: pure-given-the-port tests (fake port) and interpreter tests against real processes (real temp-script fixtures), plus an FSI transcript. |
| V. Test Evidence | PASS | Semantic tests fail before the bodies exist and pass after, driving the public FSI surface against **real** captured bytes, **real** F032 `build`/`canonicalId`, **real** child processes (the edge side), and **real** F049 operations (close-the-loop) — reaching no network and no governed repository (SC-007). Output digests are **derived from real captured bytes**, not `Synthetic` literals; this row removes the last hand-fabricated outcome on the capture path (F050 could only assemble from supplied bytes; now the bytes are sensed from a real run), so the disclosure discipline is satisfied by the **absence** of synthetic data on this path. The fake port is a deterministic test double over real `byte[]`, not a stand-in for unavailable real evidence (the real evidence — `realPort` against real scripts — is also exercised). |
| VI. Observability & Safe Failure | PASS | The edge is **total and never silently swallows** (FR-008): a start failure becomes a recorded outcome carrying a named `startFailureExitCode` and a **captured diagnostic** (the failure message in the stderr bytes), never a swallowed exception; a timeout becomes a recorded outcome carrying a named `timeoutExitCode`; a non-zero exit is recorded verbatim. The two named sentinel exit codes are exported precisely so a consumer can **distinguish a tool-level failure-to-start / timeout from an ordinary gate exit** — the Principle VI requirement to distinguish a defect from malformed input — rather than reporting one as the other. The edge fails explicitly (recorded sentinel outcome) and never hangs (bounded timeout return, FR-006). |

**Change Classification**: **Tier 1 (contracted change)** — adds new public API surface (a new packable
library with a new domain vocabulary, an injected port type, the real port, and the `senseExecution`
composition, plus a new surface baseline). Requires the full artifact chain: spec, plan, `.fsi`, surface
baseline, and test evidence. **No** new third-party dependency is added; **no** schema version is bumped;
**no** existing public surface is modified (the library is referenced by nothing on landing).

**Engineering Constraints**: net10.0 ✅; each new public module carries a curated `.fsi` ✅; a surface baseline
is added ✅; no new third-party dependency ✅ (BCL `System.Diagnostics.Process`/`Stopwatch` + FSharp.Core,
layered on the already-on-graph F050 → F032 → F014 chain); `FS.GG.Governance.*` namespace ✅; existing
packages' pack output unaffected ✅ (the new library is independently packable, like F050/F049/F047); one-way
operating rule unaffected — the port runs *whatever executable the caller supplies*, assuming no rendering
package IDs, template names, or layout ✅. No violations → **Complexity Tracking is empty**.

## Project Structure

### Documentation (this feature)

```text
specs/051-gate-execution-port/
├── plan.md              # This file (/speckit-plan command output)
├── spec.md              # Feature specification (input)
├── research.md          # Phase 0 output — the resolved decisions
├── data-model.md        # Phase 1 output — port + composition semantics
├── quickstart.md        # Phase 1 output — build/exercise/test walkthrough
├── contracts/
│   ├── Model.fsi              # Phase 1 output — GateCommand, ExecutionOutcome, ExecutionPort
│   └── Interpreter.fsi        # Phase 1 output — sentinels, realPort, senseExecution
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.GateExecution/                    # NEW (this row)
├── Model.fsi          # curated surface: GateCommand, ExecutionOutcome, ExecutionPort (F032/F014 verbatim)
├── Model.fs           # the three domain declarations (the port is a function-type abbreviation)
├── Interpreter.fsi    # curated surface: startFailureExitCode, timeoutExitCode, realPort, senseExecution
├── Interpreter.fs     # realPort drives System.Diagnostics.Process (the ONLY process spawn);
│                       # senseExecution = port command + pure ExecutionRecord.recordOf
└── FS.GG.Governance.GateExecution.fsproj   # ProjectReference ExecutionRecord (F050); compile order
                                            # Model.fsi → Model.fs → Interpreter.fsi → Interpreter.fs

surface/
└── FS.GG.Governance.GateExecution.surface.txt        # NEW reflective baseline (generated via BLESS_SURFACE)

tests/FS.GG.Governance.GateExecution.Tests/           # NEW test project
├── Support.fs            # NEW: fake-port builders (literal byte[] + fixed exit/duration), GateCommand
│                          # builders, real temp-script fixture helpers (/bin/sh), repoRoot finder, FsCheck gens
├── SenseTests.fs         # NEW: US1 — StdoutDigest/StderrDigest = digestOf captured bytes (AC1), every
│                          # reproducible fact carried verbatim incl. the env delta's three classes (AC2),
│                          # driven by both a fake port and a real temp-script fixture
├── FailureTests.fs       # NEW: US2 — non-zero exit recorded (AC1), missing executable → startFailureExitCode
│                          # + captured diagnostic, no throw (AC2), timeout → timeoutExitCode within bounded
│                          # time with partial output + elapsed duration (AC3); real port + real fixtures
├── IdentityTests.fs      # NEW: US3 — two runs byte-identical canonicalId despite differing Duration (AC1),
│                          # single-reproducible-fact perturbation changes canonicalId (AC2), duration-only
│                          # difference does not (AC3)
├── CloseLoopTests.fs     # NEW: US1 AC3 — senseExecution record → F049 referenceOf/capture reproducible
├── SurfaceDriftTests.fs  # NEW: reflective surface baseline + scope-hygiene assertion (Principle II)
├── Main.fs               # NEW: Expecto entry point
└── FS.GG.Governance.GateExecution.Tests.fsproj   # refs GateExecution + ExecutionRecord + CommandRecord +
                                                   # Config; TEST-ONLY EvidenceCapture/EvidenceReuse/
                                                   # FreshnessKey for the close-the-loop round-trip only

scripts/prelude.fsx                                    # + an F051 GateExecution walkthrough section
FS.GG.Governance.sln                                   # + the new src + test project entries

# Untouched (additive guarantee): F050 ExecutionRecord, F049 EvidenceCapture, F047/F048 EvidenceReuseStore,
# F032 CommandRecord, F030 EvidenceReuse, F029 FreshnessKey, F046 FreshnessSensing, the Snapshot edge, all
# F041–F050 cores and host commands, every route.json/audit.json/cache-eligibility golden baseline, the
# fsgg.evidence-reuse-store/v1 schema, and the command-record-identity-format contract.
```

**Structure Decision**: Deliver a **new standalone impure edge library** layered on top of the already-merged
F050 core (constitution: heavier capabilities layer on top, not into the core; the Snapshot / F046 / F050
precedent), rather than adding a process spawn to `ExecutionRecord` — that would make a *pure value library*
do I/O, contradicting F050's explicit "no process … pure computation, not I/O" guarantee and editing a frozen
merged surface. The library mirrors the `Snapshot` edge's `Model` + `Interpreter` split: `Model` holds the
domain vocabulary and the injected port **type**; `Interpreter` holds the real port (the I/O edge) and the
`senseExecution` composition. Unlike `Snapshot`, it needs **no** pure middle module — the pure core it composes
is F050 `recordOf`, reused verbatim. It is referenced by nothing on landing; the host wiring that runs gates
inside `fsgg route` / `fsgg ship` and persists the grown store is the explicitly out-of-scope following row.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
