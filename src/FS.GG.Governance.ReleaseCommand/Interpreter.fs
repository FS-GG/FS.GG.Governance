// The EDGE interpreter of the `fsgg release` host command (F055, grown by 065 F26 host wiring). Visibility
// lives in Interpreter.fsi (Principle II). It executes the `Loop.Effect`s the pure `update` requests
// against INJECTED, FAKEABLE ports and feeds each result back as a `Loop.Msg`. 065 adds the F51 execution
// port, a pack-output reader, and the normalized head/environment/builder senses — every new I/O at the
// edge, none in a pure core (FR-010). TOTAL and SAFE: every port `Error` and thrown write exception is
// caught and reified; the interpreter NEVER throws and (via temp+rename) NEVER leaves a partial artifact.

namespace FS.GG.Governance.ReleaseCommand

open System
open System.IO
open System.Security.Cryptography
open System.Text.RegularExpressions
open FS.GG.Governance.Config                       // Loader
open FS.GG.Governance.Config.Model                  // SurfaceId, EnvironmentClass, Local, Ci, LocalOrCi
open FS.GG.Governance.FreshnessKey.Model            // Revision, ArtifactHash
open FS.GG.Governance.Provenance.Model              // BuilderIdentity
open FS.GG.Governance.GateExecution.Model           // ExecutionPort
open FS.GG.Governance.CommandRecord.Model            // ExitCode
open FS.GG.Governance.CommandKind.Model             // KindedCommandRun, CommandKind.Pack
open FS.GG.Governance.PackEvidence.Model            // PackOutcome, PackArtifact, NoArtifactReason
open FS.GG.Governance.Snapshot.Model                // CommitId, SnapshotOptions, DiffRange
open FS.GG.Governance.ReleaseFactsSensing.Model      // SourceLayout, ReleaseExpectations, SensedRelease
open FS.GG.Governance.ReleaseDeclaration            // 065: the shared Declaration leaf (was row-local)
open FS.GG.Governance.CommandHost           // 049: shared host-loop combinators (guard/drive)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type ArtifactWriter = string -> string -> Result<unit, string>

    type OutputSink = string -> unit

    type Ports =
        { Files: Loader.FileReader
          Sense: SourceLayout -> ReleaseExpectations -> SensedRelease
          Execute: ExecutionPort
          PackRead: SurfaceId -> KindedCommandRun -> PackOutcome
          SenseHead: unit -> Revision
          SenseEnvironment: unit -> EnvironmentClass
          SenseBuilder: unit -> BuilderIdentity
          Write: ArtifactWriter
          Out: OutputSink }

    // Run a port call, converting BOTH an `Error` and a thrown exception into `Error`.
    // The real persistence port: create parent dirs, write to a unique temp sibling, then atomically rename.
    let writeAtomic (path: string) (content: string) : Result<unit, string> =
        try
            match Path.GetDirectoryName path with
            | null
            | "" -> ()
            | dir -> Directory.CreateDirectory dir |> ignore

            let tmp = path + ".tmp-" + Guid.NewGuid().ToString("N")
            File.WriteAllText(tmp, content)
            File.Move(tmp, path, true)
            Ok()
        with e ->
            Error e.Message

    let declError (reason: string) : Result<Declaration.ReleaseDeclaration, Declaration.DeclError> =
        Error { Reason = reason }

    // ── 065 real edge senses (normalized — no username/host/clock leaks into the attestation) ──

    let senseEnvironmentReal () : EnvironmentClass =
        // Qualify the cases: `Ci` also names a `Snapshot.Model.CiEnvironment` case.
        match Environment.GetEnvironmentVariable "CI" with
        | null
        | "" -> EnvironmentClass.Local
        | _ -> EnvironmentClass.Ci

    let senseBuilderReal () : BuilderIdentity = BuilderIdentity "fsgg"

    // The F016 head revision (the release attests a product state, not a diff range). An absent/unavailable
    // range degrades to a deterministic empty sentinel — never a throw, never a fabricated commit.
    let senseHeadReal (repo: string) () : Revision =
        try
            let snap =
                FS.GG.Governance.Snapshot.Interpreter.senseSnapshot
                    (FS.GG.Governance.Snapshot.Interpreter.realPorts repo)
                    { Since = None; Base = None; Head = None }

            match snap.Range with
            | Some r ->
                let (CommitId c) = r.Head
                Revision c
            | None -> Revision ""
        with _ ->
            Revision ""

    // Parse the package version out of a `Name.1.2.3[-pre].nupkg` filename (deterministic, normalized).
    let versionFromNupkg (fileName: string) : string =
        let stem =
            if fileName.EndsWith ".nupkg" then
                fileName.Substring(0, fileName.Length - 6)
            else
                fileName

        let m = Regex.Match(stem, @"\d+\.\d+(\.\d+)?([-+].*)?$")
        if m.Success then m.Value else stem

    // The real pack-output reader: a non-zero exit ⇒ `PackFailed` (sentinel recorded); otherwise locate the
    // produced `.nupkg` under the constitution's pack-output dir, read its version + compute its
    // `ArtifactHash`. An unreadable / absent artifact ⇒ `PackedNoArtifact` (input signal, never a throw).
    // The artifact PATH is normalized to the bare filename so the attestation/release bytes are
    // machine-independent (FR-011).
    let packReadReal (surface: SurfaceId) (run: KindedCommandRun) : PackOutcome =
        let (ExitCode code) = run.Record.Reproducible.ExitCode

        if code <> 0 then
            PackFailed(surface, code, run)
        else
            try
                let dir =
                    Path.Combine(
                        Environment.GetFolderPath Environment.SpecialFolder.UserProfile,
                        ".local",
                        "share",
                        "nuget-local"
                    )

                if not (Directory.Exists dir) then
                    PackedNoArtifact(surface, NoArtifactEmitted, run)
                else
                    match
                        Directory.GetFiles(dir, "*.nupkg")
                        |> Array.sortByDescending File.GetLastWriteTimeUtc
                        |> Array.tryHead
                    with
                    | None -> PackedNoArtifact(surface, NoArtifactEmitted, run)
                    | Some path ->
                        use sha = SHA256.Create()
                        let digest = File.ReadAllBytes path |> sha.ComputeHash |> Convert.ToHexString

                        let fileName =
                            match Path.GetFileName path with
                            | null -> path
                            | f -> f

                        Packed(
                            { Surface = surface
                              ArtifactPath = fileName
                              PackedVersion = versionFromNupkg fileName
                              Digest = ArtifactHash(digest.ToLowerInvariant()) },
                            run
                        )
            with e ->
                PackedNoArtifact(surface, ArtifactUnreadable e.Message, run)

    let step (ports: Ports) (effect: Loop.Effect) : Loop.Msg =
        match effect with
        | Loop.LoadDeclaration _ ->
            let loaded =
                try
                    match ports.Files "release.yml" with
                    | Ok(Some content) ->
                        let lines = content.Replace("\r\n", "\n").Split('\n') |> List.ofArray
                        Declaration.parse lines
                    | Ok None -> declError ".fsgg/release.yml not found"
                    | Error reason -> declError ("unreadable: " + reason)
                with e ->
                    declError ("read failed: " + e.Message)

            Loop.DeclarationLoaded loaded

        | Loop.SenseRelease(layout, expectations) -> Loop.Sensed(ports.Sense layout expectations)

        | Loop.PackProjects requests ->
            // Run each declared pack command ONCE through the F51 port, wrap its record as a `Pack` run, and
            // classify the produced artifact. The run is carried in EVERY outcome (never dropped, FR-001);
            // request order is preserved.
            let outcomes =
                requests
                |> List.map (fun (surface, command) ->
                    let record = FS.GG.Governance.GateExecution.Interpreter.senseExecution ports.Execute command
                    let run = { Kind = Pack; Record = record }
                    ports.PackRead surface run)

            Loop.PacksRun outcomes

        | Loop.SenseProvenance ->
            Loop.ProvenanceSensed(ports.SenseHead(), ports.SenseEnvironment(), ports.SenseBuilder())

        | Loop.WriteArtifact(kind, path, content) -> Loop.Wrote(kind, CommandHost.guard (fun () -> ports.Write path content))

        | Loop.EmitSummary text ->
            ports.Out text
            Loop.Emitted

    let realPorts (repo: string) : Ports =
        { Files = Loader.fileSystemReader repo
          Sense =
            fun layout expectations ->
                FS.GG.Governance.ReleaseFactsSensing.Interpreter.senseRelease
                    (FS.GG.Governance.ReleaseFactsSensing.Interpreter.realPort repo layout)
                    expectations
          Execute = FS.GG.Governance.GateExecution.Interpreter.realPort
          PackRead = packReadReal
          SenseHead = senseHeadReal repo
          SenseEnvironment = senseEnvironmentReal
          SenseBuilder = senseBuilderReal
          Write = writeAtomic
          Out = fun text -> Console.Out.WriteLine text }

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request

        CommandHost.drive (fun (m: Loop.Model) -> m.Phase = Loop.Done) (step ports) Loop.update m0 eff0
