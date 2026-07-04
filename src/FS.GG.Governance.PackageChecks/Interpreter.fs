// The EDGE of the package/API check (F24, P1) — the ONLY impure code in the domain (FR-007). Visibility
// lives in Interpreter.fsi (Constitution Principle II); no top-level access modifiers here. `realPort` reads
// only LOCAL files via BCL `System.IO` and runs transcripts through the injected `ExecutionPort`; it starts
// no other process, opens no socket, references no registry. It NEVER throws out of itself: a thrown
// exception or absent source becomes a `*Unreadable`/`*Unlocatable` input fact (FR-012). The on-disk FORMAT
// knowledge (baseline location, transcript layout, token extraction) lives here in the swappable port — the
// pure pack invents no schema.

namespace FS.GG.Governance.PackageChecks

open System.IO
open System.Text
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.PackageChecks.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    // BCL Path.GetDirectoryName / GetFileNameWithoutExtension are nullable; coalesce to "" (Nullable=enable).
    // Hidden by ABSENCE from Interpreter.fsi (Constitution II — no access modifiers here).
    let dirOrEmpty (p: string) : string =
        match Path.GetDirectoryName p with
        | null -> ""
        | d -> d

    let fileStem (p: string) : string =
        match Path.GetFileNameWithoutExtension p with
        | null -> ""
        | s -> s

    type PackagePort =
        { RegenerateSurface: GovernedPath -> Result<SurfaceTokens, string>
          ReadBaseline: GovernedPath -> Result<SurfaceTokens option, string>
          WriteBaseline: GovernedPath -> SurfaceTokens -> Result<unit, string>
          ListTranscripts: GovernedPath -> Result<GovernedPath list, string>
          RunTranscript: GovernedPath -> Result<TranscriptOutcome, string> }

    // ── Local-file helpers (the ONLY filesystem touch) ──

    // Normalize an `.fsi` (or any text surface) into a sorted, distinct, whitespace-collapsed, comment-free
    // token set (D5: a TOKEN diff, not a text diff — whitespace/order/formatting never report as drift).
    let extractTokens (text: string) : SurfaceTokens =
        text.Split([| '\n'; '\r' |])
        |> Array.map (fun line ->
            let noComment =
                let i = line.IndexOf "//"
                if i >= 0 then line.Substring(0, i) else line

            noComment.Split([| ' '; '\t' |], System.StringSplitOptions.RemoveEmptyEntries)
            |> String.concat " ")
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (fun s -> s <> "")
        |> Array.distinct
        |> Array.sort
        |> List.ofArray
        |> SurfaceTokens

    let baselinePathOf (repo: string) (GovernedPath rel) : string =
        Path.Combine(repo, rel + ".baseline")

    let regenerateSurface (repo: string) (path: GovernedPath) : Result<SurfaceTokens, string> =
        let (GovernedPath rel) = path
        let full = Path.Combine(repo, rel)

        if not (File.Exists full) then
            Error(sprintf "surface source not found: %s" rel)
        else
            try
                Ok(extractTokens (File.ReadAllText full))
            with ex ->
                Error(sprintf "surface source unreadable: %s: %s" rel ex.Message)

    let readBaseline (repo: string) (path: GovernedPath) : Result<SurfaceTokens option, string> =
        let full = baselinePathOf repo path

        if not (File.Exists full) then
            Ok None
        else
            try
                let tokens =
                    File.ReadAllLines full
                    |> Array.map (fun s -> s.Trim())
                    |> Array.filter (fun s -> s <> "")
                    |> Array.distinct
                    |> Array.sort
                    |> List.ofArray

                Ok(Some(SurfaceTokens tokens))
            with ex ->
                Error(sprintf "baseline unreadable: %s" ex.Message)

    let writeBaseline (repo: string) (path: GovernedPath) (SurfaceTokens tokens) : Result<unit, string> =
        let full = baselinePathOf repo path

        try
            Directory.CreateDirectory(dirOrEmpty full) |> ignore
            File.WriteAllText(full, String.concat "\n" tokens + "\n")
            Ok()
        with ex ->
            Error(sprintf "baseline could not be written: %s" ex.Message)

    // Transcripts: the `*.fsx` files under a sibling `transcripts/` directory of the surface path. Returned
    // repo-relative, forward-slash normalized, sorted (deterministic). Absent directory ⇒ Ok [] (no error).
    let listTranscripts (repo: string) (path: GovernedPath) : Result<GovernedPath list, string> =
        let (GovernedPath rel) = path
        let transcriptDir = Path.Combine(repo, dirOrEmpty rel, "transcripts")

        try
            if not (Directory.Exists transcriptDir) then
                Ok []
            else
                Directory.GetFiles(transcriptDir, "*.fsx")
                |> Array.map (fun full ->
                    let relFull = Path.GetRelativePath(repo, full)
                    normalizePath relFull)
                |> Array.sortBy (fun (GovernedPath p) -> p)
                |> List.ofArray
                |> Ok
        with ex ->
            Error(sprintf "transcripts unreadable: %s" ex.Message)

    let runTranscript (repo: string) (exec: ExecutionPort) (path: GovernedPath) : Result<TranscriptOutcome, string> =
        let (GovernedPath rel) = path
        let full = Path.Combine(repo, rel)

        if not (File.Exists full) then
            Error(sprintf "transcript not found: %s" rel)
        else
            try
                let command: GateCommand =
                    { Executable = Executable "dotnet"
                      Arguments = [ Argument "fsi"; Argument full ]
                      WorkingDirectory = WorkingDirectory repo
                      Environment = { Added = []; Changed = []; Removed = [] }
                      Timeout = TimeoutLimit 300
                      CapturedOutput = NoCapturedOutput }

                let outcome = exec command
                let (ExitCode code) = outcome.ExitCode

                if code <> 0 then
                    let stderr = (Encoding.UTF8.GetString outcome.Stderr).Trim()
                    Ok(TranscriptCompileFailed(if stderr = "" then sprintf "exit %d" code else stderr))
                else
                    let expectedPath = full + ".expected"

                    if File.Exists expectedPath then
                        let expected = (File.ReadAllText expectedPath).Trim()
                        let actual = (Encoding.UTF8.GetString outcome.Stdout).Trim()

                        if expected = actual then
                            Ok TranscriptPasses
                        else
                            Ok(TranscriptResultChanged(expected, actual))
                    else
                        Ok TranscriptPasses
            with ex ->
                Error(sprintf "transcript run threw: %s" ex.Message)

    let realPort (repo: string) (exec: ExecutionPort) : PackagePort =
        { RegenerateSurface = regenerateSurface repo
          ReadBaseline = readBaseline repo
          WriteBaseline = writeBaseline repo
          ListTranscripts = listTranscripts repo
          RunTranscript = runTranscript repo exec }

    // Compare the regenerated surface to the committed baseline as a normalized token diff (D5).
    let diffBaseline (SurfaceTokens committed) (SurfaceTokens generated) : FsiBaselineFact =
        let c = Set.ofList committed
        let g = Set.ofList generated
        let added = Set.difference g c |> Set.toList |> List.sort
        let removed = Set.difference c g |> Set.toList |> List.sort

        if List.isEmpty added && List.isEmpty removed then
            BaselineMatches
        else
            BaselineDrift(added, removed)

    let sensePackage (port: PackagePort) (request: SC.SurfaceCheckRequest) : PackageFacts =
        let source = request.Path

        // Reify any thrown exception as `Error` so the sensor never crashes (FR-012).
        let baseline =
            match SC.safe (fun () -> port.RegenerateSurface source) with
            | Error e -> BaselineUnreadable e
            | Ok generated ->
                match SC.safe (fun () -> port.ReadBaseline source) with
                | Error e -> BaselineUnreadable e
                | Ok None ->
                    match SC.safe (fun () -> port.WriteBaseline source generated) with
                    | Error e -> BaselineUnreadable e
                    | Ok() -> BaselineAbsent generated
                | Ok(Some committed) -> diffBaseline committed generated

        let transcripts =
            match SC.safe (fun () -> port.ListTranscripts source) with
            // FAIL-CLOSED (FR-012): an unreadable transcripts directory must NOT collapse to `[]` — that
            // reads as "no transcripts declared", so a package verify would pass exactly when the evidence
            // could not be gathered. Reify it as a synthetic Unlocatable transcript so the pure pack raises
            // a Blocking input-state finding (the same treatment as a per-transcript TranscriptUnlocatable /
            // a BaselineUnreadable), never a fabricated pass.
            | Error e ->
                [ { ExampleId = "<transcripts>"
                    Source = source
                    Outcome = TranscriptUnlocatable e } ]
            | Ok paths ->
                paths
                |> List.map (fun p ->
                    let (GovernedPath ps) = p
                    let exampleId = fileStem ps

                    let outcome =
                        match SC.safe (fun () -> port.RunTranscript p) with
                        | Ok o -> o
                        | Error e -> TranscriptUnlocatable e

                    { ExampleId = exampleId
                      Source = p
                      Outcome = outcome })

        { BaselineSource = source
          Baseline = baseline
          Transcripts = transcripts }
