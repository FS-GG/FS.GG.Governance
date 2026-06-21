// Curated public signature contract for the command-record types (F032).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, YAML-free values the `CommandRecord.build` /
// `canonicalId` operations construct and project over. They REUSE the F014 `TimeoutLimit` verbatim — opened
// from `FS.GG.Governance.Config.Model` — never redefined (FR-009). Everything else is a minimal new
// vocabulary of run facts; nothing here carries raw bytes, a clock reading, or product vocabulary.

namespace FS.GG.Governance.CommandRecord

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The program that ran (path or name as sensed). Opaque, comparable. A reproducible fact.
    type Executable = Executable of string

    /// One command-line argument. The record carries an ORDERED `Argument list`; argument order is
    /// significant in the canonical identity (D6) — `["-a"; "-b"]` is distinct from `["-b"; "-a"]`.
    type Argument = Argument of string

    /// The run's working directory as sensed. Opaque (an arbitrary cwd, not a governed-root path).
    type WorkingDirectory = WorkingDirectory of string

    /// The process exit code — any `int`, incl. non-zero. A failed run is RECORDED, not rejected (FR-003).
    type ExitCode = ExitCode of int

    /// A supplied, already-computed digest of stdout OR stderr. Opaque — no hashing happens here (FR-010,
    /// D3); an empty string is a literal value, distinct from a non-empty one.
    type OutputDigest = OutputDigest of string

    /// An environment-variable name (in the run's environment delta).
    type EnvVarName = EnvVarName of string

    /// An environment-variable value (in the run's environment delta).
    type EnvVarValue = EnvVarValue of string

    /// The path to a captured-output file, when one exists (the `CapturedAt` case below).
    type CapturedOutputPath = CapturedOutputPath of string

    /// The SENSED / non-deterministic wall-clock duration of the run, as an opaque integer measure of
    /// nanoseconds (D3 — no `float`, no `DateTime`, no clock). It is the ONLY sensed fact: carried as
    /// metadata, held structurally apart from the reproducible facts, and EXCLUDED from the canonical
    /// identity (FR-004, FR-005). The name itself flags the non-determinism at every use site.
    type SensedDuration = SensedDuration of nanoseconds: int64

    /// The byte-stable canonical identity over the reproducible facts (FR-005). The wrapped string is the
    /// canonical rendering (`contracts/command-record-identity-format.md`); equality is exact byte equality.
    type CommandIdentity = CommandIdentity of string

    /// A variable the run ADDED (absent in the baseline, present after).
    type AddedVar = { Name: EnvVarName; Value: EnvVarValue }

    /// A variable the run CHANGED (present in both baseline and run, with a different value). It carries
    /// BOTH its baseline `Old` and run `New` value and appears exactly ONCE, in `Changed` — never split into
    /// a `Removed` + `Added` pair (FR-002, D4).
    type ChangedVar = { Name: EnvVarName; Old: EnvVarValue; New: EnvVarValue }

    /// A variable the run REMOVED (present in the baseline, absent after). Carries its baseline `Old` value.
    type RemovedVar = { Name: EnvVarName; Old: EnvVarValue }

    /// The run's environment delta: a three-class PARTITION of changes relative to a baseline (D4), never a
    /// full environment snapshot. Any class may be empty; an entirely empty delta is an ordinary value, not
    /// an error. For the canonical identity each class is compared as a SET (entries rendered to a canonical
    /// string, deduplicated, ordinal-sorted) so supply order and duplicates never affect the identity (D6).
    type EnvironmentDelta =
        { Added: AddedVar list
          Changed: ChangedVar list
          Removed: RemovedVar list }

    /// The captured-output outcome (FR-011, D5). `NoCapturedOutput` is the EXPLICIT, total, locatable "no
    /// captured-output file" — never an empty string that could collide with a real path. It and
    /// `CapturedAt (CapturedOutputPath "")` encode to DISTINCT identity segments (the F029 presence-digit
    /// guarantee), so absence never collides with an empty present path.
    type CapturedOutput =
        | CapturedAt of CapturedOutputPath
        | NoCapturedOutput

    /// The nine REPRODUCIBLE facts of a run — the addressable "reproducible part of the run" value and the
    /// SOLE input to `canonicalId` (the sensed duration is deliberately absent). Every field is reproducible
    /// from the command and its context (FR-005). `Timeout` is the F014 `TimeoutLimit`, reused verbatim.
    type ReproducibleFacts =
        { Executable: Executable
          Arguments: Argument list
          WorkingDirectory: WorkingDirectory
          Environment: EnvironmentDelta
          Timeout: TimeoutLimit
          ExitCode: ExitCode
          StdoutDigest: OutputDigest
          StderrDigest: OutputDigest
          CapturedOutput: CapturedOutput }

    /// The complete command record (FR-001) — all ten declared facts, none dropped or optional-by-omission.
    /// The sensed `Duration` is a SEPARATE field of a distinct type (D2): reachable as sensed metadata via
    /// `record.Duration`, structurally apart from `record.Reproducible`, and structurally impossible to fold
    /// into the canonical identity. A consumer reads `record.Reproducible.*` for the reproducible facts.
    type CommandRecord =
        { Reproducible: ReproducibleFacts
          Duration: SensedDuration }
