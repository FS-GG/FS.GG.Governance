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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    // The injected edge ports — the implementation of the type declared in Interpreter.fsi.
    type Ports =
        { SenseReport: string -> Result<ProjectEvidenceReport, Loop.ReportFault>
          Write: string -> string -> Result<unit, string>
          Out: string -> unit }

    // ── F12 project sensing (replicated verbatim from the Cli composition root; same call-sequence) ──

    let fullPath (path: string) = Path.GetFullPath path

    let fact (value: ProjectFact) : FactAssertion<ProjectFact> =
        { Id = Project.identify value
          Value = value
          Provenance = [] }

    let tryReadAllText (path: string) =
        try
            if File.Exists path then Ok(File.ReadAllText path) else Error("missing " + path)
        with ex ->
            Error ex.Message

    let readJson (path: string) =
        try
            if File.Exists path then
                use doc = JsonDocument.Parse(File.ReadAllText path)
                Some(doc.RootElement.Clone())
            else
                None
        with _ ->
            None

    let stringProperty (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value -> value.GetString() |> Option.ofObj
        | _ -> None

    let activeFeatureDirectory (root: string) =
        if File.Exists(Path.Combine(root, "tasks.md")) || File.Exists(Path.Combine(root, "spec.md")) then
            root
        else
            let fromFeatureJson =
                readJson (Path.Combine(root, ".specify", "feature.json"))
                |> Option.bind (stringProperty "feature_directory")
                |> Option.map (fun relative -> Path.Combine(root, relative))

            match fromFeatureJson with
            | Some dir when Directory.Exists dir -> dir
            | _ ->
                let specs = Path.Combine(root, "specs")

                if Directory.Exists(Path.Combine(specs, "012-cli")) then
                    Path.Combine(specs, "012-cli")
                elif Directory.Exists specs then
                    Directory.EnumerateDirectories specs |> Seq.sort |> Seq.tryLast |> Option.defaultValue root
                else
                    root

    let specKitArtifactPath (root: string) (featureDir: string) (key: string) =
        match key with
        | "constitution" -> Path.Combine(root, ".specify", "memory", "constitution.md")
        | "spec" -> Path.Combine(featureDir, "spec.md")
        | "plan" -> Path.Combine(featureDir, "plan.md")
        | "research" -> Path.Combine(featureDir, "research.md")
        | "data-model" -> Path.Combine(featureDir, "data-model.md")
        | "contracts" -> Path.Combine(featureDir, "contracts")
        | "quickstart" -> Path.Combine(featureDir, "quickstart.md")
        | "tasks" -> Path.Combine(featureDir, "tasks.md")
        | "task-deps" -> Path.Combine(featureDir, "tasks.deps.yml")
        | _ -> Path.Combine(featureDir, key)

    let designFileName (key: string) =
        match key with
        | "token-document" -> "token-document.json"
        | "generated-token-surface" -> "generated-token-surface.json"
        | "rendered-capture" -> "rendered-capture.json"
        | "interaction-state-spec" -> "interaction-state-spec.json"
        | "page-pattern-spec" -> "page-pattern-spec.json"
        | other -> other + ".json"

    let designBase (root: string) =
        let nested = Path.Combine(root, "design-system")
        if Directory.Exists nested then nested else root

    let designArtifactPath (root: string) (key: string) =
        Path.Combine(designBase root, designFileName key)

    let readArtifact (root: string) (artifact: ArtifactRef) =
        let featureDir = activeFeatureDirectory root

        let path =
            match artifact.Kind with
            | "speckit" -> specKitArtifactPath root featureDir artifact.Key
            | "design" -> designArtifactPath root artifact.Key
            | _ -> Path.Combine(root, artifact.Kind, artifact.Key)

        try
            if Directory.Exists path then
                Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                |> Seq.sort
                |> Seq.map (fun file -> "### " + Path.GetRelativePath(root, file) + "\n" + File.ReadAllText file)
                |> String.concat "\n"
                |> Ok
            elif File.Exists path then
                Ok(File.ReadAllText path)
            else
                Error("missing " + path)
        with ex ->
            Error ex.Message

    let phaseFromText (text: string) =
        match text.Trim().ToLowerInvariant() with
        | "constitution" -> Phase.Constitution
        | "specify" -> Phase.Specify
        | "clarify" -> Phase.Clarify
        | "plan" -> Phase.Plan
        | "tasks" -> Phase.Tasks
        | "analyze" -> Phase.Analyze
        | "implement" -> Phase.Implement
        | "merge" -> Phase.Merge
        | _ -> Phase.Implement

    let phaseFor (root: string) (featureDir: string) =
        let explicit = Path.Combine(root, ".governance-phase")

        if File.Exists explicit then
            File.ReadAllText explicit |> phaseFromText
        else
            match Path.GetFileName(featureDir) |> Option.ofObj with
            | Some name when name.Contains("merge", StringComparison.OrdinalIgnoreCase) -> Phase.Merge
            | _ -> Phase.Implement

    let specKitArtifactOfKey (key: string) =
        match key with
        | "constitution" -> SpecKitArtifact.Constitution
        | "spec" -> SpecKitArtifact.Spec
        | "plan" -> SpecKitArtifact.Plan
        | "research" -> SpecKitArtifact.Research
        | "data-model" -> SpecKitArtifact.DataModel
        | "contracts" -> SpecKitArtifact.Contracts
        | "quickstart" -> SpecKitArtifact.Quickstart
        | "tasks" -> SpecKitArtifact.Tasks
        | "task-deps" -> SpecKitArtifact.TaskDeps
        | _ -> SpecKitArtifact.Tasks

    let artifactPresent (root: string) (featureDir: string) (key: string) =
        let path = specKitArtifactPath root featureDir key
        File.Exists path || Directory.Exists path

    let taskStateFromMarker (marker: string) =
        match marker with
        | "X"
        | "x" -> Real
        | "-" -> Skipped
        | "S"
        | "s" -> Synthetic
        | _ -> Pending

    let taskStatesFrom (content: string) =
        Regex.Matches(content, @"-\s+\[(?<mark>[ XxSs\-])\]\s+(?<id>T\d+)")
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Groups["id"].Value, taskStateFromMarker m.Groups["mark"].Value)
        |> Seq.distinct
        |> Seq.toList

    let taskDependenciesFrom (content: string) =
        let arrows =
            Regex.Matches(content, @"(?<a>T\d+)\s*->\s*(?<b>T\d+)")
            |> Seq.cast<Match>
            |> Seq.map (fun m -> m.Groups["a"].Value, m.Groups["b"].Value)
            |> Seq.toList

        let yaml =
            Regex.Matches(content, @"(?m)^\s*(?<a>T\d+)\s*:\s*\[?(?<deps>[T\d,\s]+)\]?\s*$")
            |> Seq.cast<Match>
            |> Seq.collect (fun m ->
                let a = m.Groups["a"].Value

                m.Groups["deps"].Value.Split([| ','; ' ' |], StringSplitOptions.RemoveEmptyEntries)
                |> Seq.filter (fun dep -> dep.StartsWith("T", StringComparison.Ordinal))
                |> Seq.map (fun dep -> a, dep))
            |> Seq.toList

        arrows @ yaml |> List.distinct |> List.sort

    let constitutionFilled (root: string) =
        let path = Path.Combine(root, ".specify", "memory", "constitution.md")

        if File.Exists path then
            let text = File.ReadAllText path

            not (
                text.Contains("[NEEDS", StringComparison.OrdinalIgnoreCase)
                || text.Contains("TODO", StringComparison.OrdinalIgnoreCase)
            )
        else
            true

    let specKitFacts (root: string) =
        let featureDir = activeFeatureDirectory root
        let phase = phaseFor root featureDir

        let artifactKeys =
            [ "constitution"; "spec"; "plan"; "research"; "data-model"; "contracts"; "quickstart"; "tasks"; "task-deps" ]

        let taskText =
            [ Path.Combine(featureDir, "tasks.md"); Path.Combine(root, "tasks.md") ]
            |> List.tryPick (fun path ->
                match tryReadAllText path with
                | Ok text -> Some text
                | Error _ -> None)
            |> Option.defaultValue ""

        let depsText =
            [ Path.Combine(featureDir, "tasks.deps.yml"); Path.Combine(root, "tasks.deps.yml") ]
            |> List.tryPick (fun path ->
                match tryReadAllText path with
                | Ok text -> Some text
                | Error _ -> None)
            |> Option.defaultValue ""

        let states = taskStatesFrom taskText
        let deps = taskDependenciesFrom depsText

        let surfaces =
            artifactKeys
            |> List.filter (artifactPresent root featureDir)
            |> List.map specKitArtifactOfKey
            |> Set.ofList

        let facts =
            [ yield SpecKitProjectFact(SpecKitFact.PhaseReached phase)

              for key in artifactKeys do
                  if artifactPresent root featureDir key then
                      yield SpecKitProjectFact(SpecKitFact.ArtifactPresent(specKitArtifactOfKey key))

              if File.Exists(Path.Combine(root, ".specify", "memory", "constitution.md")) then
                  yield SpecKitProjectFact(SpecKitFact.ConstitutionArea("constitution", constitutionFilled root))

              for taskId, state in states do
                  yield SpecKitProjectFact(SpecKitFact.TaskState(taskId, state))

              for dependent, dependency in deps do
                  yield SpecKitProjectFact(SpecKitFact.TaskDependsOn(dependent, dependency)) ]

        facts, { Phase = phase; Surfaces = surfaces }

    let stateOfName (name: string) =
        match name.ToLowerInvariant() with
        | "pending" -> Some Pending
        | "real" -> Some Real
        | "synthetic" -> Some Synthetic
        | "failed" -> Some Failed
        | "skipped" -> Some Skipped
        | _ -> None

    let designFactsFromFile (subject: DesignArtifactRef) (path: string) =
        match readJson path with
        | None -> []
        | Some root ->
            [ yield DesignSystemProjectFact(DesignSystemFact.ArtifactPresent subject)

              match root.TryGetProperty("observations") with
              | true, observations ->
                  for observation in observations.EnumerateObject() do
                      yield
                          DesignSystemProjectFact(
                              DesignSystemFact.SurfaceObservation(observation.Name, subject, observation.Value.GetBoolean())
                          )
              | _ -> ()

              match root.TryGetProperty("measurements") with
              | true, measurements ->
                  for measurement in measurements.EnumerateArray() do
                      let id = stringProperty "id" measurement
                      let state = stringProperty "state" measurement |> Option.bind stateOfName

                      match id, state with
                      | Some id, Some state -> yield DesignSystemProjectFact(DesignSystemFact.MeasurementState(id, state))
                      | _ -> ()
              | _ -> ()

              match root.TryGetProperty("verdictRestsOn") with
              | true, verdicts ->
                  for verdict in verdicts.EnumerateArray() do
                      match stringProperty "verdict" verdict, stringProperty "measurement" verdict with
                      | Some v, Some m -> yield DesignSystemProjectFact(DesignSystemFact.VerdictRestsOn(v, m))
                      | _ -> ()
              | _ -> () ]

    let designFacts (root: string) =
        let baseDir = designBase root
        let policyPath = Path.Combine(baseDir, "policy.json")

        let policyFacts =
            match readJson policyPath with
            | None -> []
            | Some policy ->
                [ match stringProperty "policy" policy with
                  | Some value -> yield DesignSystemProjectFact(DesignSystemFact.PolicySelected value)
                  | None -> ()

                  match policy.TryGetProperty("designRules") with
                  | true, rules ->
                      for rule in rules.EnumerateArray() do
                          match rule.GetString() |> Option.ofObj with
                          | None -> ()
                          | Some value -> yield DesignSystemProjectFact(DesignSystemFact.DesignRule value)
                  | _ -> () ]

        let artifactFiles =
            [ TokenDocument, "token-document.json"
              GeneratedTokenSurface, "generated-token-surface.json"
              RenderedCapture, "rendered-capture.json"
              InteractionStateSpec, "interaction-state-spec.json"
              PagePatternSpec, "page-pattern-spec.json" ]

        let facts =
            [ yield! policyFacts

              for artifact, file in artifactFiles do
                  let path = Path.Combine(baseDir, file)

                  if File.Exists path then
                      yield! designFactsFromFile artifact path ]

        let surfaces =
            [ for artifact, file in artifactFiles do
                  if File.Exists(Path.Combine(baseDir, file)) then
                      artifact ]
            |> Set.ofList

        facts, { Surfaces = surfaces }

    let optionsFor () : ProjectOptions =
        { Domains = Set.ofList [ SpecKitDomain; DesignSystemDomain ]
          Judge = Project.defaultJudge
          SpecKitDial = Catalog.defaultDial }

    let artifactsFor () =
        let composed = Project.compose (optionsFor ())

        composed.Catalog
        |> List.collect (fun rule -> Check.reads rule.Check)
        |> List.distinct
        |> List.sortBy (fun artifact -> artifact.Kind + ":" + artifact.Key)

    let loadSnapshot (rawRoot: string) : Result<ProjectSnapshot, string> =
        let root = fullPath rawRoot

        try
            if not (Directory.Exists root) then
                Error("root does not exist or is not a directory: " + root)
            else
                let specFacts, specChange = specKitFacts root
                let designFacts, designChange = designFacts root

                let facts: FactSet<ProjectFact> =
                    [ yield! specFacts; yield! designFacts ] |> List.map fact

                let change: ProjectChange =
                    { SpecKit = Some specChange
                      DesignSystem = if List.isEmpty designFacts then None else Some designChange
                      Scope = [] }

                Ok
                    { Root = root
                      Supplied = facts
                      Change = change
                      Artifacts = artifactsFor ()
                      // The evidence command does not consume the SDD handoff (it reports declared/effective
                      // evidence, not the route gate verdict); the handoff drives only `route` (Cli.resultForHost).
                      Handoffs = []
                      // 090: the policy profile is consulted only by the `route` gate verdict; the evidence
                      // report does not enforce, so it carries no declared profile.
                      DefaultProfile = None }
        with ex ->
            Error ex.Message

    // ── drive the Host loop (cache-only, zero fresh-review budget ⇒ deterministic) ──

    let private stepHostEffect (root: string) (effect: FS.GG.Governance.Host.Effect) : FS.GG.Governance.Host.Msg<ProjectFact> list =
        match effect with
        | FS.GG.Governance.Host.ReadArtifact artifact -> [ FS.GG.Governance.Host.Msg.Sensed(artifact, readArtifact root artifact) ]
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

    // ── atomic write (temp+rename: a failed write leaves NO partial file) ──

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
        with ex ->
            Error ex.Message

    // ── ports / step / run ──

    // `repo` is accepted to mirror the sibling hosts' `realPorts repo` shape; the repository is actually
    // threaded through the `SenseReport repo` effect at run time, so the real ports are repo-agnostic.
    let realPorts (repo: string) : Ports =
        ignore repo

        { SenseReport = senseReport
          Write = writeAtomic
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
