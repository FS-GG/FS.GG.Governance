namespace FS.GG.Governance.Config

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CapabilityBindings =
    /// A stable neutral meaning; this declaration creates no organization floor.
    type SemanticCapability = { Id: string; Meaning: string; RequiresExecutable: bool }

    /// Ordered argv is passed literally; no command-string parsing is permitted.
    type BindingKind = SemanticOnly | Command of Fsgg.Provider.DeclaredCommand

    /// Explicit normalized limits. Missing input remains representable and refuses.
    type CommandLimits =
        { WorkingDirectory: Model.GovernedPath option
          Timeout: Model.TimeoutLimit option
          Cost: Model.Cost option
          Environments: Model.EnvironmentClass list }

    type CapabilityBinding =
        { CapabilityId: string
          Required: bool
          Platforms: string list
          ToolIds: string list
          EvidenceIds: string list
          Binding: BindingKind
          Limits: CommandLimits option }

    /// Exact version is a declaration, never an observation of the installed tool.
    type ToolBinding = { Id: string; ExactVersion: string }
    type EvidenceFormat = { Id: string; Version: string }
    type EvidenceBinding =
        { Id: string; Format: EvidenceFormat; OutputPath: Model.GovernedPath }

    /// Support and requirement lists belong to trusted caller policy, not provider data.
    type ResolutionRequest =
        { ContractVersion: string
          SelectedPlatform: string
          RequiredCapabilityIds: string list
          SemanticOnlyObligationIds: string list
          KnownPlatforms: string list
          SupportedEnvironments: Model.EnvironmentClass list
          SupportedEvidenceFormats: EvidenceFormat list
          Tools: ToolBinding list
          Evidence: EvidenceBinding list
          Bindings: CapabilityBinding list }

    type DiagnosticCode =
        | MalformedInput | DuplicateIdentity | MissingRequiredBinding
        | UnknownRequiredSemanticId | IllegalSemanticOnly | UnresolvedTool
        | UnresolvedEvidence | UnresolvedPlatform | UnsupportedEvidenceFormat
        | InvalidPath | InvalidExecutionLimit | UnsupportedContractVersion

    type Diagnostic =
        { CapabilityId: string; Field: string; Code: DiagnosticCode; Message: string }

    /// Complete validated declarations only; neither execution nor evidence acceptance.
    type ResolvedSet =
        { Bindings: CapabilityBinding list
          Tools: ToolBinding list
          Evidence: EvidenceBinding list
          UnsupportedCapabilityIds: string list }

    type Resolution = Resolved of ResolvedSet | Rejected of Diagnostic list

    /// Exactly the eight ADR-0092 meanings, sorted by stable identity.
    val catalog: SemanticCapability list
    /// Pure exact-version declaration resolution, with no partial success on diagnostics.
    val resolve: request: ResolutionRequest -> Resolution
    /// Deterministic canonical semantic vocabulary with a trailing newline.
    val catalogJson: unit -> string
