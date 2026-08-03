namespace FS.GG.Governance.DesignChecks

/// Executable governance rules for evidence that crosses a product or tool boundary.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceBoundary =

    type EvidenceKind = SemanticRegression | BoundaryFixture | GoldenOrSchema | ProductionJourney
    type Provenance = Real | Synthetic of representedSeam: string
    type Observation = DispatchOnly | ObservedOutcome | MalformedInput | UnknownInput | PartialWrite | Degraded

    type EvidenceRecord =
        { Kind: EvidenceKind
          Provenance: Provenance
          Command: string
          ExitCode: int
          SourceDigest: string
          Fresh: bool
          Observation: Observation }

    type GeneratedArtifact =
        { Source: string option
          RegenerationDeterministic: bool
          Consumer: string option
          HasGoldenOrSchema: bool }

    type Mitigation =
        { ProducerClasses: string list
          ReintroducedByMutation: string list }

    type RenderEvidence =
        { Executed: bool
          ByteReproducible: bool
          SemanticReceiptStable: bool }

    type Request =
        { RequiresProductionJourney: bool
          RequiresObservedOutcome: bool
          Evidence: EvidenceRecord list
          GeneratedArtifacts: GeneratedArtifact list
          Mitigations: Mitigation list
          Render: RenderEvidence option }

    type Finding = { Code: string; Detail: string }

    /// Evaluate a complete evidence-boundary request deterministically. Missing or malformed facts never
    /// collapse into a clean verdict; a native-boundary obligation requires an observed outcome, not dispatch.
    val evaluate: request: Request -> Finding list
