# Phase 0 Research: Gate-Execution Port

All NEEDS CLARIFICATION from the Technical Context are resolved below. Each decision records what was chosen,
why, and the alternatives rejected. Two scope decisions were maintainer-confirmed this session via
AskUserQuestion and are recorded as D1/D2.

## D1 — `senseExecution` returns an assembled `CommandRecord` (not a raw outcome)

**Decision**: The port composition `senseExecution` returns a complete F032 `CommandRecord`, assembled by
delegating to the pure F050 `ExecutionRecord.recordOf`. It does **not** return a raw `ExecutionOutcome` for
the caller to assemble.

**Rationale**: Maintainer-confirmed this session. It mirrors the `Snapshot` precedent exactly —
`senseSnapshot` is edge I/O composed with the pure `Snapshot.assemble`, returning the assembled
`RepoSnapshot`, not raw git output. Here `senseExecution` is edge I/O composed with the pure `recordOf`,
returning the assembled `CommandRecord`. Returning the record (not the outcome) is what closes the chain at
the edge: a caller gets a value that flows straight into F049 `referenceOf`/`capture` with no intermediate
assembly step, and the only place that knows how to assemble a record (F050) is the only place that does it.

**Alternatives rejected**: Returning a raw `ExecutionOutcome` and leaving assembly to the caller — declined.
It would duplicate the "call `recordOf` with the command's facts + the outcome's bytes" wiring at every call
site, invite a caller to assemble it wrongly (swap the digests, drop a fact), and break the senseSnapshot
symmetry. The `ExecutionOutcome` type still exists (it is what the **port** yields), but it is an internal
seam between the port and `recordOf`, not the library's return value.

## D2 — The port enforces the `TimeoutLimit` (kill + record), never hanging

**Decision**: `realPort` **enforces** the supplied `TimeoutLimit`: it waits for the process up to the limit,
and if the process overruns it **terminates** the process (kills the process and its children) and records an
ordinary outcome — `timeoutExitCode`, whatever output was captured so far, and the elapsed wall-clock
duration. `senseExecution` returns within a bounded time of the limit and never hangs (FR-006, SC-003).

**Rationale**: Maintainer-confirmed this session. Totality is not optional for the first process-spawning edge
— a governance run cannot hang because a gate misbehaved. Enforcing the timeout at the edge is the only way to
guarantee the port is total over *every* process outcome, including a runaway one. The `TimeoutLimit` is then
both **enforced** (the kill) and **recorded** (carried verbatim into the record's reproducible facts by
`recordOf`), so the limit that was applied is also the limit that appears in the canonical identity.

**Alternatives rejected**: An unbounded run with the timeout carried only as a *fact* (recorded but not
enforced) — declined. It would let a hanging gate hang the whole run, defeating the safety guarantee the row
exists to provide, and would make `senseExecution`'s termination depend on the gate's good behavior.

## D3 — The injected port returns a **total** `ExecutionOutcome` (sentinel-reified failures), not a `Result`

**Decision**: `type ExecutionPort = GateCommand -> ExecutionOutcome`. The port is **total**: it always yields
an `ExecutionOutcome`. A process-start failure and a timeout are reified by `realPort` into ordinary outcomes
carrying a **sentinel exit code** (`startFailureExitCode` / `timeoutExitCode`) and — for a start failure — the
failure message captured into the stderr `byte[]`. The port never returns an error arm and never throws.

