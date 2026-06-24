// Curated public signature contract for the gate-execution EDGE (F051).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Interpreter.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — the
// process-spawning helpers stay unexported by ABSENCE from this file, not by a keyword.
//
// This is the codebase's FIRST and ONLY process-spawning capability. It is the I/O edge of the
// injected-port / interpreter boundary (Principle IV, the Snapshot precedent): `realPort` is the sole place a
// process starts; `senseExecution` is PURE GIVEN THE PORT (edge I/O + the pure F050 `recordOf`), so tests
// drive it with a deterministic fake and reach no process, no network, no governed repository.

namespace FS.GG.Governance.GateExecution

open FS.GG.Governance.CommandRecord.Model      // ExitCode, CommandRecord
open FS.GG.Governance.GateExecution.Model        // GateCommand, ExecutionOutcome, ExecutionPort

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The sentinel exit code the real port records when the process FAILS TO START (e.g. a missing
    /// executable). The run is reified as an ordinary failure outcome — this sentinel plus a captured
    /// diagnostic (the failure message in the stderr bytes) — never a thrown exception (FR-007). Exported so
    /// a consumer can DISTINGUISH a failure-to-start from an ordinary gate exit (Principle VI).
    val startFailureExitCode: ExitCode

    /// The sentinel exit code the real port records when the process is TERMINATED for exceeding its
    /// `TimeoutLimit` (FR-006). The run is reified as an ordinary outcome — this sentinel, the partial output
    /// captured before the kill, and the elapsed duration. Exported so a consumer can DISTINGUISH a timeout
    /// from an ordinary gate exit (Principle VI).
    val timeoutExitCode: ExitCode

    /// The REAL execution port — the ONE place in the codebase that starts a gate process (FR-001, FR-010).
    /// Drives `System.Diagnostics.Process`: applies the command's executable, ORDERED arguments, working
    /// directory, and environment delta; spawns the process EXACTLY ONCE; captures raw stdout/stderr BYTES
    /// verbatim (no decoding/normalization/truncation, FR-002); senses the integer exit code and the
    /// wall-clock duration (FR-003); ENFORCES the `TimeoutLimit` by terminating an overrunning process and
    /// recording `timeoutExitCode` + partial output + elapsed duration (FR-006); and catches a start failure
    /// into `startFailureExitCode` + a captured diagnostic (FR-007). TOTAL — never throws, never hangs
    /// (FR-008). Reads no governed repository and no network (FR-012).
    val realPort: ExecutionPort

    /// Run one gate command through the injected port and assemble its complete F032 `CommandRecord` — edge
    /// I/O + the pure F050 `recordOf`. Mirrors `Snapshot.senseSnapshot` (edge I/O + the pure
    /// `Snapshot.assemble`). The two captured byte buffers become `StdoutDigest` / `StderrDigest` via
    /// `recordOf` (never swapped); the sensed exit code and duration come from the outcome; EVERY other
    /// reproducible fact (executable, ordered arguments, working directory, the env delta's three classes,
    /// timeout, captured-output target) is carried VERBATIM from the command (FR-004). A non-zero exit is
    /// recorded, not rejected — no success/exit-code/reuse policy (FR-005). PURE GIVEN THE PORT: it starts no
    /// process itself, so a fake port makes it fully deterministic and I/O-free for testing.
    val senseExecution: port: ExecutionPort -> command: GateCommand -> CommandRecord
