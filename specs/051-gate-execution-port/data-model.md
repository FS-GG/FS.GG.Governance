# Phase 1 Data Model: Gate-Execution Port

This row introduces a small domain vocabulary and one edge composition. It reuses F032/F014 types verbatim and
introduces no persisted artifact, no schema, and no new identity scheme. The full curated surface is in
[contracts/Model.fsi](./contracts/Model.fsi) and [contracts/Interpreter.fsi](./contracts/Interpreter.fsi);
this document describes the entities, their fields, the port contract, and the composition semantics.

## Entities

### `GateCommand` — the command-to-run (reproducible inputs)

The reproducible inputs for one gate execution. All fields are F032/F014 types reused verbatim; the record
carries no bytes, no clock reading, and no product vocabulary.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| `Executable` | `Executable` (F032) | command | the program to run (path or name) |
| `Arguments` | `Argument list` (F032) | command | ORDERED; argument order is significant in the identity (F032 D6) |
| `WorkingDirectory` | `WorkingDirectory` (F032) | command | the cwd to apply |
| `Environment` | `EnvironmentDelta` (F032) | command | three-class delta (Added/Changed/Removed); any class may be empty |
| `Timeout` | `TimeoutLimit` (F014) | command | `TimeoutLimit of seconds: int`; ENFORCED by the real port (FR-006) |
| `CapturedOutput` | `CapturedOutput` (F032) | command | `NoCapturedOutput` in the common case; carried verbatim into the record |

**Validation**: none added — every field is an already-validated F032/F014 value. An empty `Arguments` list,
an entirely empty `Environment` delta, and `NoCapturedOutput` are all ordinary values, not errors.

### `ExecutionOutcome` — the captured execution outcome (sensed result)

The sensed result of one run — the value the port yields and F050 `recordOf` consumes.

| Field | Type | Determinism | Notes |
|-------|------|-------------|-------|
| `Stdout` | `byte[]` | reproducible (content) | raw bytes verbatim — no decoding/locale/normalization/truncation (FR-002) |
| `Stderr` | `byte[]` | reproducible (content) | raw bytes verbatim; for a start failure, holds the captured diagnostic |
| `ExitCode` | `ExitCode` (F032) | reproducible | the integer exit code, or a sentinel (`startFailureExitCode` / `timeoutExitCode`) |
| `Duration` | `SensedDuration` (F032) | **SENSED / non-deterministic** | wall-clock measure (nanoseconds, `int64`); the SOLE sensed fact, excluded from `canonicalId` (F050 FR-006) |

The empty-output case yields a zero-length `byte[]`, which `digestOf` maps to the fixed empty-bytes digest
(F050 FR-003) — an ordinary value.

### `ExecutionPort` — the injected port (effect as data)

```fsharp
type ExecutionPort = GateCommand -> ExecutionOutcome
```

A function value that runs one gate command and yields its captured outcome. **Total by contract**: it always
returns an `ExecutionOutcome`; a start failure or timeout is reified into an outcome with a sentinel exit code,
never an exception (FR-007, FR-008). This is the only seam through which the feature touches a process
(FR-010). The real implementation is `Interpreter.realPort`; tests supply a deterministic fake of this exact
shape (the I/O is represented as data, Principle IV).

## Edge contract

### Sentinel exit codes

| Value | When recorded | Carries |
|-------|---------------|---------|
| `startFailureExitCode` | the process fails to start (e.g. missing executable) | this sentinel + the failure message captured in the stderr bytes |
| `timeoutExitCode` | the process is terminated for exceeding its `TimeoutLimit` | this sentinel + the partial output captured before the kill + the elapsed duration |

Both are exported `ExitCode` values (research D8) so consumers and tests can distinguish them by name from an
ordinary gate exit (Principle VI). They are recorded like any other exit code — this row applies no
success/exit-code policy (FR-005); whether a sentinel outcome's evidence is reused or suppressed is a host
concern (Out of Scope). Concrete values are chosen to be ones an ordinary successful gate would not return
(documented at the definition site in `Interpreter.fs`).

### `realPort: ExecutionPort` — the real edge (the only process spawn)

Behavior, all confined to this one binding (FR-010):

