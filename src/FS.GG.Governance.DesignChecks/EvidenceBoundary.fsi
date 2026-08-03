namespace FS.GG.Governance.DesignChecks

/// Executable governance rules for evidence that crosses a product or tool boundary.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceBoundary =

    type EvidenceKind = SemanticRegression | BoundaryFixture | GoldenOrSchema | ProductionJourney
    type Provenance = Real | Synthetic of representedSeam: string
    type Observation = DispatchOnly | ObservedOutcome | MalformedInput | UnknownInput | PartialWrite | Degraded

    type EvidenceRecord =
        { Subject: string
          Kind: EvidenceKind
          Provenance: Provenance
          Command: string
          ExitCode: int
          SourceDigest: string
          Fresh: bool
          Observation: Observation }

    type GeneratedArtifact =
        { Path: string
          Source: string option
          RegenerationDeterministic: bool
          Consumer: string option
          HasGoldenOrSchema: bool }

    type Mitigation =
        { Claim: string
          ProducerClasses: string list
          ReintroducedByMutation: string list }

    type RenderEvidence =
        { Fixture: string
          Executed: bool
          ByteReproducible: bool
          SemanticReceiptStable: bool }

    type Request =
        { RequiresProductionJourney: bool
          RequiresObservedOutcome: bool
          Evidence: EvidenceRecord list
          GeneratedArtifacts: GeneratedArtifact list
          Mitigations: Mitigation list
          Render: RenderEvidence option }

    /// One deterministic, actionable unsatisfied evidence fact.
    type Finding = { Code: string; Subject: string; Correction: string }

    /// Evaluate a complete evidence-boundary request deterministically. Missing or malformed facts never
    /// collapse into a clean verdict; a native-boundary obligation requires an observed outcome, not dispatch.
    val evaluate: request: Request -> Finding list
