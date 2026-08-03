namespace FS.GG.Governance.CodeChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type SourceDocument =
        { Path: string
          Source: string
          IsGenerated: bool }

    type ReviewThresholds =
        { ModuleLines: int option
          TypeLines: int option
          MemberLines: int option
          DependencyFanOut: int option }

    type JustificationReason = Measured | Interoperability

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

    type FindingCategory = ProhibitedStructure | ComplexityRequiresJustification

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

    type JustificationDisposition = NotApplicable | Missing | Invalid | StaleHead | StaleSource

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

    let findingIdToken = function
        | InheritanceHierarchy -> "inheritance-hierarchy"
        | AbstractClassHierarchy -> "abstract-class-hierarchy"
        | ReflectionOrMetaprogramming -> "reflection-or-metaprogramming"
        | SharedMutableState -> "shared-mutable-state"
        | PublicClassShape -> "public-class-shape"
        | ImperativeInPureDomain -> "imperative-in-pure-domain"
        | ModuleSizeReview -> "module-size-review"
        | TypeSizeReview -> "type-size-review"
        | MemberSizeReview -> "member-size-review"
        | DependencyFanOutReview -> "dependency-fan-out-review"
        | DuplicateHomeGrownAbstraction -> "duplicate-home-grown-abstraction"
        | CompilerAnalysisFailed -> "compiler-analysis-failed"

    let categoryToken = function
        | ProhibitedStructure -> "prohibited-structure"
        | ComplexityRequiresJustification -> "complexity-requires-justification"
