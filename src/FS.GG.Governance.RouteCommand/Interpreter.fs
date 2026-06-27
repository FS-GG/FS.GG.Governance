// The EDGE interpreter of the `fsgg route` host command (F022) — the ONLY impure code in the
// feature. Visibility lives in Interpreter.fsi (Principle II). It executes the `Loop.Effect`s the
// pure `update` requests against INJECTED, FAKEABLE ports and feeds each result back as a `Loop.Msg`.
// It REUSES the existing edges verbatim — `Config.Loader` for catalog reads (F014) and
// `Snapshot.Interpreter` for git sensing (F016) — adding only the persistence (`ArtifactWriter`) and
// stdout (`OutputSink`) ports. TOTAL and SAFE (FR-010/FR-013): every port `Error` and every thrown
// exception is caught and reified to the matching `Msg` — it NEVER throws and (via temp+rename)
// NEVER leaves a partial artifact.

namespace FS.GG.Governance.RouteCommand

open System
open System.IO
open FS.GG.Governance.Config              // Loader, Schema
open FS.GG.Governance.Config.Model         // GovernedPath, Validation, Invalid, Diagnostic, FsggFile, Locator, DiagnosticId
open FS.GG.Governance.Snapshot.Model        // SnapshotOptions, GitRef, RepoSnapshot, sensingDiagnosticIdToken
open FS.GG.Governance.FreshnessSensing       // FreshnessSensing.senseFreshness, loadStore, realSensor, realStoreReader (F046)
open FS.GG.Governance.HumanText              // RenderMode (selectMode), ReportView (F27 wiring 063)
open FS.GG.Governance.HumanRender            // Capability.senseCapability, RichRender.emitStdout (Spectre confined here)

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
          // F081: locate + read every readiness/<id>/governance-handoff.json under `repo` in stable <id> order.
          Handoffs: string -> FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list }

    // Run a port call, converting BOTH an `Error` and a thrown exception into `Error` so the
    // interpreter never throws out of itself (FR-010/FR-013, the Host.Interpreter discipline).
    let guard (call: unit -> Result<'a, string>) : Result<'a, string> =
        try
            call ()
        with e ->
            Error e.Message

    // The real persistence port: create parent dirs, write to a unique temp sibling, then atomically
    // rename over the target — a failed write leaves NO partial/truncated file (research D9, FR-010).
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
                    // senseSnapshot NEVER throws; a failure surfaces as a SensingDiagnostic. Any
                    // sensing diagnostic (not-a-repo, unknown-ref, git-unavailable) ⇒ InputUnavailable.
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
            // (read the four files through ports.Files, then the pure Schema.validate). readSource +
            // validate do not throw, but a misbehaving reader could — reify that to an Invalid catalog.
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
        // leaves no partial file and is reified to the NON-FATAL `StorePersisted` (research D8/FR-001).
        | Loop.PersistStore(path, content) -> Loop.StorePersisted(guard (fun () -> ports.Write path content))

        // F052: run each requested (must-recompute command-) gate ONCE through the injected F051 port
        // (FR-001), assembling its F032 `CommandRecord` via the merged `senseExecution`. Records come back in
        // request order, tagged by GateId. The port is TOTAL & SAFE (start-failure/timeout are recorded
        // sentinel outcomes, never throws/hangs); `senseExecution` is pure given the port (interpreter edge).
        | Loop.ExecuteGates requests ->
            Loop.GatesExecuted(
                requests
                |> List.map (fun (gateId, command) ->
                    gateId, FS.GG.Governance.GateExecution.Interpreter.senseExecution ports.Execute command)
            )

        // F27 wiring (063): the render-mode dispatch lives HERE at the edge (FR-004). Json (human = None)
        // and the ANSI-free Plain path go via the existing `Out` sink (byte-stable, captured in tests); only
        // the interactive `Rich` path goes through `RenderReport` (Spectre, confined to HumanRender) followed
        // by the operational lines. The mode is `selectMode false (senseCapability explicitPlain)`.
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

        | Loop.LoadHandoffs repo -> Loop.HandoffsLoaded(ports.Handoffs repo)

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
          Handoffs = realHandoffs }

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request

        // Drive init → update* to Done: execute every requested Effect via `step`, feed each result
        // Msg back into the pure `update`, accumulate new effects, repeat. Stops at Done or quiescence
        // (EmitSummary → Emitted → Done) (the Host.Interpreter.run shape). NEVER throws.
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