**Rationale**: This is what makes `senseExecution` trivially total (FR-008) — it always receives an outcome
and always calls `recordOf`, with no error-handling fork. It keeps the failure *visible in the record itself*
(a sentinel exit code + a captured diagnostic are ordinary `CommandRecord` content, exactly the "a failed gate
is evidence" stance of F032 FR-003), rather than out-of-band in a `Result.Error`. The spec's wording — the
port "yields its captured execution outcome … **or a start failure**" — is satisfied by the start failure
being reified *into* an outcome with a sentinel, not by a separate error channel. Observability (Principle VI)
is served by the two **named, exported** sentinels, so a consumer can tell a failure-to-start / timeout from
an ordinary non-zero gate exit.

**Alternatives rejected**: `GateCommand -> Result<ExecutionOutcome, StartFailure>` (the `Snapshot`
`GitPort = GitCommand -> Result<string,string>` shape) — declined here. Snapshot returns a `Result` per *git
sub-command* and folds many results into one snapshot with diagnostics; this row runs *one* gate to *one*
outcome and the "failure" is itself the evidence to record, so reifying it into the outcome (rather than a
sibling error arm) is both simpler and more faithful to F032's "record, not reject". `senseExecution` would
otherwise have to map both `Result` arms back into an outcome anyway — the `Result` would buy nothing.

## D4 — Module layout: `Model` (vocabulary + port type) + `Interpreter` (edge + composition); no pure middle

**Decision**: Two module pairs, mirroring the `Snapshot` edge minus its pure middle:
`Model.fsi`/`Model.fs` holds `GateCommand`, `ExecutionOutcome`, and the `ExecutionPort` type abbreviation;
`Interpreter.fsi`/`Interpreter.fs` holds `startFailureExitCode`, `timeoutExitCode`, `realPort`, and
`senseExecution`. There is **no** `GateExecution.fs` pure-core module — the pure core composed here is F050
`recordOf`, reused verbatim.

**Rationale**: `Snapshot` is `Model` (domain types) + `Snapshot` (pure `assemble`) + `Interpreter` (edge). The
analogue minus the pure middle is exactly this layout: domain vocabulary in `Model`, the edge + the one-line
composition in `Interpreter`. Keeping the port *type* in `Model` (with the other vocabulary) and the *real
port value* + `senseExecution` in `Interpreter` matches `Snapshot`, where `GitPort`/`Ports` and
`realPorts`/`senseSnapshot` live in `Interpreter` — here the port type sits with the data it transports
because it carries no I/O itself; the I/O lives only in `realPort`. This keeps `Model` pure data and
`Interpreter` the sole I/O surface.

**Alternatives rejected**: A single `GateExecution.fsi`/`.fs` holding everything — workable but blurs the
data/edge line the codebase draws consistently (Snapshot, FreshnessSensing). A three-module split copying
Snapshot's pure middle — rejected: there is no new pure transformation to host; `recordOf` already is it, and
an empty pass-through middle is ceremony Principle III discourages.

## D5 — Real edge uses BCL `System.Diagnostics.Process` + `Stopwatch`; output drained as raw bytes

**Decision**: `realPort` starts the process with `ProcessStartInfo` (`RedirectStandardOutput`/`Error = true`,
`UseShellExecute = false`, `ArgumentList` for the ordered arguments, `WorkingDirectory` set, and the
`EnvironmentDelta`'s three classes applied to `psi.Environment`), reads **raw bytes** from the redirected
output/error base streams (not the text `StandardOutput.ReadToEnd`, which would decode), times the run with a
`Stopwatch`, and waits with a bounded `WaitForExit(timeout)`-style wait. All of it is wrapped in `try/with`
so a start failure is caught.

**Rationale**: This is the established `Snapshot` interpreter precedent (`runGit` over `ProcessStartInfo` with
redirected streams, exit code, exceptions caught and reified) — no new third-party dependency. Reading the
**base byte streams** (e.g. draining `proc.StandardOutput.BaseStream` / `StandardError.BaseStream` into a
`MemoryStream`) rather than `ReadToEnd()` is required by FR-002: the capture must be raw bytes with no
decoding, locale, or normalization, so binary / non-UTF-8 output round-trips byte-exactly (Edge cases). Both
streams are drained **concurrently** (one on a background read) to avoid the classic pipe-buffer deadlock when
a gate fills both stdout and stderr.

**Alternatives rejected**: `StandardOutput.ReadToEnd()` (text) — rejected: it decodes to a string under the
process's encoding, losing raw bytes and violating FR-002/SC-008 for binary output. A third-party
process-helper package — rejected: the BCL + the Snapshot precedent already cover it (FR-011, no new
dependency).

## D6 — Timeout termination kills the process tree; partial output and elapsed duration are recorded

**Decision**: On timeout, `realPort` calls `Process.Kill(entireProcessTree = true)`, then collects whatever
output was drained before the kill, sets the exit code to `timeoutExitCode`, and records the `Stopwatch`'s
elapsed time as the `SensedDuration`. The wait is bounded by the `TimeoutLimit` (seconds, F014), so the call
returns shortly after the limit, not after the full overrun.

**Rationale**: FR-006 / SC-003 require termination + an ordinary recorded outcome within a bounded time.
Killing the whole tree prevents an orphaned child from outliving the gate. Recording the partial output and
elapsed duration keeps the timeout outcome an *ordinary* `CommandRecord` (US2 AC3), digestible by `recordOf`
like any other. `TimeoutLimit of seconds: int` converts directly to the wait duration.

**Alternatives rejected**: Killing only the direct child (leaving grandchildren) — rejected: a gate that
spawns helpers could leave them running, defeating "never hang / never leak". Discarding partial output on
timeout — rejected: the captured partial output is evidence and US2 AC3 requires it be recorded.

## D7 — Reproducible facts carry the **declared** env delta, not a full ambient-environment diff

**Decision**: The record's `Environment` is the `GateCommand.Environment` **declared** delta, carried verbatim
by `recordOf`. `realPort` *applies* that delta to the child's environment but does **not** diff the entire
process environment back into the record.

**Rationale**: FR-009 / Assumptions. Diffing the full ambient environment would leak machine-specific,
non-deterministic variables (PATH entries, temp paths, locale, PID-derived values) into the reproducible facts
and break the byte-identical `canonicalId` guarantee (SC-005). Recording the *declared* delta keeps the
reproducible facts a pure function of the command-to-run and the captured bytes.

**Alternatives rejected**: Recording the effective child environment — rejected: non-deterministic, ambient,
and identity-breaking.

## D8 — Sentinel exit codes are named, exported values

**Decision**: `startFailureExitCode` and `timeoutExitCode` are exported `ExitCode` values in `Interpreter`.

**Rationale**: Observability (Principle VI) — a consumer (and the tests) must be able to **distinguish** a
failure-to-start or timeout from an ordinary gate exit, and naming them avoids magic numbers scattered across
tests and call sites. They are chosen to be values an ordinary gate would not plausibly return as a success
(documented in data-model.md); they are recorded like any other exit code and never gate behavior in this row
(no success/exit-code policy — FR-005, that is a host concern).

**Alternatives rejected**: Inlined magic numbers — rejected (opaque, drift-prone, untestable by name).
Hiding the sentinels — rejected: tests assert on them and consumers need to recognize them.

## D9 — Packable, like the F047/F049/F050 thread siblings

**Decision**: `IsPackable=true`, `PackageId=FS.GG.Governance.GateExecution`, `Version=0.1.0`, matching the
immediately-preceding thread libraries (F047 `EvidenceReuseStore`, F049 `EvidenceCapture`, F050
`ExecutionRecord`) and the `Snapshot` edge — all independently packable.

**Rationale**: The evidence-reuse thread ships each library as an independently packable unit; F050's `.fsproj`
states the norm explicitly ("packable like EvidenceCapture/CommandRecord/EvidenceReuseStore"). `Snapshot`, the
nearest edge precedent, is also `IsPackable=true`. "Existing packages' pack output unaffected" means existing
packages are untouched, not that this library is unpackable.

**Alternatives rejected**: `IsPackable=false` (the `FreshnessSensing` edge's choice) — a defensible
alternative, but it diverges from the three thread siblings this row directly composes and continues. If the
maintainer prefers the edge-not-packable convention, flipping the one `.fsproj` flag is a trivial, reversible
change.

## D10 — Tests drive real temp-script fixtures for the edge; a fake port for the pure-given-port side

**Decision**: Edge behavior (real capture, real exit codes, the sentinels, bounded timeout, totality) is
tested by driving `realPort` against **real `/bin/sh` temp-script fixtures** — a script printing known bytes
and exiting a chosen code, a guaranteed-missing executable path, and a script sleeping past a 1-second
`TimeoutLimit`. The pure-given-the-port behavior (digest placement, verbatim fact carriage, identity stability
and sensitivity) is tested by driving `senseExecution` with a **deterministic fake port** returning literal
`byte[]` and a fixed `ExitCode`/`SensedDuration`.

**Rationale**: Principle IV requires both sides tested — pure transitions and a real interpreter. Real scripts
exercise the genuine `System.Diagnostics.Process` path (the only honest test of capture/timeout/start-failure,
mirroring the `Snapshot` tests' use of real `git`); the fake port gives deterministic control over bytes and
duration to prove digest placement and identity invariance without process noise. Both reach no network and no
governed repository (SC-007). The platform-specific `/bin/sh` detail is confined to the **fixtures**, matching
the repo's Linux CI; the library contract is OS-neutral.

**Alternatives rejected**: Faking the whole edge (no real process anywhere) — rejected: it would leave the
one capability this row exists to deliver (actually starting a process) untested, exactly the synthetic-only
trap Principle V warns against. Mocking `System.Diagnostics.Process` — rejected: brittle and unfaithful; a
real child process is cheap and honest.
