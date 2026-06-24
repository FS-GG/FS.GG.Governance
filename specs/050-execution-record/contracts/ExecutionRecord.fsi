// Curated public signature contract for the pure execution-record bridge (F050).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// ExecutionRecord.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any ExecutionRecord.fs body
// exists (Principle I). Both operations are PURE and TOTAL (FR-008): defined for EVERY input — empty output
// bytes, binary/non-textual bytes, arbitrarily large bytes, a non-zero exit code, an applied timeout, an empty
// environment delta, and every captured-output outcome — never throwing, reading no clock, filesystem, git,
// environment, or network, spawning no process; byte-for-byte identical for identical input regardless of
// evaluation time, machine, process, or collection identity. Hashing the supplied bytes is PURE computation, not
// I/O. This row runs NO gate, senses NO fact, times NOTHING, persists NOTHING, and adds NO CLI: its sole outputs
// are the derived `OutputDigest` and the assembled `CommandRecord` value.
//
// This is the value-only content-addressing bridge that turns a gate's CAPTURED OUTPUT BYTES into the byte-stable
// `OutputDigest`s F032 requires, and assembles the complete F032 `CommandRecord` from an already-captured
// execution outcome — the FIRST and ONLY place in the codebase that hashes output bytes (the gap F032 left open
// at D3: its `OutputDigest` is "a supplied, already-computed digest … no hashing happens here"). The IMPURE edge
// (actually spawning a gate's process, reading its real stdout/stderr, timing the run, sensing the reproducible
// facts) is the FOLLOWING row (the gate-execution port) and is out of scope here; this core consumes
// already-captured bytes and already-sensed F032 facts.
//
// It introduces NO new type — it reuses F032 `OutputDigest`/`CommandRecord`, the reproducible-fact types,
// `SensedDuration`, `CapturedOutput`, and the F014 `TimeoutLimit` VERBATIM — and NO new record representation,
// normalization, or reuse/success policy. `recordOf` is EXACTLY `CommandRecord.build` with `digestOf` composed
// on the two output positions; everything else is `build`'s verbatim carriage. It references ONLY F032
// `CommandRecord`; F014 `Config` (`TimeoutLimit`) arrives transitively. It adds NO new third-party
// PackageReference (BCL `SHA256` + FSharp.Core only), bumps NO schema version, and is referenced by nothing on
// landing.

namespace FS.GG.Governance.ExecutionRecord

open FS.GG.Governance.Config.Model          // TimeoutLimit (F014, transitive via F032)
open FS.GG.Governance.CommandRecord.Model    // Executable, Argument, WorkingDirectory, EnvironmentDelta, ExitCode,
                                             // OutputDigest, CapturedOutput, SensedDuration, CommandRecord

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ExecutionRecord =

    /// Derive the deterministic, byte-stable `OutputDigest` (F032, reused) of a gate's CAPTURED OUTPUT BYTES —
    /// the first and only place in the codebase that hashes output bytes, owning the digesting F032 explicitly
    /// deferred (D3). PURE and TOTAL (FR-001, FR-003, FR-008): defined for EVERY `byte[]` — empty (the fixed
    /// empty-input digest, distinct from every non-empty digest), binary/non-textual (hashed as raw bytes — no
    /// decoding, locale, or normalization), and arbitrarily large (a fixed-form result, no truncation) — never
    /// throwing, no I/O, no process. A function of byte CONTENT ONLY (FR-001, FR-009): never reads the sensed
    /// duration or any wall-clock/GUID/path/locale/environment, and does not depend on the array's identity.
    /// CONTENT-ADDRESSED (FR-002): byte-identical content yields the BYTE-IDENTICAL digest; any byte changed,
    /// added, removed, or reordered yields a DIFFERENT digest. Equal stdout and stderr bytes yield equal digests
    /// (content alone). DETERMINISTIC and BYTE-STABLE (FR-009): identical bytes yield the byte-identical digest on
    /// every run, process, and machine. The scheme is FIXED and INTERNAL (FR-011) — not a policy knob,
    /// configurable algorithm, or caller-varied value; callers supply bytes and receive an opaque `OutputDigest`.
    val digestOf: bytes: byte[] -> OutputDigest

    /// Assemble a complete F032 `CommandRecord` from a CAPTURED EXECUTION OUTCOME: the nine reproducible run facts
    /// (executable, ordered arguments, working directory, environment delta, timeout, exit code, captured-output
    /// outcome) plus the sensed `duration`, together with the RAW stdout and stderr BYTES. It digests `stdout`
    /// into the record's `StdoutDigest`, digests `stderr` into its `StderrDigest`, and DELEGATES the assembly to
    /// `CommandRecord.build` VERBATIM (FR-004) — this argument list is `build`'s exactly, with the two
    /// `OutputDigest` positions replaced by two `byte[]`, so `recordOf` is `build` composed with `digestOf` on the
    /// two output fields, nothing more. It introduces NO new record representation, normalization, or
    /// reuse/success policy. CORRECT POSITIONS (FR-005): stdout's digest lands in `StdoutDigest`, stderr's in
    /// `StderrDigest` — never swapped; every other fact is carried unchanged (arguments in supplied ORDER; the env
    /// delta's three classes preserved — a `Changed` entry never split into `Added`+`Removed`); the sensed
    /// `duration` is placed in `record.Duration` and NOWHERE in `record.Reproducible`. DURATION-INVARIANT (FR-006):
    /// neither `digestOf` nor `CommandRecord.canonicalId` reads the duration, so two outcomes identical in all
    /// reproducible facts (incl. output bytes) and differing ONLY in `duration` assemble to records with the
    /// byte-identical `canonicalId` (and therefore, via F049, the byte-identical `EvidenceRef`). MECHANICAL, not
    /// policy (FR-004, US3): a non-zero `exitCode` or an applied timeout assembles to an ordinary complete record
    /// (a failed run is RECORDED, not rejected — F032 FR-003); success/exit-code gating is the out-of-scope host
    /// row. PURE and TOTAL (FR-008): defined for every input, never throwing, no clock/fs/git/env/network, no
    /// process. DETERMINISTIC and BYTE-STABLE (FR-009): identical bytes and facts yield the byte-identical record
    /// on every run, process, and machine. CLOSE-THE-LOOP (FR-007): `CommandRecord.canonicalId (recordOf …)` is
    /// defined and reproducible, F049 `EvidenceCapture.referenceOf` over it is reproducible, and F049 `capture`
    /// makes the gate's freshness world reusable for exactly that derived reference; any single-reproducible-fact
    /// perturbation (incl. one output byte) changes the identity and the reference.
    val recordOf:
        executable: Executable ->
        arguments: Argument list ->
        workingDirectory: WorkingDirectory ->
        environment: EnvironmentDelta ->
        timeout: TimeoutLimit ->
        exitCode: ExitCode ->
        stdout: byte[] ->
        stderr: byte[] ->
        capturedOutput: CapturedOutput ->
        duration: SensedDuration ->
            CommandRecord
