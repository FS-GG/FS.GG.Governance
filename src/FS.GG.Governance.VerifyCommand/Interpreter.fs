// The EDGE interpreter of the `fsgg verify` host command (F056) — the ONLY impure code in the feature.
// Visibility lives in Interpreter.fsi (Principle II). It executes the `Loop.Effect`s the pure `update`
// requests against INJECTED, FAKEABLE ports and feeds each result back as a `Loop.Msg`. It REUSES the
// existing edges verbatim — `Config.Loader` for catalog reads (F014), `Snapshot.Interpreter` for git sensing
// (F016), `FreshnessSensing` for the F046 senses, `GateExecution` for the F051 run — adding only the
// persistence (`ArtifactWriter`) and stdout (`OutputSink`) ports. TOTAL and SAFE: every port `Error` and
// every thrown exception is caught and reified to the matching `Msg` — it NEVER throws and (via temp+rename)
// NEVER leaves a partial artifact, and a write failure is reified to a `Wrote(_, Error _)` (mapped by
// `update` to ToolError, never Blocked). The only difference from `ShipCommand` is the document written.

namespace FS.GG.Governance.VerifyCommand

open System
open System.IO
open FS.GG.Governance.Config              // Loader, Schema
open FS.GG.Governance.Config.Model         // GovernedPath, Validation, Invalid, Diagnostic, Locator, DiagnosticId
open FS.GG.Governance.Snapshot.Model        // SnapshotOptions, GitRef, RepoSnapshot, sensingDiagnosticIdToken
open FS.GG.Governance.FreshnessSensing       // FreshnessSensing.senseFreshness, loadStore, realSensor, realStoreReader (F046)
open FS.GG.Governance.HumanText              // RenderMode (selectMode), ReportView (F27 wiring 063)
open FS.GG.Governance.HumanRender            // Capability.senseCapability, RichRender.emitStdout (Spectre confined here)

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement // F070: CurrencyFinding (the port's result)
module CS = FS.GG.Governance.CurrencySensing.CurrencySensing         // F070: the shared edge sensing (senseRepo)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type ArtifactWriter = string -> string -> Result<unit, string>

    type OutputSink = string -> unit

    type Ports =
        { Files: Loader.FileReader
          Git: FS.GG.Governance.Snapshot.Ports
          Freshness: FreshnessSensing.FreshnessSensor
          Store: FreshnessSensing.StoreReader
          Write: ArtifactWriter
          Out: OutputSink
          Execute: FS.GG.Governance.GateExecution.Model.ExecutionPort
          SenseCapability: bool -> RenderMode.ColorCapability
          RenderReport: ReportView.ReportView -> unit
          SenseEnvironment: unit -> FS.GG.Governance.Config.Model.EnvironmentClass
          SenseBuilder: unit -> FS.GG.Governance.Provenance.Model.BuilderIdentity
          SenseRelease:
              FS.GG.Governance.ReleaseFactsSensing.Model.SourceLayout
                  -> FS.GG.Governance.ReleaseFactsSensing.Model.ReleaseExpectations
                  -> FS.GG.Governance.ReleaseFactsSensing.Model.SensedRelease
          // 067: the read-only product-surface sense + dispatch port (classified report ⇒ findings).
          SenseSurfaces:
              FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport
                  -> FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list
          // F070: the read-only generated-view currency sense (repo ⇒ stale-view findings). TOTAL & SAFE.
          SenseViewCurrency: string -> CE.CurrencyFinding list
          // F081: locate + read every readiness/<id>/governance-handoff.json under `repo` in stable <id> order.
          Handoffs: string -> FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list }

    // Run a port call, converting BOTH an `Error` and a thrown exception into `Error` so the interpreter
    // never throws out of itself.
    let guard (call: unit -> Result<'a, string>) : Result<'a, string> =
        try
            call ()
        with e ->
            Error e.Message

    // The real persistence port: create parent dirs, write to a unique temp sibling, then atomically rename
    // over the target — a failed write leaves NO partial/truncated file.
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

    let step (ports: Ports) (effect: Loop.Effect) : Loop.Msg =
        match effect with
        | Loop.SenseScope scope ->
            let options =
                match scope with
                | Loop.Since rev -> { Since = Some(GitRef rev); Base = None; Head = None }
                | Loop.ExplicitPaths _
                | Loop.DefaultRange -> { Since = None; Base = None; Head = None }

            let result =
                try
                    let snap = FS.GG.Governance.Snapshot.Interpreter.senseSnapshot ports.Git options
                    // senseSnapshot NEVER throws; a failure surfaces as a SensingDiagnostic. Any sensing
                    // diagnostic (not-a-repo, unknown-ref, git-unavailable) ⇒ InputUnavailable.
                    match snap.Diagnostics with
                    | [] -> Ok snap
                    | ds ->
                        ds
                        |> List.map (fun d -> sprintf "%s: %s" (sensingDiagnosticIdToken d.Id) d.Message)
                        |> String.concat "; "
                        |> Error
                with e ->
                    Error e.Message

            Loop.Sensed result

        | Loop.LoadCatalog _ ->
            // The reader is pre-bound to the repo's `.fsgg` in realPorts; mirror Loader.loadAndValidate
            // (read the four files through ports.Files, then the pure Schema.validate). readSource + validate
            // do not throw, but a misbehaving reader could — reify that to an Invalid catalog.
            let validation =
                try
                    Loader.readSource (GovernedPath ".") ports.Files |> Schema.validate
                with e ->
                    Invalid
                        [ { Id = MissingRequiredFile
                            File = Project
                            Locator = { Field = None; Id = None; Line = None }
                            Message = "catalog read failed: " + e.Message } ]

            Loop.Loaded validation

        | Loop.SenseFreshness(gates, baseHead) ->
            // F046: assemble SensedFacts at the shared sensing edge; an Error here DEGRADES in `update`.
            Loop.FreshnessSensed(FreshnessSensing.senseFreshness ports.Freshness gates baseHead)

        | Loop.LoadStore path ->
            // F046: read-only store load (absent ⇒ empty); a malformed store DEGRADES in `update`.
            Loop.StoreLoaded(FreshnessSensing.loadStore ports.Store path)

        | Loop.WriteArtifact(kind, path, content) -> Loop.Wrote(kind, guard (fun () -> ports.Write path content))

        // F048: reuse the existing atomic `writeAtomic` (temp + rename) for the store write — a failed write
        // leaves no partial file and is reified to the NON-FATAL `StorePersisted`.
        | Loop.PersistStore(path, content) -> Loop.StorePersisted(guard (fun () -> ports.Write path content))

        // F052: run each requested must-recompute command-gate ONCE through the injected F051 port, assembling
        // its F032 `CommandRecord` via the merged `senseExecution`. Records come back in request order, tagged
        // by GateId. The port is TOTAL & SAFE; `senseExecution` is pure given the port.
        | Loop.ExecuteGates requests ->
            Loop.GatesExecuted(
                requests
                |> List.map (fun (gateId, command) ->
                    gateId, FS.GG.Governance.GateExecution.Interpreter.senseExecution ports.Execute command)
            )

        // 067 (F24 verify-host wiring): sense + run the product-surface checks for the classified report. The
        // port is TOTAL & SAFE (it catches its own exceptions ⇒ no fabricated findings, no crash — FR-010);
        // an empty report ⇒ `[]` ⇒ byte-identical verify.json (FR-004).
        | Loop.SenseSurfaces report -> Loop.SurfacesSensed(ports.SenseSurfaces report)

        // F070 (stale-view blocking): sense generated-view currency at the edge. TOTAL & SAFE; `[]` ⇒
        // byte-identical verify.json (FR-004).
        | Loop.SenseViewCurrency repo -> Loop.ViewCurrencySensed(ports.SenseViewCurrency repo)

        | Loop.LoadHandoffs repo -> Loop.HandoffsLoaded(ports.Handoffs repo)

        // F25 wiring (064): the two NEW normalized provenance senses. Both are normalized (no username, host,
        // absolute path, or wall-clock) so `provenance.json` is byte-identical across machines/re-runs.
        | Loop.SenseProvenance -> Loop.ProvenanceSensed(ports.SenseEnvironment(), ports.SenseBuilder())

        // 065 (US3): load `.fsgg/release.yml` through the pre-bound `Files` reader and, IF it parses, sense the
        // F54 release facts so the advisory preview can assemble a ReleaseReport WITHOUT packing. An absent or
        // unparsable declaration ⇒ `None` ⇒ no preview ⇒ byte-identical verify.json. TOTAL & SAFE.
        | Loop.SenseReleasePreview _ ->
            let result =
                try
                    match ports.Files "release.yml" with
                    | Ok(Some content) ->
                        let lines = content.Replace("\r\n", "\n").Split('\n') |> List.ofArray

                        match FS.GG.Governance.ReleaseDeclaration.Declaration.parse lines with
                        | Ok decl -> Some(decl, ports.SenseRelease decl.Layout decl.Expectations)
                        | Error _ -> None
                    | _ -> None
                with _ ->
                    None

            Loop.ReleasePreviewSensed result

        // F27 wiring (063) US2: the render-mode dispatch lives HERE at the edge (FR-004). Json (human = None)
        // and the ANSI-free Plain path go via the existing `Out` sink (byte-stable, captured in tests); only the
        // interactive `Rich` path goes through `RenderReport` (Spectre, confined to HumanRender) followed by the
        // operational line. The mode is `selectMode false (senseCapability explicitPlain)`.
        | Loop.EmitSummary(text, human, explicitPlain) ->
            match human with
            | None -> ports.Out text
            | Some(view, operational) ->
                match RenderMode.selectMode false (ports.SenseCapability explicitPlain) with
                | RenderMode.Rich ->
                    ports.RenderReport view
                    if operational <> "" then ports.Out operational
                | RenderMode.Plain
                | RenderMode.Json -> ports.Out text

            Loop.Emitted

    // F25 wiring (064): the real, NORMALIZED provenance senses. Environment is classified from the presence of
    // a generic `CI` marker only (`Ci` vs `Local`) — never a hostname, username, or path. Builder is a fixed,
    // machine-independent tool identity so `provenance.json` is byte-identical across machines and re-runs.
    let senseEnvironmentReal () : FS.GG.Governance.Config.Model.EnvironmentClass =
        match Environment.GetEnvironmentVariable "CI" with
        | null
        | "" -> FS.GG.Governance.Config.Model.Local
        | _ -> FS.GG.Governance.Config.Model.Ci

    let senseBuilderReal () : FS.GG.Governance.Provenance.Model.BuilderIdentity =
        FS.GG.Governance.Provenance.Model.BuilderIdentity "fsgg"

    // 067 (F24 verify-host wiring): the design-catalog layout the design sensor reads (repo-relative JSON
    // catalogs). The established default mirrors the DesignChecks sensor tests; a per-repo override is a
    // documented later extension (no config field exists yet — the four catalogs are absent in most repos, so
    // the design sensor reports `catalog-unavailable` input facts rather than fabricating, FR-010/FR-012).
    let designCatalogLayout: string * string * string * string =
        "design/tokens.json", "design/captures.json", "design/controls.json", "design/contrast.json"

    // 067: the REAL read-only product-surface sense + dispatch over a repo working directory. Loads `TypedFacts`
    // (read-only) for the request build, senses each DECLARED domain through a READ-ONLY port, then runs the
    // pure `Composition.run` aggregator. Crucially side-effect-free at verify (FR-012): the **package** port
    // no-ops `WriteBaseline` (an absent baseline is REPORTED via `package.baseline-absent`, never written) and
    // lists NO transcripts (no FSI process is spawned). TOTAL & SAFE: an invalid/absent catalog or an
    // unexpected exception degrades to `[]` (no fabricated findings, no crash — FR-010, research D6). The four
    // domain sensors themselves already encode bounded, disclosed outcomes for missing/unreadable inputs.
    let senseSurfacesReal
        (repo: string)
        (exec: FS.GG.Governance.GateExecution.Model.ExecutionPort)
        (report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport)
        : FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list =
        try
            match Loader.readSource (GovernedPath ".") (Loader.fileSystemReader repo) |> Schema.validate with
            | Invalid _ -> []
            | Valid facts ->
                let requests =
                    FS.GG.Governance.SurfaceChecks.Dispatch.Composition.requestsOf facts report

                // The READ-ONLY package port (FR-012): regenerate/read are kept; the WRITE and the transcript
                // EXEC are removed (baseline establishment + transcript runs belong to route/ship).
                let pkgPort =
                    { FS.GG.Governance.PackageChecks.Interpreter.realPort repo exec with
                        WriteBaseline = (fun _ _ -> Ok())
                        ListTranscripts = (fun _ -> Ok []) }

                let docsPort = FS.GG.Governance.DocsChecks.Interpreter.realPort repo
                let skillPort = FS.GG.Governance.SkillChecks.Interpreter.realPort repo
                let designPort = FS.GG.Governance.DesignChecks.Interpreter.realPort repo designCatalogLayout

                let bundle =
                    requests
                    |> List.fold
                        (fun (b: FS.GG.Governance.SurfaceChecks.Dispatch.Composition.DomainFactBundle) req ->
                            match req.Domain with
                            | FS.GG.Governance.SurfaceChecks.Model.PackageDomain ->
                                { b with
                                    Package =
                                        Map.add
                                            req.Surface
                                            (FS.GG.Governance.PackageChecks.Interpreter.sensePackage pkgPort req)
                                            b.Package }
                            | FS.GG.Governance.SurfaceChecks.Model.DocsDomain ->
                                { b with
                                    Docs =
                                        Map.add
                                            req.Surface
                                            (FS.GG.Governance.DocsChecks.Interpreter.senseDocs docsPort req)
                                            b.Docs }
                            | FS.GG.Governance.SurfaceChecks.Model.SkillDomain ->
                                { b with
                                    Skill =
                                        Map.add
                                            req.Surface
                                            (FS.GG.Governance.SkillChecks.Interpreter.senseSkill skillPort req)
                                            b.Skill }
                            | FS.GG.Governance.SurfaceChecks.Model.DesignDomain ->
                                { b with
                                    Design =
                                        Map.add
                                            req.Surface
                                            (FS.GG.Governance.DesignChecks.Interpreter.senseDesign designPort req)
                                            b.Design })
                        FS.GG.Governance.SurfaceChecks.Dispatch.Composition.emptyBundle

                FS.GG.Governance.SurfaceChecks.Dispatch.Composition.run facts report bundle
        with _ ->
            []

    // F081: the real handoff-location port — locate every `readiness/<id>/governance-handoff.json` under
    // `repo` in stable `<id>` (ordinal) order and read its raw JSON. TOTAL & SAFE (any error / absent
    // `readiness/` ⇒ `[]`, never a throw).
    let realHandoffs (repo: string) : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list =
        try
            let readinessDir = Path.Combine(repo, "readiness")

            if not (Directory.Exists readinessDir) then
                []
            else
                Directory.GetDirectories readinessDir
                |> Array.sortWith (fun a b -> String.CompareOrdinal(Path.GetFileName a, Path.GetFileName b))
                |> Array.choose (fun dir ->
                    let file = Path.Combine(dir, "governance-handoff.json")

                    if File.Exists file then
                        Some
                            { FS.GG.Governance.Adapters.SddHandoff.Reader.Source =
                                sprintf "readiness/%s/governance-handoff.json" (Path.GetFileName dir)
                              FS.GG.Governance.Adapters.SddHandoff.Reader.Json = File.ReadAllText file }
                    else
                        None)
                |> Array.toList
        with _ ->
            []

    let realPorts (repo: string) : Ports =
        { Files = Loader.fileSystemReader repo
          Git = FS.GG.Governance.Snapshot.Interpreter.realPorts repo
          Freshness = FreshnessSensing.realSensor repo
          Store = FreshnessSensing.realStoreReader
          Write = writeAtomic
          Out = fun text -> Console.Out.WriteLine text
          Execute = FS.GG.Governance.GateExecution.Interpreter.realPort
          SenseCapability = Capability.senseCapability
          RenderReport = (fun view -> RichRender.emitStdout RenderMode.Rich view "")
          SenseEnvironment = senseEnvironmentReal
          SenseBuilder = senseBuilderReal
          SenseRelease =
            fun layout exp ->
                FS.GG.Governance.ReleaseFactsSensing.Interpreter.senseRelease
                    (FS.GG.Governance.ReleaseFactsSensing.Interpreter.realPort repo layout)
                    exp
          // 067: the real read-only surface sense closes over `repo` + the F051 real execution port at
          // construction time (mirroring `SenseRelease`/`Execute`), so the effect carries only the report.
          SenseSurfaces = senseSurfacesReal repo FS.GG.Governance.GateExecution.Interpreter.realPort
          // F070: the shared read-only generated-view currency sense (the CurrencySensing core).
          SenseViewCurrency = CS.senseRepo
          Handoffs = realHandoffs }

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request

        // Drive init → update* to Done: execute every requested Effect via `step`, feed each result Msg back
        // into the pure `update`, accumulate new effects, repeat. Stops at Done or quiescence. NEVER throws.
        let rec drive (model: Loop.Model) (effects: Loop.Effect list) : Loop.Model =
            if model.Phase = Loop.Done then
                model
            else
                match effects with
                | [] -> model
                | _ ->
                    let model2, newEffects =
                        effects
                        |> List.map (step ports)
                        |> List.fold
                            (fun (m, acc) msg ->
                                let m2, e2 = Loop.update msg m
                                m2, acc @ e2)
                            (model, [])

                    drive model2 newEffects

        drive m0 eff0
