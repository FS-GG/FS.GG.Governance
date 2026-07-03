// The EDGE interpreter of the `fsgg evidence` host command (069) — the impure code in the feature. Visibility
// lives in Interpreter.fsi (Principle II). It executes the `Loop.Effect`s the pure `update` requests against
// INJECTED, FAKEABLE ports and feeds each result back as a `Loop.Msg`. The `SenseReport` port REPLICATES the
// F12 project-sensing call-sequence (the cache-eligibility-host precedent of replicating a sense/select
// sequence rather than exposing it): `loadSnapshot` (real SpecKit + design fact sensing) → `Project.compose` /
// `Project.toLoopConfig` → the `Host` loop drive → `Project.evidenceReport`. It adds NO new third-party
// dependency. TOTAL and SAFE: every failure is caught and classified into `InputMissing` (absent/unreadable
// input ⇒ exit 3) or `ToolFault` (interpreter/host defect ⇒ exit 4); it NEVER throws and (via temp+rename)
// NEVER leaves a partial artifact. Cache-only with a zero fresh-review budget ⇒ deterministic evidence world.

namespace FS.GG.Governance.EvidenceCommand

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.DesignSystem
open FS.GG.Governance.Cli
open FS.GG.Governance.CommandHost         // 049: shared host edge leaf (writeAtomic)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    // The injected edge ports — the implementation of the type declared in Interpreter.fsi.
    type Ports =
        { SenseReport: string -> Result<ProjectEvidenceReport, Loop.ReportFault>
          Write: string -> string -> Result<unit, string>
          Out: string -> unit }

    // ── project sensing: reuse Cli.ArtifactReading (single source; #49 removed the ~325-line copy) ──

    // The domains + judge the evidence report senses over. Evidence reports declared/effective evidence, so its
    // snapshot deliberately carries NO SDD handoff and NO declared profile (those drive only `route`).
    let private evidenceDomains = Set.ofList [ SpecKitDomain; DesignSystemDomain ]

    let optionsFor () : ProjectOptions =
        { Domains = evidenceDomains
          Judge = Cli.defaultJudge
          SpecKitDial = Catalog.defaultDial }

    // A RunRequest shaped for `ArtifactReading.loadSnapshot`: it consults only Root/Scope/Domains/Judge; the
    // remaining fields are inert here and take neutral defaults.
    let private senseRequest (rawRoot: string) : RunRequest =
        { Root = rawRoot
          Command = CommandKind.EvidenceCommand
          Mode = Inner
          Format = OutputFormat.Text
          Scope = []
          Domains = evidenceDomains
          ReviewBudget = ReviewBudget.CacheOnly
          ReviewStore = None
          OutputPath = None
          Judge = Cli.defaultJudge
          ExplicitPlain = false }

    let loadSnapshot (rawRoot: string) : Result<ProjectSnapshot, string> =
        // Reuse the Cli reader (identical spec-kit/design fact sensing), then clear the two fields Evidence
        // deliberately omits: the SDD handoff and the declared profile (both route-only concerns).
        ArtifactReading.loadSnapshot (senseRequest rawRoot)
        |> Result.map (fun snapshot -> { snapshot with Handoffs = []; DefaultProfile = None })

    // ── drive the Host loop (cache-only, zero fresh-review budget ⇒ deterministic) ──

    let private stepHostEffect (root: string) (effect: FS.GG.Governance.Host.Effect) : FS.GG.Governance.Host.Msg<ProjectFact> list =
        match effect with
        | FS.GG.Governance.Host.ReadArtifact artifact -> [ FS.GG.Governance.Host.Msg.Sensed(artifact, ArtifactReading.readArtifact root artifact) ]
        | FS.GG.Governance.Host.LoadReview key -> [ FS.GG.Governance.Host.Msg.Loaded(key, Ok None) ]
        | FS.GG.Governance.Host.DispatchReview _ -> []
        | FS.GG.Governance.Host.RecordVerdict review -> [ FS.GG.Governance.Host.Msg.Recorded(review.Key, Ok()) ]
        | FS.GG.Governance.Host.EmitOutput _ -> []

    let runHost (snapshot: ProjectSnapshot) : FS.GG.Governance.Host.Model<ProjectFact> =
        let config = Project.toLoopConfig (optionsFor ()) Inner snapshot
        let model0, effects0 = FS.GG.Governance.Host.Loop.init config snapshot.Change

        let rec drive (model: FS.GG.Governance.Host.Model<ProjectFact>) effects =
            let messages = effects |> List.collect (stepHostEffect snapshot.Root)

            match messages with
            | [] -> model
            | _ ->
                let model', effects' =
                    messages
                    |> List.fold
                        (fun (m, acc) msg ->
                            let m2, produced = FS.GG.Governance.Host.Loop.update config msg m
                            m2, acc @ produced)
                        (model, [])

                drive model' effects'

        drive model0 effects0

    // ── the SenseReport port: F12 sense → Host drive → Project.evidenceReport, classified ──

    let senseReport (repo: string) : Result<ProjectEvidenceReport, Loop.ReportFault> =
        try
            match loadSnapshot repo with
            | Error reason -> Error(Loop.InputMissing reason)
            | Ok snapshot -> Ok(Project.evidenceReport (runHost snapshot))
        with ex ->
            Error(Loop.ToolFault ex.Message)

    // ── ports / step / run ──

    // `repo` is accepted to mirror the sibling hosts' `realPorts repo` shape; the repository is actually
    // threaded through the `SenseReport repo` effect at run time, so the real ports are repo-agnostic.
    let realPorts (repo: string) : Ports =
        ignore repo

        { SenseReport = senseReport
          Write = CommandHost.writeAtomic
          Out = fun text -> Console.Out.WriteLine text }

    let guard (call: unit -> Result<'a, string>) : Result<'a, string> =
        try
            call ()
        with e ->
            Error e.Message

    let step (ports: Ports) (effect: Loop.Effect) : Loop.Msg =
        match effect with
        | Loop.SenseReport repo ->
            let result =
                try
                    ports.SenseReport repo
                with e ->
                    Error(Loop.ToolFault e.Message)

            Loop.Reported result
        | Loop.WriteArtifact(path, content) -> Loop.Wrote(guard (fun () -> ports.Write path content))
        | Loop.EmitSummary text ->
            ports.Out text
            Loop.Emitted

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request

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
