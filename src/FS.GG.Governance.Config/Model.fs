// Typed-fact model for the `.fsgg` schemas (F014). Visibility lives entirely in
// Model.fsi (Principle II): no top-level binding here carries an access modifier.
// These are plain records/DUs (Principle III) — the product-neutral, YAML-free values
// later Governance features consume (FR-010, SC-005).

namespace FS.GG.Governance.Config

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── Scalars & closed enumerations ──

    type SchemaVersion = SchemaVersion of int

    type GovernedPath = GovernedPath of string

    type Cost =
        | Cheap
        | Medium
        | High
        | Exhaustive

    type EnvironmentClass =
        | Local
        | Ci
        | LocalOrCi
        | Release

    type Maturity =
        | Observe
        | Warn
        | BlockOnPr
        | BlockOnShip
        | BlockOnRelease

    type SurfaceClass =
        // — MVP (F014, unchanged) —
        | Routine
        | GovernedRoot
        | ProtectedSurface
        | GeneratedView
        | ReleaseSurface
        // — F23 product kinds (capabilities.yml v2) —
        | PackageSurface
        | DocsSurface
        | SkillSurface
        | DesignSurface
        | SampleAppSurface
        | GeneratedProductRoot

    type GeneratedProductTier =
        | StructuralScan
        | RestoreBuild
        | FocusedTests
        | FullVerify
        | ReleaseValidation

    // ── Identity newtypes ──

    type ProjectId = ProjectId of string
    type DomainId = DomainId of string
    type ProfileId = ProfileId of string
    type SurfaceId = SurfaceId of string
    type CheckId = CheckId of string
    type CommandId = CommandId of string
    type Owner = Owner of string
    type TimeoutLimit = TimeoutLimit of seconds: int

    // ── F23 product-surface attributes (single-string newtypes) ──

    type EvidenceTag = EvidenceTag of string
    type TemplateProfile = TemplateProfile of string
    type Baseline = Baseline of string

    // ── project.yml ──

    type ProjectFacts =
        { SchemaVersion: SchemaVersion
          Id: ProjectId
          Domains: DomainId list
          GovernedRoot: GovernedPath
          PackageSurfaces: GovernedPath list
          PolicyRef: GovernedPath option
          CapabilitiesRef: GovernedPath option }

    // ── policy.yml (optional) ──

    type BranchPolicyDecl = { Pattern: string; RequirePr: bool }

    type ReviewBudgetDecl = { MaxReviews: int }

    type PolicyFacts =
        { SchemaVersion: SchemaVersion
          Profiles: ProfileId list
          DefaultProfile: ProfileId
          BranchPolicy: BranchPolicyDecl option
          ReviewBudget: ReviewBudgetDecl option }

    // ── capabilities.yml ──

    type PathMapEntry =
        { Glob: GovernedPath
          Capability: DomainId }

    type Surface =
        { Id: SurfaceId
          Class: SurfaceClass
          Paths: GovernedPath list
          Owner: Owner
          Maturity: Maturity
          EvidenceTag: EvidenceTag option
          TemplateProfile: TemplateProfile option
          Baseline: Baseline option }

    type Check =
        { Id: CheckId
          Domain: DomainId
          Command: CommandId option
          Owner: Owner
          Cost: Cost
          Environment: EnvironmentClass
          Maturity: Maturity
          Tier: GeneratedProductTier option }

    type CapabilityFacts =
        { SchemaVersion: SchemaVersion
          Domains: DomainId list
          PathMap: PathMapEntry list
          Surfaces: Surface list
          Checks: Check list }

    // ── tooling.yml (optional) ──

    type CommandSpec =
        { Id: CommandId
          Command: string
          Timeout: TimeoutLimit
          Environment: EnvironmentClass }

    type ExternalToolReq = { Tool: string; MinVersion: string }

    type ToolingFacts =
        { SchemaVersion: SchemaVersion
          Commands: CommandSpec list
          EnvironmentClasses: EnvironmentClass list
          ExternalTools: ExternalToolReq list }

    // ── The aggregate typed facts ──

    type TypedFacts =
        { Project: ProjectFacts
          Policy: PolicyFacts option
          Capabilities: CapabilityFacts
          Tooling: ToolingFacts option }

    // ── Diagnostics ──

    type FsggFile =
        | Project
        | Policy
        | Capabilities
        | Tooling

    type Locator =
        { Field: string option
          Id: string option
          Line: int option }

    type DiagnosticId =
        | UnknownField
        | MissingRequiredField
        | MalformedValue
        | DuplicateId
        | MissingSchemaVersion
        | MalformedSchemaVersion
        | UnsupportedSchemaVersion
        | PathEscapesRoot
        | DanglingReference
        | EmptyFile
        | MissingRequiredFile

    type Diagnostic =
        { Id: DiagnosticId
          File: FsggFile
          Locator: Locator
          Message: string }

    type Validation =
        | Valid of TypedFacts
        | Invalid of Diagnostic list

    // ── Stable rendering of a diagnostic id ──

    // Total, deterministic: every case maps to its lowerCamelCase wire token. A new
    // DiagnosticId case is a compile error here until it gets a token (closed set, D7).
    let diagnosticIdToken (id: DiagnosticId) : string =
        match id with
        | UnknownField -> "unknownField"
        | MissingRequiredField -> "missingRequiredField"
        | MalformedValue -> "malformedValue"
        | DuplicateId -> "duplicateId"
        | MissingSchemaVersion -> "missingSchemaVersion"
        | MalformedSchemaVersion -> "malformedSchemaVersion"
        | UnsupportedSchemaVersion -> "unsupportedSchemaVersion"
        | PathEscapesRoot -> "pathEscapesRoot"
        | DanglingReference -> "danglingReference"
        | EmptyFile -> "emptyFile"
        | MissingRequiredFile -> "missingRequiredFile"

    // Total, deterministic: every SurfaceClass maps to its YAML `kind` token. A new case is a
    // compile error here until it gets a token (closed set, single-sourced with Schema's parse, D2).
    let surfaceClassToken (cls: SurfaceClass) : string =
        match cls with
        | Routine -> "routine"
        | GovernedRoot -> "governedRoot"
        | ProtectedSurface -> "protected"
        | GeneratedView -> "generatedView"
        | ReleaseSurface -> "release"
        | PackageSurface -> "package"
        | DocsSurface -> "docs"
        | SkillSurface -> "skill"
        | DesignSurface -> "design"
        | SampleAppSurface -> "sampleApp"
        | GeneratedProductRoot -> "generatedProduct"

    // The closed order StructuralScan < RestoreBuild < FocusedTests < FullVerify < ReleaseValidation.
    let generatedProductTierRank (tier: GeneratedProductTier) : int =
        match tier with
        | StructuralScan -> 1
        | RestoreBuild -> 2
        | FocusedTests -> 3
        | FullVerify -> 4
        | ReleaseValidation -> 5

    let generatedProductTierToken (tier: GeneratedProductTier) : string =
        match tier with
        | StructuralScan -> "structuralScan"
        | RestoreBuild -> "restoreBuild"
        | FocusedTests -> "focusedTests"
        | FullVerify -> "fullVerify"
        | ReleaseValidation -> "releaseValidation"

    // ── Path normalization (single-sourced — F016 research D7) ──

    // Pure string logic (never Path.GetFullPath, research D5): unify separators, drop `.`/empty
    // segments, resolve `..` against the stack. TOTAL — an unpoppable `..` (root escape) is kept
    // as a literal `..` segment so out-of-root sensed paths are REPRESENTED, never dropped (F016
    // FR-002); F014's `Schema.validate` rejects escape by testing for a `..` segment in the result.
    let normalizePath (raw: string) : GovernedPath =
        let segments = raw.Split([| '/'; '\\' |])
        // mutable: local accumulator for the normalized segment stack (Principle III disclosure).
        let stack = System.Collections.Generic.List<string>()

        for seg in segments do
            if seg = "" || seg = "." then ()
            elif seg = ".." then
                if stack.Count > 0 && stack.[stack.Count - 1] <> ".." then
                    stack.RemoveAt(stack.Count - 1)
                else
                    stack.Add ".."
            else
                stack.Add seg

        let joined = System.String.Join("/", stack)
        GovernedPath(if joined = "" then "." else joined)
