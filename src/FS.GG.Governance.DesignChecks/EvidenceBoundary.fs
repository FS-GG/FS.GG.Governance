namespace FS.GG.Governance.DesignChecks

open System

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceBoundary =

    type EvidenceKind = SemanticRegression | BoundaryFixture | GoldenOrSchema | ProductionJourney
    type Provenance = Real | Synthetic of representedSeam: string
    type Observation = DispatchOnly | ObservedOutcome | MalformedInput | UnknownInput | PartialWrite | Degraded

    type EvidenceRecord =
        { Kind: EvidenceKind; Provenance: Provenance; Command: string; ExitCode: int; SourceDigest: string; Fresh: bool; Observation: Observation }

    type GeneratedArtifact =
        { Source: string option; RegenerationDeterministic: bool; Consumer: string option; HasGoldenOrSchema: bool }

    type Mitigation = { ProducerClasses: string list; ReintroducedByMutation: string list }
    type RenderEvidence = { Executed: bool; ByteReproducible: bool; SemanticReceiptStable: bool }
    type Request =
        { RequiresProductionJourney: bool; RequiresObservedOutcome: bool; Evidence: EvidenceRecord list
          GeneratedArtifacts: GeneratedArtifact list; Mitigations: Mitigation list; Render: RenderEvidence option }
    type Finding = { Code: string; Detail: string }

    let private finding code detail = { Code = code; Detail = detail }
    let private has kind evidence = evidence |> List.exists (fun item -> item.Kind = kind)
    let private complete item =
        String.IsNullOrWhiteSpace item.Command || String.IsNullOrWhiteSpace item.SourceDigest || item.ExitCode <> 0 || not item.Fresh

    let evaluate request =
        let required = [ SemanticRegression; BoundaryFixture; GoldenOrSchema ] @ if request.RequiresProductionJourney then [ ProductionJourney ] else []
        let missing = required |> List.choose (fun kind -> if has kind request.Evidence then None else Some(finding "evidence.kind-missing" (string kind)))
        let recordFindings =
            request.Evidence
            |> List.collect (fun item ->
                [ if complete item then finding "evidence.provenance-incomplete" (string item.Kind)
                  match item.Provenance with Synthetic seam when String.IsNullOrWhiteSpace seam -> finding "evidence.synthetic-undisclosed" (string item.Kind) | _ -> ()
                  match item.Observation with
                  | MalformedInput -> finding "evidence.malformed-input" (string item.Kind)
                  | UnknownInput -> finding "evidence.unknown-input" (string item.Kind)
                  | PartialWrite -> finding "evidence.partial-write" (string item.Kind)
                  | _ -> () ])
        let outcome =
            if request.RequiresObservedOutcome && request.Evidence |> List.exists (fun item -> item.Observation = DispatchOnly) then
                [ finding "evidence.dispatch-is-not-outcome" "native-boundary obligation" ] else []
        let artifacts =
            request.GeneratedArtifacts |> List.collect (fun artifact ->
                [ if Option.isNone artifact.Source then finding "evidence.generated-source-missing" "generated artifact"
                  if not artifact.RegenerationDeterministic then finding "evidence.generated-regeneration-nondeterministic" "generated artifact"
                  if Option.isNone artifact.Consumer then finding "evidence.generated-consumer-missing" "generated artifact"
                  if not artifact.HasGoldenOrSchema then finding "evidence.generated-compatibility-missing" "generated artifact" ])
        let mitigations =
            request.Mitigations |> List.collect (fun mitigation ->
                let declared = mitigation.ProducerClasses |> Set.ofList
                let mutated = mitigation.ReintroducedByMutation |> Set.ofList
                if Set.isEmpty declared then [ finding "evidence.producer-inventory-empty" "mitigation" ]
                elif declared <> mutated then [ finding "evidence.producer-mutation-incomplete" "each producer class requires its own reintroduction mutation" ]
                else [])
        let render =
            match request.Render with
            | None -> []
            | Some item when not item.Executed -> [ finding "evidence.render-not-executed" "render fixture" ]
            | Some item when not item.ByteReproducible && not item.SemanticReceiptStable -> [ finding "evidence.render-receipt-unstable" "byte identity is not promised, so semantic receipt must be stable" ]
            | _ -> []
        List.concat [ missing; recordFindings; outcome; artifacts; mitigations; render ] |> List.sortBy (fun item -> item.Code, item.Detail)
