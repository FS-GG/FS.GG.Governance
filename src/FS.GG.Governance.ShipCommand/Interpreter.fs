// The EDGE interpreter of the `fsgg ship` host command (F026) — the ONLY impure code in the feature.
// Visibility lives in Interpreter.fsi (Principle II). It executes the `Loop.Effect`s the pure `update`
// requests against INJECTED, FAKEABLE ports and feeds each result back as a `Loop.Msg`. It REUSES the
// existing edges verbatim — `Config.Loader` for catalog reads (F014) and `Snapshot.Interpreter` for git
// sensing (F016) — adding only the persistence (`ArtifactWriter`) and stdout (`OutputSink`) ports.
// TOTAL and SAFE (FR-010/FR-013): every port `Error` and every thrown exception is caught and reified
// to the matching `Msg` — it NEVER throws and (via temp+rename) NEVER leaves a partial artifact, and a
// write failure is reified to a `Wrote(_, Error _)` (mapped by `update` to ToolError, never Blocked).

namespace FS.GG.Governance.ShipCommand

open System
open System.IO
open FS.GG.Governance.Config              // Loader, Schema
open FS.GG.Governance.Config.Model         // GovernedPath, Validation, Invalid, Diagnostic, Locator, DiagnosticId
open FS.GG.Governance.Snapshot.Model        // SnapshotOptions, GitRef, RepoSnapshot, sensingDiagnosticIdToken
open FS.GG.Governance.FreshnessSensing       // FreshnessSensing.senseFreshness, loadStore, realSensor, realStoreReader (F046)
open FS.GG.Governance.HumanText              // RenderMode (selectMode), ReportView (F27 wiring 063)
open FS.GG.Governance.HumanRender            // Capability.senseCapability, RichRender.emitStdout (Spectre confined here)
open FS.GG.Governance.CommandHost           // 049: shared host-loop combinators (guard/drive)

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
          /// F070: sense generated-view currency for `repo`, returning the stale-view findings (gated by the
          /// manifest's `currency-enforcement` dial). `realPorts` wires the real read-only sense reusing the
          /// F057 refresh machinery; tests inject a deterministic port. TOTAL & SAFE (catches its own
          /// exceptions ⇒ `[]`, never a fabricated finding, never a crash). `[]` ⇒ byte-identical ship.json.
          SenseViewCurrency: string -> CE.CurrencyFinding list
          /// F081: locate + read every `readiness/<id>/governance-handoff.json` under `repo` in stable
          /// `<id>` order. The only I/O the handoff consumer needs; `[]` when none ⇒ byte-identical ship.
          Handoffs: string -> FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list }

    let step (ports: Ports) (effect: Loop.Effect) : Loop.Msg =
        match effect with
        | Loop.SenseScope scope ->
            let options =
                match scope with
                | Loop.Since rev -> { Since = Some(GitRef rev); Base = None; Head = None }
                | Loop.ExplicitPaths _
                | Loop.DefaultRange -> { Since = None; Base = None; Head = None }

            Loop.Sensed(CommandHost.senseSnapshotResult ports.Git options)

        | Loop.LoadCatalog _ -> Loop.Loaded(CommandHost.loadCatalogValidation ports.Files)

        | Loop.SenseFreshness(gates, baseHead) ->
            // F046: assemble SensedFacts at the shared sensing edge; an Error here DEGRADES in `update`.
            Loop.FreshnessSensed(FreshnessSensing.senseFreshness ports.Freshness gates baseHead)

        | Loop.LoadStore path ->
            // F046: read-only store load (absent ⇒ empty); a malformed store DEGRADES in `update`.
            Loop.StoreLoaded(FreshnessSensing.loadStore ports.Store path)

        | Loop.WriteArtifact(kind, path, content) -> Loop.Wrote(kind, CommandHost.guard (fun () -> ports.Write path content))

        // F048: reuse the existing atomic `writeAtomic` (temp + rename) for the store write — a failed write
        // leaves no partial file and is reified to the NON-FATAL `StorePersisted` (research D8/FR-001).
        | Loop.PersistStore(path, content) -> Loop.StorePersisted(CommandHost.guard (fun () -> ports.Write path content))

        // F052: run each requested must-recompute command-gate ONCE through the injected F051 port (FR-001),
        // assembling its F032 `CommandRecord` via the merged `senseExecution`. Records come back in request
        // order, tagged by GateId. The port is TOTAL & SAFE; `senseExecution` is pure given the port.
        | Loop.ExecuteGates requests ->
            Loop.GatesExecuted(
                requests
                |> List.map (fun (gateId, command) ->
                    gateId, FS.GG.Governance.GateExecution.Interpreter.senseExecution ports.Execute command)
            )

        // F25 wiring (064): the two NEW normalized provenance senses. Both are normalized (no username, host,
        // absolute path, or wall-clock) so `provenance.json` is byte-identical across machines/re-runs.
        | Loop.SenseProvenance -> Loop.ProvenanceSensed(ports.SenseEnvironment(), ports.SenseBuilder())

        // F27 wiring (063): the render-mode dispatch lives HERE at the edge (FR-004). Json (human = None)
        // and the ANSI-free Plain path go via the existing `Out` sink (byte-stable, captured in tests); only
        // the interactive `Rich` path goes through `RenderReport` (Spectre, confined to HumanRender) followed
        // by the operational line. The mode is `selectMode false (senseCapability explicitPlain)`.
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

        // F070 (stale-view blocking): sense generated-view currency at the edge. The port is TOTAL & SAFE; an
        // empty result ⇒ byte-identical ship.json (FR-004).
        | Loop.SenseViewCurrency repo -> Loop.ViewCurrencySensed(ports.SenseViewCurrency repo)

        | Loop.LoadHandoffs repo -> Loop.HandoffsLoaded(ports.Handoffs repo)

    let realPorts (repo: string) : Ports =
        { Files = Loader.fileSystemReader repo
          Git = FS.GG.Governance.Snapshot.Interpreter.realPorts repo
          Freshness = FreshnessSensing.realSensor repo
          Store = FreshnessSensing.realStoreReader
          Write = CommandHost.writeAtomic
          Out = fun text -> Console.Out.WriteLine text
          Execute = FS.GG.Governance.GateExecution.Interpreter.realPort
          SenseCapability = Capability.senseCapability
          RenderReport = (fun view -> RichRender.emitStdout RenderMode.Rich view "")
          SenseEnvironment = CommandHost.senseEnvironmentReal
          SenseBuilder = CommandHost.senseBuilderReal
          // F070: the shared read-only generated-view currency sense (the CurrencySensing core).
          SenseViewCurrency = CS.senseRepo
          Handoffs = CommandHost.realHandoffs }

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request

        // Drive init → update* to Done: execute every requested Effect via `step`, feed each result
        // Msg back into the pure `update`, accumulate new effects, repeat. Stops at Done or quiescence
        // (EmitSummary → Emitted → Done). NEVER throws.
        CommandHost.drive (fun (m: Loop.Model) -> m.Phase = Loop.Done) (step ports) Loop.update m0 eff0
