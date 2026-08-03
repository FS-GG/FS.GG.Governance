namespace FS.GG.Governance.DesignChecks

open System

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceBoundary =

    type EvidenceKind = SemanticRegression | BoundaryFixture | GoldenOrSchema | ProductionJourney
    type Provenance = Real | Synthetic of representedSeam: string
    type Observation = DispatchOnly | ObservedOutcome | MalformedInput | UnknownInput | PartialWrite | Degraded

    type EvidenceRecord =
        { Subject: string; Kind: EvidenceKind; Provenance: Provenance; Command: string; ExitCode: int; SourceDigest: string; Fresh: bool; Observation: Observation }

    type GeneratedArtifact =
        { Path: string; Source: string option; RegenerationDeterministic: bool; Consumer: string option; HasGoldenOrSchema: bool }

    type Mitigation = { Claim: string; ProducerClasses: string list; ReintroducedByMutation: string list }
    type RenderEvidence = { Fixture: string; Executed: bool; ByteReproducible: bool; SemanticReceiptStable: bool }
    type Request =
        { RequiresProductionJourney: bool; RequiresObservedOutcome: bool; OptionalIntegrations: string list; Evidence: EvidenceRecord list
          GeneratedArtifacts: GeneratedArtifact list; Mitigations: Mitigation list; Render: RenderEvidence option }
    type Finding = { Code: string; Subject: string; Correction: string }

    let private finding code subject correction = { Code = code; Subject = subject; Correction = correction }
    let private hasReal kind evidence = evidence |> List.exists (fun item -> item.Kind = kind && item.Provenance = Real)
    let private provenanceComplete (item: EvidenceRecord) = String.IsNullOrWhiteSpace item.Subject || String.IsNullOrWhiteSpace item.Command || String.IsNullOrWhiteSpace item.SourceDigest || not item.Fresh
    let private expectedExit (item: EvidenceRecord) = item.Observation = MalformedInput && item.ExitCode <> 0

    let evaluate request =
        let required = [ SemanticRegression; BoundaryFixture; GoldenOrSchema ] @ if request.RequiresProductionJourney then [ ProductionJourney ] else []
        let missing = required |> List.choose (fun kind -> if hasReal kind request.Evidence then None else Some(finding "evidence.real-boundary-required" (string kind) "record real evidence for this required class"))
        let recordFindings =
            request.Evidence
            |> List.collect (fun item ->
                [ if provenanceComplete item then finding "evidence.provenance-incomplete" item.Subject "supply subject, command, source digest, and fresh receipt"
                  if item.ExitCode <> 0 && not (expectedExit item) then finding "evidence.command-failed" item.Subject "record a successful evidence command or classify an explicit malformed-input failure"
                  match item.Provenance with Synthetic seam when String.IsNullOrWhiteSpace seam -> finding "evidence.synthetic-undisclosed" item.Subject "name the production seam represented by this synthetic record" | _ -> ()
                  match item.Observation with
                  | UnknownInput -> finding "evidence.unknown-input" item.Subject "return an explicit no-verdict diagnostic instead of success"
                  | PartialWrite -> finding "evidence.partial-write" item.Subject "report partial completion and preserve a non-success verdict"
                  | _ -> () ])
        let outcome =
            if request.RequiresObservedOutcome && not (request.Evidence |> List.exists (fun item -> item.Provenance = Real && item.Observation = ObservedOutcome)) then
                [ finding "evidence.observed-outcome-missing" "native-boundary obligation" "execute a real boundary fixture and record its observed outcome" ] else []
        let optionalIntegrations =
            request.OptionalIntegrations
            |> List.choose (fun subject ->
                let hasOutcome =
                    request.Evidence
                    |> List.exists (fun item ->
                        item.Subject = subject
                        && item.Provenance = Real
                        && (item.Observation = Degraded || item.Observation = ObservedOutcome))
                if hasOutcome then None
                else Some(finding "evidence.optional-outcome-missing" subject "record an explicit degraded or observed outcome for this optional integration"))
        let artifacts =
            request.GeneratedArtifacts |> List.collect (fun artifact ->
                [ if Option.isNone artifact.Source then finding "evidence.generated-source-missing" artifact.Path "name the authored source that generates this artifact"
                  if not artifact.RegenerationDeterministic then finding "evidence.generated-regeneration-nondeterministic" artifact.Path "run regeneration and compare deterministic output"
                  if Option.isNone artifact.Consumer then finding "evidence.generated-consumer-missing" artifact.Path "name and execute a consuming route"
                  if not artifact.HasGoldenOrSchema then finding "evidence.generated-compatibility-missing" artifact.Path "add a golden or schema compatibility check" ])
        let mitigations =
            request.Mitigations |> List.collect (fun mitigation ->
                let declared = mitigation.ProducerClasses |> Set.ofList
                let mutated = mitigation.ReintroducedByMutation |> Set.ofList
                if Set.isEmpty declared then [ finding "evidence.producer-inventory-empty" mitigation.Claim "inventory every constructor or producer class" ]
                elif declared <> mutated then [ finding "evidence.producer-mutation-incomplete" mitigation.Claim "reintroduce each inventoried producer class in its own mutation" ]
                else [])
        let render =
            match request.Render with
            | None -> []
            | Some item when not item.Executed -> [ finding "evidence.render-not-executed" item.Fixture "compile and run the declared render fixture in the gate" ]
            | Some item when not item.ByteReproducible && not item.SemanticReceiptStable -> [ finding "evidence.render-receipt-unstable" item.Fixture "persist and compare a stable semantic receipt when bytes are not reproducible" ]
            | _ -> []
        List.concat [ missing; recordFindings; outcome; optionalIntegrations; artifacts; mitigations; render ] |> List.sortBy (fun item -> item.Code, item.Subject, item.Correction)
