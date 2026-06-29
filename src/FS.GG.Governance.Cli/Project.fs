namespace FS.GG.Governance.Cli

open System
open System.Security.Cryptography
open System.Text
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.DesignSystem

module SpecKitCatalog = FS.GG.Governance.Adapters.SpecKit.Catalog
module DesignCatalog = FS.GG.Governance.Adapters.DesignSystem.Catalog

type Domain =
    | SpecKitDomain
    | DesignSystemDomain

type ProjectFact =
    | SpecKitProjectFact of SpecKitFact
    | DesignSystemProjectFact of DesignSystemFact
    | GovernanceFact of RuleOutcome
    | ArtifactContentFact of artifact: ArtifactRef * hash: string * content: string
    | EvidenceStateFact of node: string * state: EvidenceState
    | EvidenceDependencyFact of dependent: string * dependency: string
    | FreshnessFact of node: string * recorded: int64 * covered: int64 list

type ProjectChange =
    { SpecKit: SpecKitChange option
      DesignSystem: DesignChange option
      Scope: string list }

type ProjectSnapshot =
    { Root: string
      Supplied: FactSet<ProjectFact>
      Change: ProjectChange
      Artifacts: ArtifactRef list
      Handoffs: FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list
      DefaultProfile: FS.GG.Governance.Config.Model.ProfileId option }

type ProjectOptions =
    { Domains: Set<Domain>
      Judge: JudgeId
      SpecKitDial: ConstitutionDial }

type EvidenceNodeReport =
    { Id: string
      Declared: EvidenceState option
      Effective: EvidenceState option
      Freshness: Freshness option
      Source: string }

