namespace FS.GG.Governance.CodeChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type SourceDocument =
        { Path: string
          Source: string
          IsGenerated: bool
          /// Assembly paths supplied by the caller for this document's project reference set.
          /// The analyzer consumes these as compiler options and never discovers them from disk.
          References: string list }

    type ReviewThresholds =
        { ModuleLines: int option
          TypeLines: int option
          MemberLines: int option
          DependencyFanOut: int option }

    type JustificationReason =
        | Measured
        | Interoperability

    type ComplexityJustification =
        { Path: string
          Symbol: string
          Head: string
          SourceDigest: string
          SimplerAlternative: string
          Reason: JustificationReason
          Evidence: string }

    type ApprovedPrimitive =
        { Capability: string
          ApprovedSymbols: string list
          CandidateSymbols: string list }

    type AnalysisRequest =
        { Head: string
          Documents: SourceDocument list
          PureDomainPrefixes: string list
          Thresholds: ReviewThresholds
          Justifications: ComplexityJustification list
          ApprovedPrimitives: ApprovedPrimitive list }

    type FindingCategory =
        | ProhibitedStructure
        | ComplexityRequiresJustification

    type FindingId =
        | InheritanceHierarchy
        | AbstractClassHierarchy
        | ReflectionOrMetaprogramming
        | SharedMutableState
        | PublicClassShape
        | ImperativeInPureDomain
        | ModuleSizeReview
        | TypeSizeReview
        | MemberSizeReview
        | DependencyFanOutReview
        | DuplicateHomeGrownAbstraction
        | CompilerAnalysisFailed

    type SourceRange =
        { StartLine: int
          StartColumn: int
          EndLine: int
          EndColumn: int }

    type JustificationDisposition =
        | NotApplicable
        | Missing
        | Invalid
        | StaleHead
        | StaleSource

    type ArchitectureFinding =
        { Id: FindingId
          Category: FindingCategory
          Path: string
          Symbol: string
          Range: SourceRange
          Justification: JustificationDisposition
          Message: string }

    type AnalysisReport =
        { Findings: ArchitectureFinding list
          Diagnostics: string list }

    val findingIdToken: FindingId -> string
    val categoryToken: FindingCategory -> string
