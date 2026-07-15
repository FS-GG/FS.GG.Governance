// The EDGE of repository-snapshot sensing (F016) — the ONLY impure code in the feature.
// Visibility lives in Interpreter.fsi (Principle II). It runs the closed, READ-ONLY `GitCommand`
// set against an injected `GitPort` (the real one drives `git` via BCL System.Diagnostics.Process)
// and reads optional CI context through an injected `CiPort` (BCL System.Environment — NEVER a
// hosting-provider API, research D9), gathers everything into a `RawSensing`, and applies the pure
// `Snapshot.assemble`. It NEVER throws out of itself (FR-008): a thrown Process error or a nonzero
// git exit becomes the matching `Error` and then a `SensingDiagnostic`. READ-ONLY is guaranteed by
// CONSTRUCTION (FR-006): the `GitCommand` DU contains only read subcommands.

namespace FS.GG.Governance.Snapshot

open FS.GG.Governance.Snapshot.Model

type GitCommand =
    | RepoCheck
    | RevParse of GitRef
    | MergeBase of a: CommitId * b: CommitId
    | DiffNameStatus of baseId: CommitId * headId: CommitId
    | StatusPorcelain
    | CurrentBranch

    member this.Token : string =
        match this with
        | RepoCheck -> "repo-check"
        | RevParse _ -> "rev-parse"
        | MergeBase _ -> "merge-base"
        | DiffNameStatus _ -> "diff-name-status"
        | StatusPorcelain -> "status-porcelain"
        | CurrentBranch -> "current-branch"

type GitPort = GitCommand -> Result<string, string>

type CiPort = unit -> CiContext option

