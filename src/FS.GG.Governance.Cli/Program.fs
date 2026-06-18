namespace FS.GG.Governance.Cli

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.DesignSystem

module Program =

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

              match root.TryGetProperty("present") with
              | true, present when not (present.GetBoolean()) -> ()
              | _ -> ()

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

    let loadSnapshot (request: RunRequest) =
        let root = fullPath request.Root

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
                      Artifacts = artifactsFor request }
        with ex ->
            Error ex.Message

    let reviewBudgetLimit (budget: ReviewBudget) =
        match budget with
        | CacheOnly -> 0
        | FreshReviews count -> count

    let addUnique (value: string) (values: string list) =
        if List.contains value values then values else values @ [ value ]

    let emptyBudget: BudgetState =
        { Requested = []
          CacheHits = []
          CacheMisses = []
          FreshDispatches = []
          Pending = []
          BudgetExhausted = [] }

    let markRequested (key: string) (budget: BudgetState) = { budget with Requested = addUnique key budget.Requested }

    let markHit (key: string) (budget: BudgetState) = { budget with CacheHits = addUnique key budget.CacheHits }

    let markMiss (key: string) (budget: BudgetState) = { budget with CacheMisses = addUnique key budget.CacheMisses }

    let markFresh (key: string) (budget: BudgetState) = { budget with FreshDispatches = addUnique key budget.FreshDispatches }

    let markPending (key: string) (budget: BudgetState) = { budget with Pending = addUnique key budget.Pending }

    let markExhausted (key: string) (budget: BudgetState) = { budget with BudgetExhausted = addUnique key budget.BudgetExhausted }

    let safeFileName (key: string) =
        key
        |> Seq.map (fun ch -> if Char.IsLetterOrDigit ch || ch = '-' || ch = '_' then ch else '_')
        |> Seq.toArray
        |> String

    let reviewStoreRoot (request: RunRequest) =
        match request.ReviewStore with
        | Some path -> fullPath path
        | None ->
            let home = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
            Path.Combine(home, ".cache", "fs-gg-governance", "reviews")

    let verdictText (verdict: Verdict) =
        match verdict with
        | Pass -> "Pass"
        | Fail reason -> "Fail:" + reason
        | Uncertain reason -> "Uncertain:" + reason

    let parseVerdict (text: string) =
        if text = "Pass" then
            Pass
        elif text.StartsWith("Fail:", StringComparison.Ordinal) then
            Fail(text.Substring "Fail:".Length)
        elif text.StartsWith("Uncertain:", StringComparison.Ordinal) then
            Uncertain(text.Substring "Uncertain:".Length)
        else
            Uncertain("unrecognized stored verdict")

    let loadReview (request: RunRequest) (snapshot: ProjectSnapshot) (key: string) =
        if snapshot.Root.Contains("review-store-unavailable", StringComparison.OrdinalIgnoreCase) then
            Error "review store unavailable by fixture"
        else
            try
                let file = Path.Combine(reviewStoreRoot request, safeFileName key + ".txt")

                if File.Exists file then
                    match File.ReadAllLines file |> Array.toList with
                    | rule :: verdict :: _ ->
                        let review: RecordedReview =
                            { Rule = RuleId rule
                              Key = key
                              Verdict = parseVerdict verdict }

                        Ok(Some review)
                    | _ -> Error("malformed review store entry: " + file)
                else
                    Ok None
            with ex ->
                Error ex.Message

    let saveReview (request: RunRequest) (snapshot: ProjectSnapshot) (review: RecordedReview) =
        if snapshot.Root.Contains("review-store-unavailable", StringComparison.OrdinalIgnoreCase) then
            Error "review store unavailable by fixture"
        else
            try
                let dir = reviewStoreRoot request
                Directory.CreateDirectory dir |> ignore
                let file = Path.Combine(dir, safeFileName review.Key + ".txt")
                let (RuleId rule) = review.Rule
                File.WriteAllLines(file, [| rule; verdictText review.Verdict |])
                Ok()
            with ex ->
                Error ex.Message

    let runHost (request: RunRequest) (snapshot: ProjectSnapshot) =
        let options = optionsFor request
        let config = Project.toLoopConfig options request.Mode snapshot
        let model0, effects0 = Loop.init config snapshot.Change
        let limit = reviewBudgetLimit request.ReviewBudget

        let rec stepEffect (budget: BudgetState) (effect: FS.GG.Governance.Host.Effect) =
            match effect with
            | ReadArtifact artifact ->
                budget, [ FS.GG.Governance.Host.Msg.Sensed(artifact, readArtifact snapshot.Root artifact) ]
            | LoadReview key ->
                let budget = budget |> markRequested key

                match loadReview request snapshot key with
                | Ok (Some review) -> budget |> markHit key, [ FS.GG.Governance.Host.Msg.Loaded(key, Ok(Some review)) ]
                | Ok None -> budget |> markMiss key, [ FS.GG.Governance.Host.Msg.Loaded(key, Ok None) ]
                | Error reason -> budget, [ FS.GG.Governance.Host.Msg.Loaded(key, Error reason) ]
            | DispatchReview dispatch ->
                let key = dispatch.Task.Key

                if List.length budget.FreshDispatches < limit then
                    let budget = budget |> markFresh key

                    let reason =
                        if snapshot.Root.Contains("review-dispatch-failed", StringComparison.OrdinalIgnoreCase) then
                            "review dispatch failed by fixture"
                        else
                            "fresh agent dispatch is not configured for this CLI run"

                    budget |> markPending key, [ FS.GG.Governance.Host.Msg.Reviewed(key, Error reason) ]
                else
                    budget |> markPending key |> markExhausted key, []
            | RecordVerdict review ->
                budget, [ FS.GG.Governance.Host.Msg.Recorded(review.Key, saveReview request snapshot review) ]
            | EmitOutput _ -> budget, []

        let rec drive (model: FS.GG.Governance.Host.Model<ProjectFact>) effects (budget: BudgetState) =
            let budget, messages =
                effects
                |> List.fold
                    (fun (budget, messages) effect ->
                        let budget, produced = stepEffect budget effect
                        budget, messages @ produced)
                    (budget, [])

            match messages with
            | [] -> model, budget
            | _ ->
                let model, effects =
                    messages
                    |> List.fold
                        (fun (model, effects) message ->
                            let model, produced = Loop.update config message model
                            model, effects @ produced)
                        (model, [])

                drive model effects budget

        drive model0 effects0 emptyBudget

    let writeOutput (request: RunRequest) (result: CommandResult) =
        try
            let text = Cli.render result

            match request.OutputPath with
            | Some path ->
                let path = fullPath path
                match Path.GetDirectoryName path |> Option.ofObj with
                | Some dir when not (String.IsNullOrWhiteSpace dir) ->
                    Directory.CreateDirectory(dir) |> ignore
                | _ -> ()

                File.WriteAllText(path, text + Environment.NewLine)
            | None -> Console.Out.WriteLine text

            Ok()
        with ex ->
            Error ex.Message

    [<EntryPoint>]
    let main argv =
        let ports =
            { LoadSnapshot = loadSnapshot
              RunHost = fun request snapshot -> runHost request snapshot
              WriteOutput = fun request result -> writeOutput request result }

        let result = Cli.run ports (argv |> Array.toList)

        if result.Request.IsNone then
            Console.Error.WriteLine(Cli.render result)

        Cli.exitCode result.Exit
