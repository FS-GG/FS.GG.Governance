# Contract: Command-Record Public API (F032)

The public surface of `FS.GG.Governance.CommandRecord` and the laws it commits to. This is the contract the
semantic tests pin and the surface baseline guards. Types are defined in `Model.fsi`
([data-model.md](../data-model.md)); operations in `CommandRecord.fsi`.

## Types (module `FS.GG.Governance.CommandRecord.Model`)

```fsharp
open FS.GG.Governance.Config.Model   // F014 TimeoutLimit, reused verbatim (FR-009)

type Executable          = Executable of string
type Argument            = Argument of string
type WorkingDirectory    = WorkingDirectory of string
type ExitCode            = ExitCode of int
type OutputDigest        = OutputDigest of string
type EnvVarName          = EnvVarName of string
type EnvVarValue         = EnvVarValue of string
type CapturedOutputPath  = CapturedOutputPath of string
type SensedDuration      = SensedDuration of nanoseconds: int64
type CommandIdentity     = CommandIdentity of string

type AddedVar   = { Name: EnvVarName; Value: EnvVarValue }
type ChangedVar = { Name: EnvVarName; Old: EnvVarValue; New: EnvVarValue }
type RemovedVar = { Name: EnvVarName; Old: EnvVarValue }

type EnvironmentDelta =
    { Added: AddedVar list; Changed: ChangedVar list; Removed: RemovedVar list }

type CapturedOutput =
    | CapturedAt of CapturedOutputPath
    | NoCapturedOutput

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

type CommandRecord =
    { Reproducible: ReproducibleFacts
      Duration: SensedDuration }
```

## Operations (module `FS.GG.Governance.CommandRecord`)

```fsharp
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

val canonicalId: record: CommandRecord -> CommandIdentity

val identityValue: identity: CommandIdentity -> string
```

## Laws

### `build` — total assembly (FR-001, FR-003)

1. **Total.** Defined for every well-typed argument tuple; never throws. A non-zero `ExitCode`, a run whose
   `TimeoutLimit` applied, an empty `arguments` list, and an empty `EnvironmentDelta` all produce ordinary
   complete records (Edge cases).
2. **Verbatim carriage (SC-001).** In the result `r`:
   - `r.Reproducible.Executable = executable`
   - `r.Reproducible.Arguments = arguments` (same elements, **same order**)
   - `r.Reproducible.WorkingDirectory = workingDirectory`
   - `r.Reproducible.Environment = environment` (the three classes preserved; a `Changed` entry never split
     into `Added` + `Removed` — FR-002)
   - `r.Reproducible.Timeout = timeout`, `r.Reproducible.ExitCode = exitCode`
   - `r.Reproducible.StdoutDigest = stdoutDigest`, `r.Reproducible.StderrDigest = stderrDigest`
   - `r.Reproducible.CapturedOutput = capturedOutput`
   - `r.Duration = duration`
3. **Sensed split (FR-004).** `r.Duration` is the only place the duration appears; `r.Reproducible` contains no
   duration. The duration is reachable as sensed metadata, distinguishable from the reproducible facts.
4. **Deterministic (SC-005).** Identical arguments ⇒ identical record (structural equality), independent of
   evaluation time, machine, process, or working directory.

### `canonicalId` — canonical identity (FR-005, FR-006, FR-007)

1. **Total & pure.** Defined for every `CommandRecord`; reads no clock/filesystem/git/environment/network;
   spawns no process; hashes no bytes. Identical record ⇒ byte-identical `CommandIdentity` (SC-005, idempotent:
   computing twice is equal).
2. **Reproducible-only (FR-005).** Computed **only** over `record.Reproducible`. `record.Duration` is not read.
3. **Duration-invariance (FR-006, SC-004).** If two records share `record.Reproducible` and differ only in
   `Duration`, their identities are **equal**.
4. **Per-field sensitivity (FR-006, SC-004).** If two records differ in **any** reproducible fact — executable,
   any argument (including order), working directory, the environment delta (as a set), timeout, exit code,
   stdout digest, stderr digest, or captured-output outcome — their identities are **different**.
5. **Env-delta order/dup invariance (FR-007, SC-005).** Reordering the entries within any `EnvironmentDelta`
   class, or supplying duplicate entries, does **not** change the identity (each class is compared as a set).
   By contrast, reordering `Arguments` **does** change the identity (argument order is significant).
6. **Captured-output absence is unambiguous (FR-011).** `NoCapturedOutput`, `CapturedAt (CapturedOutputPath
   "")`, and `CapturedAt (CapturedOutputPath "x")` yield three pairwise-different identities.

### `identityValue` — unwrap (total)

`identityValue (CommandIdentity s) = s`. The string is the byte-stable canonical rendering
(`command-record-identity-format.md`), suitable for an audit field.

## Out of scope (this row asserts the negative)

No command execution, process spawn, byte capture, or byte hashing; no timing; no persistence; no rendering
into `audit.json` or any artifact; no provenance/attestation assembly; no severity/enforcement/freshness/ship
verdict; no CLI (FR-008, FR-010). The sole outputs are the `CommandRecord` value and its `CommandIdentity`.
