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
          Execute: FS.GG.Governance.GateExecution.Model.ExecutionPort }

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

        | Loop.EmitSummary text ->
            ports.Out text
            Loop.Emitted

    let realPorts (repo: string) : Ports =
        { Files = Loader.fileSystemReader repo
          Git = FS.GG.Governance.Snapshot.Interpreter.realPorts repo
          Freshness = FreshnessSensing.realSensor repo
          Store = FreshnessSensing.realStoreReader
          Write = writeAtomic
          Out = fun text -> Console.Out.WriteLine text
          Execute = FS.GG.Governance.GateExecution.Interpreter.realPort }

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