type Ports =
    { Git: GitPort
      Ci: CiPort }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    // A process-start failure (e.g. `git` not on PATH) is tagged with this marker so the
    // interpreter can map it to `GitUnavailable` rather than `NotARepository` (FR-008). It is an
    // edge-internal detail and never reaches the snapshot facts.
    let private gitUnavailableMarker = "git-unavailable"

    // Safety ceilings so the sensor NEVER hangs (the file header's contract). A read-only git command
    // returns quickly; these are generous backstops, not the normal path. `gitProcessTimeoutMs` bounds
    // the wait for git itself to exit; `gitDrainTimeoutMs` bounds the post-exit wait for the two async
    // reads to finish, so a pipe-inheriting background child that keeps a stream open can never wedge us
    // (M-CORE-2, mirrors the GateExecution port).
    let private gitProcessTimeoutMs = 120_000
    let private gitDrainTimeoutMs = 5_000

    /// The exact READ-ONLY argv (after `git`) for each command (git-sensing.md §1). No mutating
    /// subcommand is representable — read-only by construction (FR-006).
    let private argv (cmd: GitCommand) : string list =
        match cmd with
        | RepoCheck -> [ "rev-parse"; "--is-inside-work-tree" ]
        | RevParse(GitRef r) -> [ "rev-parse"; "--verify"; r + "^{commit}" ]
        | MergeBase(CommitId a, CommitId b) -> [ "merge-base"; a; b ]
        | DiffNameStatus(CommitId b, CommitId h) -> [ "diff"; "--name-status"; "-z"; "-M"; b; h ]
        | StatusPorcelain -> [ "status"; "--porcelain=v1"; "-z" ]
        | CurrentBranch -> [ "rev-parse"; "--abbrev-ref"; "HEAD" ]

    // The ONLY place the feature starts a process. Read-only git only; never throws — a thrown
    // exception (git missing, process failure) is caught and reified as an `Error` (FR-008).
    let private runGit (repoDir: string) (cmd: GitCommand) : Result<string, string> =
        try
            let psi = System.Diagnostics.ProcessStartInfo "git"

            for a in argv cmd do
                psi.ArgumentList.Add a

            psi.WorkingDirectory <- repoDir
            psi.RedirectStandardOutput <- true
            psi.RedirectStandardError <- true
            psi.UseShellExecute <- false
            // Read-only by construction AND in practice (FR-006): forbid the optional index-refresh
            // writes git would otherwise make on `status`/`diff`, so sensing never mutates `.git`.
            psi.Environment.["GIT_OPTIONAL_LOCKS"] <- "0"

            match System.Diagnostics.Process.Start psi with
            | null -> Error(gitUnavailableMarker + ": git process did not start")
            | proc ->
                use proc = proc
                // Drain BOTH streams CONCURRENTLY (each on its own async read) so a large write to one
                // pipe can never block git on the other — the classic pipe-buffer deadlock this file's
                // header promises we never hit (e.g. autocrlf warnings flooding stderr past 64 KB).
                let outTask = proc.StandardOutput.ReadToEndAsync()
                let errTask = proc.StandardError.ReadToEndAsync()

                if not (proc.WaitForExit gitProcessTimeoutMs) then
                    (try proc.Kill true with _ -> ())
                    Error(sprintf "%s: git '%s' exceeded %d ms and was terminated" gitUnavailableMarker cmd.Token gitProcessTimeoutMs)
                else
                    // git exited; the reads should reach EOF momentarily. Wait BOUNDED so an orphaned
                    // pipe-holder cannot hang us, then read a task's result only if it actually completed.
                    (try
                        System.Threading.Tasks.Task.WaitAll(
                            [| (outTask :> System.Threading.Tasks.Task); (errTask :> System.Threading.Tasks.Task) |],
                            gitDrainTimeoutMs)
                        |> ignore
                     with _ ->
                         ())

                    let resultOf (t: System.Threading.Tasks.Task<string>) =
                        if t.IsCompletedSuccessfully then t.Result else ""

                    let stdout = resultOf outTask
                    let stderr = resultOf errTask

                    if proc.ExitCode = 0 then
                        // git exited cleanly — but only trust stdout if its read actually reached EOF
                        // within the drain window. If the read did NOT complete, `resultOf` handed back
                        // `""`, and an empty `status --porcelain` parses to a CLEAN working tree: lost
                        // output would masquerade as a positive "nothing changed" fact and a dirty tree
                        // could be reported clean (ADPT-3). Reify the incomplete read as an Error so a lost
                        // read becomes an UnreadableWorkingTree diagnostic (for status) via `assemble`,
                        // never a fabricated clean — mirroring the nonzero-exit failure path.
                        if outTask.IsCompletedSuccessfully then
                            Ok stdout
                        else
                            Error(sprintf "git '%s' exited but its output could not be read within %d ms" cmd.Token gitDrainTimeoutMs)
                    else
                        let reason = stderr.Trim()
                        Error(if reason <> "" then reason else sprintf "git exited with code %d" proc.ExitCode)
        with ex ->
            Error(gitUnavailableMarker + ": " + ex.Message)

    // Read optional runner-supplied CI context from the ENVIRONMENT only (research D9, FR-005).
    // Returns None when no recognized signal is present (never fabricated); never network I/O.
    let private splitCsv (s: string) : string list =
        s.Split(',')
        |> Array.map (fun x -> x.Trim())
        |> Array.filter (fun x -> x <> "")
        |> List.ofArray

    let private ciPort () : CiContext option =
        let env name =
            System.Environment.GetEnvironmentVariable(name: string) |> Option.ofObj

        let labels = env "FSGG_PR_LABELS" |> Option.map splitCsv |> Option.defaultValue []
        let checks = env "FSGG_REQUIRED_STATUS_CHECKS" |> Option.map splitCsv |> Option.defaultValue []
        let ciFlag = env "CI"

        let isTruthy (v: string) =
            let v = v.Trim().ToLowerInvariant()
            v = "1" || v = "true" || v = "yes"

        match ciFlag, labels, checks with
        | None, [], [] -> None
        | _ ->
            let environment =
                match ciFlag with
                | Some v when isTruthy v -> CiEnvironment.Ci
                | Some _ -> CiEnvironment.LocalShell
                | None -> CiEnvironment.Unknown

            // Ordered deterministically here so the edge never depends on environment ordering.
            Some
                { Environment = environment
                  PrLabels = labels |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))
                  RequiredStatusChecks = checks |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b)) }

    let realPorts (repoDir: string) : Ports =
        { Git = runGit repoDir
          Ci = ciPort }

    // A short, stable hash of normalized output — the provenance digest (FR-010). The raw stdout,
    // stderr, timing, and pid are NEVER placed in the snapshot facts; an Error digests a constant so
    // no host path leaks and the value stays deterministic.
    let private digestOf (text: string) : string =
        let bytes = System.Text.Encoding.UTF8.GetBytes text
        let hash = System.Security.Cryptography.SHA256.HashData bytes
        System.Convert.ToHexString(hash).ToLowerInvariant().Substring(0, 16)

    let senseSnapshot (ports: Ports) (options: SnapshotOptions) : RepoSnapshot =
        let plan = Snapshot.planResolution options
        // mutable: a local accumulator of per-command provenance, in run order; assemble sorts it
        // deterministically (Principle III disclosure — no shared mutable state escapes).
        let digests = System.Collections.Generic.List<CommandRunDigest>()

        let run (cmd: GitCommand) : Result<string, string> =
            let r = ports.Git cmd
            let digestText = match r with Ok t -> t | Error _ -> "error"
            digests.Add { Command = cmd.Token; Digest = digestOf digestText }
            r

        let ci = ports.Ci()
        let repoCheck = run RepoCheck

        // Every branch now routes through `Snapshot.assemble`, which owns digest sorting (111/B9) — no local
        // sort needed here.
        match repoCheck with
        | Error msg when msg.StartsWith gitUnavailableMarker ->
            // git binary unavailable — route through `assemble` (RepoState = GitAbsent) so the empty-snapshot
            // shape + digest sort are owned by the one pure assembler, exactly like the not-a-work-tree path
            // below (111/B9); never a throw. The skipped fields are ignored by assemble in this branch.
            let skipped = Error "skipped: git is not available"

            Snapshot.assemble
                { RepoState = Snapshot.GitAbsent
                  BaseResolved = skipped
                  HeadResolved = skipped
                  MergeBaseResolved = skipped
                  DiffRaw = skipped
                  StatusRaw = skipped
                  BranchRaw = skipped
                  RawCi = ci
                  Digests = List.ofSeq digests
                  Plan = plan }
        | _ ->
            let repoOk =
                match repoCheck with
                | Ok t -> t.Trim() = "true"
                | Error _ -> false

            if not repoOk then
                // Not a work tree: assemble maps NotAWorkTree → NotARepository; the unrun commands are
                // marked skipped (assemble ignores them in this branch).
                let skipped = Error "skipped: target is not a git repository"

                Snapshot.assemble
                    { RepoState = Snapshot.NotAWorkTree
                      BaseResolved = skipped
                      HeadResolved = skipped
                      MergeBaseResolved = skipped
                      DiffRaw = skipped
                      StatusRaw = skipped
                      BranchRaw = skipped
                      RawCi = ci
                      Digests = List.ofSeq digests
                      Plan = plan }
            else
                let asCommit (r: Result<string, string>) =
                    r |> Result.map (fun s -> CommitId(s.Trim()))

                let baseRef = plan.BaseRef |> Option.defaultValue (GitRef "HEAD")
                let headRef = plan.HeadRef |> Option.defaultValue (GitRef "HEAD")

                let baseResolved = run (RevParse baseRef) |> asCommit
                let headResolved = run (RevParse headRef) |> asCommit

                // Merge base + committed diff only when both endpoints resolved; otherwise the
                // range diagnostics already explain the failure (assemble skips diff when Range=None).
                let mergeBaseResolved, diffRaw =
                    match baseResolved, headResolved with
                    | Ok b, Ok h ->
                        match run (MergeBase(b, h)) |> asCommit with
                        | Ok mb -> Ok mb, run (DiffNameStatus(mb, h))
                        | Error e -> Error e, Error "skipped: unresolved merge base"
                    | _ -> Error "skipped: unresolved base/head", Error "skipped: unresolved range"

                let statusRaw = run StatusPorcelain
                let branchRaw = run CurrentBranch

                Snapshot.assemble
                    { RepoState = Snapshot.WorkTree
                      BaseResolved = baseResolved
                      HeadResolved = headResolved
                      MergeBaseResolved = mergeBaseResolved
                      DiffRaw = diffRaw
                      StatusRaw = statusRaw
                      BranchRaw = branchRaw
                      RawCi = ci
                      Digests = List.ofSeq digests
                      Plan = plan }
