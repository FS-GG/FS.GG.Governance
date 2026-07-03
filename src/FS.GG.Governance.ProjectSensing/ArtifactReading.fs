namespace FS.GG.Governance.Cli

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.DesignSystem

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ArtifactReading =

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
        | true, value ->
            value.GetString() |> Option.ofObj
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
                    Directory.EnumerateDirectories specs
                    |> Seq.sort
                    |> Seq.tryLast
                    |> Option.defaultValue root
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
        | "constitution" -> FS.GG.Governance.Adapters.SpecKit.Phase.Constitution
        | "specify" -> FS.GG.Governance.Adapters.SpecKit.Phase.Specify
        | "clarify" -> FS.GG.Governance.Adapters.SpecKit.Phase.Clarify
        | "plan" -> FS.GG.Governance.Adapters.SpecKit.Phase.Plan
        | "tasks" -> FS.GG.Governance.Adapters.SpecKit.Phase.Tasks
        | "analyze" -> FS.GG.Governance.Adapters.SpecKit.Phase.Analyze
        | "implement" -> FS.GG.Governance.Adapters.SpecKit.Phase.Implement
        | "merge" -> FS.GG.Governance.Adapters.SpecKit.Phase.Merge
        | _ -> FS.GG.Governance.Adapters.SpecKit.Phase.Implement

    let phaseFor (root: string) (featureDir: string) =
        let explicit = Path.Combine(root, ".governance-phase")

        if File.Exists explicit then
            File.ReadAllText explicit |> phaseFromText
        else
            match Path.GetFileName(featureDir) |> Option.ofObj with
            | Some name when name.Contains("merge", StringComparison.OrdinalIgnoreCase) ->
                FS.GG.Governance.Adapters.SpecKit.Phase.Merge
            | _ ->
                FS.GG.Governance.Adapters.SpecKit.Phase.Implement

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
        | "X" | "x" -> Real
        | "-" -> Skipped
        | "S" | "s" -> Synthetic
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
            not (text.Contains("[NEEDS", StringComparison.OrdinalIgnoreCase)
                 || text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        else
            true

    let specKitFacts (root: string) =
        let featureDir = activeFeatureDirectory root
        let phase = phaseFor root featureDir
        let artifactKeys = [ "constitution"; "spec"; "plan"; "research"; "data-model"; "contracts"; "quickstart"; "tasks"; "task-deps" ]

        let taskText =
            [ Path.Combine(featureDir, "tasks.md")
              Path.Combine(root, "tasks.md") ]
            |> List.tryPick (fun path -> match tryReadAllText path with | Ok text -> Some text | Error _ -> None)
            |> Option.defaultValue ""

        let depsText =
            [ Path.Combine(featureDir, "tasks.deps.yml")
              Path.Combine(root, "tasks.deps.yml") ]
            |> List.tryPick (fun path -> match tryReadAllText path with | Ok text -> Some text | Error _ -> None)
            |> Option.defaultValue ""

        let states = taskStatesFrom taskText
        let deps = taskDependenciesFrom depsText
        let surfaces = artifactKeys |> List.filter (artifactPresent root featureDir) |> List.map specKitArtifactOfKey |> Set.ofList

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
                      yield DesignSystemProjectFact(DesignSystemFact.SurfaceObservation(observation.Name, subject, observation.Value.GetBoolean()))
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

    let optionsFor (request: RunRequest) : ProjectOptions =
        { Domains = request.Domains
          Judge = request.Judge
          SpecKitDial = Catalog.defaultDial }

    let artifactsFor (request: RunRequest) =
        let composed = Project.compose (optionsFor request)

        composed.Catalog
        |> List.collect (fun rule -> Check.reads rule.Check)
        |> List.distinct
        |> List.sortBy (fun artifact -> artifact.Kind + ":" + artifact.Key)

    // F081 wiring: locate + read every `readiness/<id>/governance-handoff.json` under `root` in
    // stable `<id>` order (mirrors `RouteCommand.Interpreter.realHandoffs`). The impure edge the
    // handoff consumer needs; TOTAL & SAFE — a missing/unreadable `readiness/` tree degrades to `[]`
    // (a byte-identical route), never a throw (Principle VI). `loadSnapshot` then carries these on the
    // snapshot so the pure `Cli` route verdict can fold them through `Consumer.consume`.
    let locateHandoffs (root: string) : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list =
        try
            let readinessDir = Path.Combine(root, "readiness")

            if not (Directory.Exists readinessDir) then
                []
            else
                Directory.GetDirectories readinessDir
                // #49 (D3): spell out the ordinal sort so the guarantee is visible and identical to the
                // host `realHandoffs` copies (`Array.sortBy` on a string key is ordinal today, but not
                // self-evidently so — a maintainer could port it to a culture-sensitive form).
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

    // 090 wiring: read the product's declared `defaultProfile` at the Config-load edge. TOTAL &
    // SAFE — an absent/invalid `.fsgg` (no policy, missing required files, dangling default) degrades
    // to `None`, which the `route` exit resolves to `Strict` (the one-way fail-safe, FR-004). The pure
    // `Cli` route verdict recognizes the carried id through `Enforcement.recognizeProfile`.
    let locateDefaultProfile (root: string) : FS.GG.Governance.Config.Model.ProfileId option =
        try
            match FS.GG.Governance.Config.Loader.loadAndValidate root with
            | FS.GG.Governance.Config.Model.Valid facts -> facts.Policy |> Option.map (fun p -> p.DefaultProfile)
            | FS.GG.Governance.Config.Model.Invalid _ -> None
        with _ ->
            None

    let loadSnapshot (request: RunRequest) =
        let root = Path.GetFullPath request.Root

        try
            if not (Directory.Exists root) then
                Error("root does not exist or is not a directory: " + root)
            else
                Directory.EnumerateFileSystemEntries root |> Seq.truncate 1 |> Seq.toList |> ignore

                let specFacts, specChange = specKitFacts root
                let designFacts, designChange = designFacts root

                let facts: FactSet<ProjectFact> =
                    [ yield! specFacts
                      yield! designFacts ]
                    |> List.map fact

                let change: ProjectChange =
                    { SpecKit = Some specChange
                      DesignSystem = if List.isEmpty designFacts then None else Some designChange
                      Scope = request.Scope }

                Ok
                    { Root = root
                      Supplied = facts
                      Change = change
                      Artifacts = artifactsFor request
                      Handoffs = locateHandoffs root
                      DefaultProfile = locateDefaultProfile root }
        with ex ->
            Error ex.Message
