namespace FS.GG.Governance.DesignChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpSurface =

    /// A declaration exported by a curated F# signature, with the documentation observed on that contract.
    type SignatureDeclaration =
        { Name: string
          HasXmlDocumentation: bool }

    /// The machine-checked posture of a narrow exception to the curated-signature requirement.
    type Exemption =
        | NoExemption
        | ActiveExemption of owner: string * rationale: string * reviewBy: string
        | InvalidExemption of reason: string

    /// Facts for one compiled F# implementation module. Paths are repo-relative and compile indexes are the
    /// order from the owning project file; a signature must immediately precede its implementation.
    type ModuleFacts =
        { Project: string
          Source: GovernedPath
          Signature: GovernedPath option
          SourceCompileIndex: int
          SignatureCompileIndex: int option
          IsTestProject: bool
          IsExplicitlyInternal: bool
          IsEntryPoint: bool
          IsGenerated: bool
          Exemption: Exemption
          Declarations: SignatureDeclaration list
          SignatureMatchesSource: bool
          RequiresSurfaceBaseline: bool
          SurfaceBaselineCurrent: bool }

    /// Versioned, deterministic evidence for a single F# project evaluation.  `Malformed` is explicit so
    /// callers can fail closed instead of treating an unreadable project as a clean non-applicable surface.
    type ReceiptFinding =
        { Code: string; File: string; Detail: string; IsInputState: bool
          BaseSeverity: string; EffectiveSeverity: string; Evidence: string option }
    type Receipt =
        { SchemaVersion: int
          Kind: string
          Applicability: string
          Applicable: bool
          ApplicabilityReason: string
          Project: string
          DeclaredGlob: string
          CompiledSources: string list
          MatchedModules: string list
          MatchedModuleCount: int
          Cardinality: string
          Maturity: string
          Findings: ReceiptFinding list
          FreshnessDigest: string option
          ConfigDigest: string option
          PolicyDigest: string option
          SourceDigest: string option
          Malformed: string option }

    /// Read one SDK-style F# project and produce the ordered, compiled-module facts used by the policy.
    /// The sensor is deliberately conservative: unreadable project/source/signature input is an error, and
    /// a signature/source pairing is considered matching only when both files are present and the owning
    /// project builds with its real earlier sources, references, defines, options, and adjacent order.
    /// Callers supply package-baseline facts, so executable projects do not
    /// accidentally acquire a package requirement.
    val senseProject:
        root: string ->
        project: string ->
        isTestProject: bool ->
        requiresSurfaceBaseline: bool ->
        surfaceBaselineCurrent: bool ->
            Result<ModuleFacts list, string>

    /// Build deterministic, fail-closed evidence from the live project sensor and the same pure evaluator
    /// used by the reusable profile.  The digest covers the project and every compiled source/signature.
    val receipt:
        root: string ->
        project: string ->
        isTestProject: bool ->
        requiresSurfaceBaseline: bool ->
        surfaceBaselineCurrent: bool ->
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
            Receipt

    /// Render the stable `fsharp-public-surface/v1` JSON contract with a fixed property and array order.
    val receiptJson: receipt: Receipt -> string

    /// Evaluate curated F# public-surface hygiene for modules supplied by a project sensor. Findings are
    /// advisory while the dated migration posture is active and identify the exact project and module.
    val evaluate:
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
        modules: ModuleFacts list ->
            FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list
