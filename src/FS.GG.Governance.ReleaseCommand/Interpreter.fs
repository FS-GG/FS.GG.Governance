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
open FS.GG.Governance.ReleaseRules                  // SemVer (single shared comparator, M-ADPT-1/M-CLI-4)
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

    let declError (reason: string) : Result<Declaration.ReleaseDeclaration, Declaration.DeclError> =
        Error { Reason = reason }

    // ── 065 real edge senses (normalized — no username/host/clock leaks into the attestation) ──

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

    // M-CLI-4: choose THIS surface's nupkg out of a shared-feed listing. Keep only exact
    // `<packageId>.<version>.nupkg` entries — an exact-id match rejects a sibling like
    // `<packageId>.Extras.1.2.3.nupkg` that a `<packageId>.*` glob would otherwise catch — then take the
    // HIGHEST semantic version via the single shared `SemVer` comparator. Pure and deterministic (FR-011):
    // the selection never consults wall-clock mtime. Returns the chosen file NAME (the caller resolves it
    // under the feed dir).
    let chooseSurfaceArtifact (packageId: string) (fileNames: string list) : string option =
        fileNames
        |> List.filter (fun name -> name = packageId + "." + versionFromNupkg name + ".nupkg")
        |> List.sortWith (fun a b -> SemVer.compareVersions (versionFromNupkg b) (versionFromNupkg a))
        |> List.tryHead

    // The real pack-output reader: a non-zero exit ⇒ `PackFailed` (sentinel recorded); otherwise locate the
    // produced `.nupkg` under the constitution's pack-output dir, read its version + compute its
    // `ArtifactHash`. An unreadable / absent artifact ⇒ `PackedNoArtifact` (input signal, never a throw).
    // The artifact PATH is normalized to the bare filename so the attestation/release bytes are
    // machine-independent (FR-011).
    //
    // M-CLI-4: the shared `~/.local/share/nuget-local` feed accumulates every package's nupkgs, so the reader
    // MUST isolate THIS surface's artifact — `SurfaceId` doubles as the NuGet PackageId (a nupkg is named
    // `<PackageId>.<version>.nupkg`). We name-filter by the package id (exact, not a sibling-prefix match) and
    // pick the HIGHEST semantic version via the single shared `SemVer` comparator — a deterministic selection
    // (FR-011), never wall-clock mtime. In the normal forward-release path this is the just-packed artifact;
    // fully disambiguating a same-package downgrade against a stale higher version in the shared feed would
    // require a before/after listing diff around the pack run (out of scope here).
    let packReadReal (surface: SurfaceId) (run: KindedCommandRun) : PackOutcome =
        let (ExitCode code) = run.Record.Reproducible.ExitCode
        let (SurfaceId packageId) = surface

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
                    let names =
                        Directory.GetFiles(dir, packageId + ".*.nupkg")
                        |> Array.choose (fun p ->
                            match Path.GetFileName p with
                            | null -> None
                            | name -> Some name)
                        |> List.ofArray

                    match chooseSurfaceArtifact packageId names with
                    | None -> PackedNoArtifact(surface, NoArtifactEmitted, run)
                    | Some fileName ->
                        use sha = SHA256.Create()

                        let digest =
                            Path.Combine(dir, fileName) |> File.ReadAllBytes |> sha.ComputeHash |> Convert.ToHexString

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
          SenseEnvironment = CommandHost.senseEnvironmentReal
          SenseBuilder = CommandHost.senseBuilderReal
          Write = CommandHost.writeAtomic
          Out = fun text -> Console.Out.WriteLine text }

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request

        CommandHost.drive (fun (m: Loop.Model) -> m.Phase = Loop.Done) (step ports) Loop.update m0 eff0