type ProjectEvidenceReport =
    { Nodes: EvidenceNodeReport list
      Dependencies: (string * string) list
      Disclosures: Disclosure list
      Failures: Failure list }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Project =

    let (|SpecKitProject|_|) (fact: ProjectFact) =
        match fact with
        | SpecKitProjectFact f -> Some f
        | _ -> None

    let (|DesignSystemProject|_|) (fact: ProjectFact) =
        match fact with
        | DesignSystemProjectFact f -> Some f
        | _ -> None

    let factIdText (FactId value) = value

    let ruleIdText (RuleId value) = value

    let artifactKey (artifact: ArtifactRef) = artifact.Kind + ":" + artifact.Key

    let outcomeKey (outcome: RuleOutcome) =
        match outcome with
        | RuleOutcome.Decided (rule, verdict) -> "decided:" + ruleIdText rule + ":" + sprintf "%A" verdict
        | RuleOutcome.NeedsReview request -> "needs:" + request.Key
        | RuleOutcome.Reviewed review -> "reviewed:" + review.Key
        | RuleOutcome.Escalated rule -> "escalated:" + ruleIdText rule

    let identify (fact: ProjectFact) : FactId =
        match fact with
        | SpecKitProjectFact f -> FactId("speckit:" + factIdText (SpecKit.identify f))
        | DesignSystemProjectFact f -> FactId("design:" + factIdText (DesignSystem.identify f))
        | GovernanceFact outcome -> FactId("governance:" + outcomeKey outcome)
        | ArtifactContentFact (artifact, _, _) -> FactId("artifact-content:" + artifactKey artifact)
        | EvidenceStateFact (node, _) -> FactId("evidence-state:" + node)
        | EvidenceDependencyFact (dependent, dependency) -> FactId("evidence-dependency:" + dependent + "->" + dependency)
        | FreshnessFact (node, _, _) -> FactId("freshness:" + node)

    let hash (content: string) =
        use sha = SHA256.Create()
        sha.ComputeHash(Encoding.UTF8.GetBytes content)
        |> Array.map (fun b -> b.ToString "x2")
        |> String.concat ""

    let bridge (judge: JudgeId) : Bridge<ProjectFact> =
        { Judge = judge
          ArtifactHash =
            fun facts artifact ->
                facts
                |> List.tryPick (fun fact ->
                    match fact.Value with
                    | ArtifactContentFact (a, h, _) when a = artifact -> Some h
                    | _ -> None)
                |> Option.defaultValue ""
          Embed = GovernanceFact
          Project =
            function
            | GovernanceFact outcome -> Some outcome
            | _ -> None }

    let compose (options: ProjectOptions) : Composed<ProjectFact, ProjectChange> =
        let lifted =
            [ if options.Domains.Contains SpecKitDomain then
                  let adapter = SpecKitCatalog.adapter options.Judge options.SpecKitDial

                  Composition.lift
                      (fun fact -> match fact with | SpecKitProjectFact value -> Some value | _ -> None)
                      (fun change ->
                          change.SpecKit
                          |> Option.defaultValue
                              { Phase = FS.GG.Governance.Adapters.SpecKit.Phase.Implement
                                Surfaces = Set.empty })
                      adapter

              if options.Domains.Contains DesignSystemDomain then
                  let adapter = DesignCatalog.adapter options.Judge

                  Composition.lift
                      (fun fact -> match fact with | DesignSystemProjectFact value -> Some value | _ -> None)
                      (fun change ->
                          change.DesignSystem
                          |> Option.defaultValue { Surfaces = Set.empty })
                      adapter ]

        Composition.compose lifted []

    let senseArtifact (artifact: ArtifactRef) (content: string) : ProjectFact =
        ArtifactContentFact(artifact, hash content, content)

    let readContent (facts: FactSet<ProjectFact>) (artifact: ArtifactRef) : string option =
        facts
        |> List.tryPick (fun fact ->
            match fact.Value with
            | ArtifactContentFact (a, _, content) when a = artifact -> Some content
            | _ -> None)

    let toLoopConfig
        (options: ProjectOptions)
        (mode: RunMode)
        (snapshot: ProjectSnapshot)
        : LoopConfig<ProjectChange, ProjectFact> =
        ignore snapshot
        let composed = compose options

        { Identify = identify
          Rules = composed.Catalog
          Bridge = bridge options.Judge
          Fences = composed.Fences
          Mode = mode
          Policy = Loop.defaultPolicy
          SenseArtifact = senseArtifact
          ReadContent = readContent }

    let declaredEvidence (facts: FactSet<ProjectFact>) =
        facts
        |> List.choose (fun fact ->
            match fact.Value with
            | EvidenceStateFact (node, state) -> Some(node, state, "project")
            | SpecKitProjectFact (TaskState (taskId, state)) -> Some("speckit:" + taskId, state, "speckit")
            | DesignSystemProjectFact (MeasurementState (measurementId, state)) ->
                Some("design:" + measurementId, state, "design-system")
            | GovernanceFact (RuleOutcome.NeedsReview request) -> Some("review:" + request.Key, Pending, "review-cache")
            | GovernanceFact (RuleOutcome.Reviewed review) -> Some("review:" + review.Key, Real, "review-cache")
            | _ -> None)

    let evidenceDependencies (facts: FactSet<ProjectFact>) =
        facts
        |> List.choose (fun fact ->
            match fact.Value with
            | EvidenceDependencyFact (dependent, dependency) -> Some(dependent, dependency)
            | SpecKitProjectFact (TaskDependsOn (dependent, dependency)) ->
                Some("speckit:" + dependent, "speckit:" + dependency)
            | DesignSystemProjectFact (VerdictRestsOn (verdict, measurement)) ->
                Some("design:" + verdict, "design:" + measurement)
            | _ -> None)
        |> List.distinct
        |> List.sort

    let freshnessByNode (facts: FactSet<ProjectFact>) =
        facts
        |> List.choose (fun fact ->
            match fact.Value with
            | FreshnessFact (node, recorded, covered) -> Some(node, Freshness.decide recorded covered)
            | _ -> None)
        |> Map.ofList

    let evidenceReport (host: FS.GG.Governance.Host.Model<ProjectFact>) : ProjectEvidenceReport =
        let declared =
            declaredEvidence host.Facts
            |> List.groupBy (fun (id, _, _) -> id)
            |> List.map (fun (id, entries) ->
                let _, state, source = entries |> List.last
                id, state, source)
            |> List.sortBy (fun (id, _, _) -> id)

        let dependencies = evidenceDependencies host.Facts

        let effective =
            match Evidence.build (declared |> List.map (fun (id, state, _) -> id, state)) dependencies with
            | Ok graph -> Evidence.effective graph
            | Error _ -> Map.empty

        let freshness = freshnessByNode host.Facts

        let nodes =
            declared
            |> List.map (fun (id, state, source) ->
                { Id = id
                  Declared = Some state
                  Effective = Map.tryFind id effective
                  Freshness = Map.tryFind id freshness
                  Source = source })

        { Nodes = nodes
          Dependencies = dependencies
          Disclosures = host.Disclosures |> List.sort
          Failures = host.Failures |> List.sort }
