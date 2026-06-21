// Curated public signature contract for the command-record operations (F032).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching CommandRecord.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any CommandRecord.fs
// body exists (Principle I). All three operations are PURE and TOTAL (FR-003, FR-008): defined for every
// well-typed input, never throwing, reading no clock, filesystem, git, environment, or network, spawning no
// process, and hashing no bytes; byte-for-byte identical for identical input regardless of evaluation time,
// machine, process, or working directory. This row performs NO execution/timing/persistence/provenance and
// adds NO CLI: its sole outputs are the `CommandRecord` value and its `CommandIdentity`.
//
// Naming note (F029 precedent): the operations module `CommandRecord` and the `Model.CommandRecord` record
// type are distinct CLR entities (a module suffix vs a type); they share a name by intent, as F019's `Route`
// and F029's pattern do.

namespace FS.GG.Governance.CommandRecord

open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CommandRecord =

    /// Assemble the ten supplied run facts (curried in the design row's field order) into one complete
    /// `CommandRecord`. PURE and TOTAL: defined for every well-typed argument tuple, never throwing — a
    /// non-zero `ExitCode`, a run whose `TimeoutLimit` applied, an empty `arguments` list, and an empty
    /// `EnvironmentDelta` all produce ordinary complete records (FR-003, Edge cases). Carriage is VERBATIM:
    /// each fact reads back unchanged (arguments in the SAME order; the env delta's three classes preserved,
    /// a `Changed` entry never split into `Added` + `Removed`). The sensed `duration` is placed in
    /// `r.Duration` and NOWHERE in `r.Reproducible` (FR-004, D2). No clock/filesystem/git; no normalization
    /// or reordering (canonicalization is `canonicalId`'s job).
    val build:
        executable: Executable ->
        arguments: Argument list ->
        workingDirectory: WorkingDirectory ->
        environment: EnvironmentDelta ->
        timeout: TimeoutLimit ->
        exitCode: ExitCode ->
        stdoutDigest: OutputDigest ->
        stderrDigest: OutputDigest ->
        capturedOutput: CapturedOutput ->
        duration: SensedDuration ->
            CommandRecord

    /// Render a record's REPRODUCIBLE facts to their canonical, deterministic, byte-stable `CommandIdentity`
    /// (`contracts/command-record-identity-format.md`). PURE and TOTAL: defined for every `CommandRecord`;
    /// reads no clock/filesystem/git/environment/network, spawns no process, hashes no bytes. Computed ONLY
    /// over `record.Reproducible` — `record.Duration` is NEVER read (D2), so two records differing only in
    /// duration share an identity (FR-006), while differing in ANY reproducible fact (executable, an
    /// argument or its order, working directory, the env delta as a set, timeout, exit code, either digest,
    /// or the captured-output outcome) yields a different identity (FR-006). Each env-delta class is compared
    /// as a SET (order/dup-invariant); arguments are order-SIGNIFICANT (FR-007 vs D6). `NoCapturedOutput`,
    /// `CapturedAt (CapturedOutputPath "")`, and `CapturedAt (CapturedOutputPath "x")` yield three
    /// pairwise-different identities (FR-011). The encoding is INJECTIVE across fields (length prefixes +
    /// unique tags); BCL string building only, no hashing (FR-010).
    val canonicalId: record: CommandRecord -> CommandIdentity

    /// Unwrap a `CommandIdentity` to its canonical string (for storage, messages, tests). TOTAL.
    /// `identityValue (CommandIdentity s) = s`.
    val identityValue: identity: CommandIdentity -> string