1. Build `ProcessStartInfo` from the `GateCommand`: the `Executable`, the ORDERED `Arguments` via
   `ArgumentList` (no shell string-splitting), `WorkingDirectory`, `RedirectStandardOutput = true`,
   `RedirectStandardError = true`, `UseShellExecute = false`; apply the `Environment` delta's three classes to
   `psi.Environment` (Added/Changed set the value; Removed deletes the key). The delta is **applied** but not
   diffed back (research D7).
2. Start a `Stopwatch`, then `Process.Start` inside a `try/with`. A start failure (`null` process or a thrown
   exception) is caught → an outcome with `startFailureExitCode`, the exception message as the stderr bytes,
   empty stdout bytes, and the elapsed duration (FR-007).
3. Drain the redirected **base byte streams** into in-memory buffers, both streams **concurrently** (one on a
   background read) to avoid pipe-buffer deadlock. Raw bytes only — never `ReadToEnd()` text (FR-002, research
   D5).
4. Wait for exit **bounded by `TimeoutLimit`**. On a clean/within-limit exit: capture the drained bytes, the
   real integer `ExitCode`, and the elapsed duration. On overrun: `Kill(entireProcessTree = true)`, capture
   whatever was drained, set `timeoutExitCode`, record the elapsed duration, and return — within a bounded
   time of the limit, never the full overrun (FR-006, research D6).
5. Spawn exactly **once** per call; issue no other command and start no other process (FR-001).

`realPort` never throws and never hangs (FR-008): every outcome — clean, non-zero, start-failure, timeout,
empty/binary/large output — is an ordinary `ExecutionOutcome`.

### `senseExecution: ExecutionPort -> GateCommand -> CommandRecord` — the composition

Edge I/O + the pure F050 `recordOf` (mirrors `senseSnapshot` = edge I/O + pure `assemble`). One expression:

```fsharp
let senseExecution (port: ExecutionPort) (command: GateCommand) : CommandRecord =
    let outcome = port command
    ExecutionRecord.recordOf
        command.Executable
        command.Arguments
        command.WorkingDirectory
        command.Environment
        command.Timeout
        outcome.ExitCode
        outcome.Stdout            // → StdoutDigest via recordOf's digestOf (never swapped)
        outcome.Stderr            // → StderrDigest via recordOf's digestOf
        command.CapturedOutput
        outcome.Duration
```

- **Pure given the port**: `senseExecution` starts no process itself — all I/O is in the injected `port`. With
  a fake port it is fully deterministic and I/O-free (FR-010, Principle IV).
- **Verbatim delegation** (FR-004): the record is assembled ONLY by `recordOf`. The two captured buffers
  become the two output digests (stdout → `StdoutDigest`, stderr → `StderrDigest`, never swapped); the exit
  code and duration come from the outcome; every other reproducible fact is carried verbatim from the command.
  No new record shape, normalization, or digest scheme.
- **Record-not-reject** (FR-005): a non-zero (or sentinel) exit code flows through as an ordinary record; no
  success/exit-code/reuse gate.

## Reproducible identity (FR-009, US3)

`CommandRecord.canonicalId` of `senseExecution`'s result is, by construction (F050 FR-006), a function only of
the **reproducible facts** — executable, ordered arguments, working directory, the env delta's three classes,
timeout, exit code, the two output digests (of the captured bytes), and the captured-output target. The
`SensedDuration` is excluded.

| Property | Holds because |
|----------|---------------|
| Stable across runs (SC-005) | every reproducible fact is the declared command (D7: no ambient-env diff) or the captured bytes; the real port leaks no clock/GUID/abs-temp-path/locale/pid into them, so two deterministic runs assemble byte-identical `canonicalId` |
| Sensitivity (SC-006) | a one-byte output change → a different `digestOf` → a different identity; an argument, the working directory, an env-delta entry, or the exit code each feed the identity directly via `recordOf` → `build` |
| Duration-invariant (SC-006) | `SensedDuration` is never read by `digestOf` or `canonicalId`; two outcomes differing only in duration assemble to a byte-identical identity (and therefore, via F049, a byte-identical `EvidenceRef`) |

## What this row does NOT add

No new persisted artifact, no schema version bump, no new identity/digest scheme (reuses F050/F032), no
success/exit-code/reuse policy, no captured-output file subsystem, no sandboxing beyond the timeout, no
parallel/retry execution, and no edit to any F029–F050 core, host command, golden baseline, or the
`fsgg.evidence-reuse-store/v1` / `route.json` / `audit.json` schemas.
