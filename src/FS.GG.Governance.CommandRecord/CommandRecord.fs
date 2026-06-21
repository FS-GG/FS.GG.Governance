// Command-record operations for the command-record core (F032). The public surface is fixed by
// CommandRecord.fsi (Principle II); no top-level binding here carries an access modifier. `build` is pure
// record construction; `canonicalId` renders ONLY `record.Reproducible` to a byte-stable identity in the
// F029 tagged, length-prefixed, injective discipline (contracts/command-record-identity-format.md): the
// duration is never read (D2), arguments are order-significant, and each env-delta class is a SET. All three
// operations are pure, total, deterministic: no clock, filesystem, git, environment, or network; no process
// spawn; no hashing. BCL string building only (FR-010).

namespace FS.GG.Governance.CommandRecord

open System.Text
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CommandRecord =

    // ── The single pure, total assembly (US1) ──

    let build
        (executable: Executable)
        (arguments: Argument list)
        (workingDirectory: WorkingDirectory)
        (environment: EnvironmentDelta)
        (timeout: TimeoutLimit)
        (exitCode: ExitCode)
        (stdoutDigest: OutputDigest)
        (stderrDigest: OutputDigest)
        (capturedOutput: CapturedOutput)
        (duration: SensedDuration)
        : CommandRecord =
        // Verbatim carriage — no normalization, no reordering (canonicalization is `canonicalId`'s job).
        // The sensed duration is held structurally apart in `Duration`, never inside `Reproducible` (D2).
        { Reproducible =
            { Executable = executable
              Arguments = arguments
              WorkingDirectory = workingDirectory
              Environment = environment
              Timeout = timeout
              ExitCode = exitCode
              StdoutDigest = stdoutDigest
              StderrDigest = stderrDigest
              CapturedOutput = capturedOutput }
          Duration = duration }

    // ── Segment encoders (internal; hidden by CommandRecord.fsi) — the F029 discipline (D6) ──

    // UTF-8 byte length of a value — the length prefix that makes the encoding injective: a reader knows
    // exactly how many bytes a value occupies, so no value can masquerade as another field (FR-006).
    let byteLen (s: string) : int = Encoding.UTF8.GetByteCount s

    // A required string value: presence digit '1', then "<byteLen>:<value>" (e.g. "gcc" -> "exe=13:gcc").
    let req (tag: string) (s: string) : string = sprintf "%s=1%d:%s" tag (byteLen s) s

    // The captured-output segment: presence '0' (NoCapturedOutput, no payload) or the required encoding
    // (CapturedAt — presence '1', then "<byteLen>:<path>"). So `cap=0` (absent) is distinct from `cap=10:`
    // (present, empty path) and from `cap=11:x` (present "x") — the F029 presence-digit guarantee (D5).
    let capSegment (c: CapturedOutput) : string =
        match c with
        | NoCapturedOutput -> "cap=0"
        | CapturedAt (CapturedOutputPath p) -> req "cap" p

    // The arguments segment: "args=<count>;<len1>:<a1>;<len2>:<a2>;…" rendered in given ORDER, NOT sorted or
    // deduplicated (argument order is significant; a repeated argument is a real repeat — D6). Empty list ⇒
    // "args=0;".
    let argsSegment (arguments: Argument list) : string =
        let body =
            arguments
            |> List.map (fun (Argument a) -> sprintf "%d:%s" (byteLen a) a)
            |> String.concat ";"

        sprintf "args=%d;%s" (List.length arguments) body

    // The canonical per-entry string of each env-delta class (themselves internally length-prefixed so the
    // name/value boundaries are injective — contracts/command-record-identity-format.md).
    let addedEntry (e: AddedVar) : string =
        let (EnvVarName n) = e.Name
        let (EnvVarValue v) = e.Value
        sprintf "n:%d:%s|v:%d:%s" (byteLen n) n (byteLen v) v

    let changedEntry (e: ChangedVar) : string =
        let (EnvVarName n) = e.Name
        let (EnvVarValue o) = e.Old
        let (EnvVarValue w) = e.New
        sprintf "n:%d:%s|o:%d:%s|w:%d:%s" (byteLen n) n (byteLen o) o (byteLen w) w

    let removedEntry (e: RemovedVar) : string =
        let (EnvVarName n) = e.Name
        let (EnvVarValue o) = e.Old
        sprintf "n:%d:%s|o:%d:%s" (byteLen n) n (byteLen o) o

    // An environment-delta class segment: "<tag>=<count>;<e1>;<e2>;…" over the entries' canonical strings,
    // DEDUPLICATED and ordinal-SORTED (the F029 set discipline) so supply order and duplicates never affect
    // the identity (FR-007). Distinct tags (env+/env~/env-) + a changed entry carrying both `o` and `w`
    // ensure a changed variable can never encode the same bytes as an Added + Removed pair (FR-002). Empty
    // class ⇒ "<tag>=0;".
    let envClassSegment (tag: string) (entries: string list) : string =
        let canon =
            entries
            |> List.distinct
            |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))

        sprintf "%s=%d;%s" tag (List.length canon) (String.concat ";" canon)

    // ── The pure, total canonical identity over the reproducible facts (US2) ──

    let canonicalId (record: CommandRecord) : CommandIdentity =
        // Computed ONLY over `record.Reproducible`; `record.Duration` is never read (D2, FR-005).
        let r = record.Reproducible
        let (Executable exe) = r.Executable
        let (WorkingDirectory cwd) = r.WorkingDirectory
        let (TimeoutLimit timeoutSeconds) = r.Timeout
        let (ExitCode exitCode) = r.ExitCode
        let (OutputDigest stdout) = r.StdoutDigest
        let (OutputDigest stderr) = r.StderrDigest

        // Fixed field order, joined by '\n', no trailing newline
        // (contracts/command-record-identity-format.md).
        [ req "exe" exe
          argsSegment r.Arguments
          req "cwd" cwd
          envClassSegment "env+" (r.Environment.Added |> List.map addedEntry)
          envClassSegment "env~" (r.Environment.Changed |> List.map changedEntry)
          envClassSegment "env-" (r.Environment.Removed |> List.map removedEntry)
          req "to" (string timeoutSeconds)
          req "exit" (string exitCode)
          req "out" stdout
          req "err" stderr
          capSegment r.CapturedOutput ]
        |> String.concat "\n"
        |> CommandIdentity

    let identityValue (identity: CommandIdentity) : string =
        let (CommandIdentity s) = identity
        s
