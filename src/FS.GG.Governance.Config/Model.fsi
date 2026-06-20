// Curated public signature contract for the typed-fact model of the `.fsgg` schemas (F014).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Model.fs carries NO `private`/`internal`/`public` modifiers
// on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Model.fs body exists (Principle I). It is the product-
// neutral, YAML-free typed result of validating the four `.fsgg` files (FR-010, SC-005):
// the values later Governance features (git/CI sensing, routing, the gate registry, ship)
// consume. No field carries raw YAML text or product-specific vocabulary the schemas do not
// define. Every collection is emitted in deterministic, id/path-sorted order (FR-012,
// SC-002). This module performs NO I/O and is referenced by no kernel code — the kernel
// receives only typed facts and never sees this YAML/config vocabulary.

namespace FS.GG.Governance.Config

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── Scalars & closed enumerations ──

    /// The explicit, validated version stamp present on every `.fsgg` file (FR-001, FR-007).
    /// The MVP supports exactly one version; `Schema.validate` rejects missing/malformed/
    /// too-new with the matching diagnostic (D7).
    type SchemaVersion = SchemaVersion of int

    /// A path declared in a `.fsgg` file after deterministic normalization (FR-008, D5):
    /// separators unified, `.`/`..` resolved, kept relative to the governed root. Never an
    /// absolute host path (SC-002/SC-005). Used for governed roots, package surfaces, surface
    /// paths, and path-map globs.
    type GovernedPath = GovernedPath of string

    /// Closed cost class for a check/capability (FR-004). Ordering is `Cheap < Medium < High
    /// < Exhaustive`.
    type Cost =
        | Cheap
        | Medium
        | High
        | Exhaustive

    /// Closed environment class a check/command runs in (FR-004, FR-005).
    type EnvironmentClass =
        | Local
        | Ci
        | LocalOrCi
        | Release

    /// Closed rule-maturity level (FR-004). Declared here; effective enforcement is Phase 5.
    type Maturity =
        | Observe
        | Warn
        | BlockOnPr
        | BlockOnShip
        | BlockOnRelease

    /// The MVP surface classification (FR-011, D6). `Routine` names an explicitly-declared
    /// unmanaged region; it is NEVER produced for undeclared files (those yield no surface
    /// fact at all — US3 scenario 3).
    type SurfaceClass =
        | Routine
        | GovernedRoot
        | ProtectedSurface
        | GeneratedView
        | ReleaseSurface

    // ── Identity newtypes (kept distinct so cross-references are type-checked) ──

    type ProjectId = ProjectId of string
    type DomainId = DomainId of string
    type ProfileId = ProfileId of string
    type SurfaceId = SurfaceId of string
    type CheckId = CheckId of string
    type CommandId = CommandId of string
    type Owner = Owner of string
    type TimeoutLimit = TimeoutLimit of seconds: int

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

    /// Declared-but-not-enforced branch-policy placeholder (FR-003). Parsed and validated;
    /// no enforcement is computed in this feature.
    type BranchPolicyDecl = { Pattern: string; RequirePr: bool }

    /// Declared-but-not-enforced review-budget placeholder (FR-003).
    type ReviewBudgetDecl = { MaxReviews: int }

    type PolicyFacts =
        { SchemaVersion: SchemaVersion
          Profiles: ProfileId list
          DefaultProfile: ProfileId
          BranchPolicy: BranchPolicyDecl option
          ReviewBudget: ReviewBudgetDecl option }

    // ── capabilities.yml ──

    /// One path-pattern → capability-domain binding (FR-004). `Glob` is normalized and kept
    /// within the governed root; `Capability` is dangling-checked against declared domains.
    type PathMapEntry =
        { Glob: GovernedPath
          Capability: DomainId }

    /// A classified region of the tree (FR-011). `Owner`/`Maturity` are preserved (US3
    /// scenario 2).
    type Surface =
        { Id: SurfaceId
          Class: SurfaceClass
          Paths: GovernedPath list
          Owner: Owner
          Maturity: Maturity }

    /// A declared verification associated with a capability domain (FR-004). `Command`, when
    /// present, references a declared `tooling.yml` command (cross-file, FR-009). Carries the
    /// full per-entry metadata FR-004 requires.
    type Check =
        { Id: CheckId
          Domain: DomainId
          Command: CommandId option
          Owner: Owner
          Cost: Cost
          Environment: EnvironmentClass
          Maturity: Maturity }

    type CapabilityFacts =
        { SchemaVersion: SchemaVersion
          Domains: DomainId list
          PathMap: PathMapEntry list
          Surfaces: Surface list
          Checks: Check list }

    // ── tooling.yml (optional) ──

    /// One allow-listed command with its per-command timeout and environment class (FR-005).
    /// `Id` is the cross-file reference target for `Check.Command`.
    type CommandSpec =
        { Id: CommandId
          Command: string
          Timeout: TimeoutLimit
          Environment: EnvironmentClass }

    /// An external tool/version expectation (FR-005).
    type ExternalToolReq = { Tool: string; MinVersion: string }

    type ToolingFacts =
        { SchemaVersion: SchemaVersion
          Commands: CommandSpec list
          EnvironmentClasses: EnvironmentClass list
          ExternalTools: ExternalToolReq list }

    // ── The aggregate typed facts ──

    /// The product-neutral aggregate handed to later features (FR-010). Optional files are
    /// `None` when ABSENT (never when present-but-invalid — that makes the whole result
    /// `Invalid`, FR-015).
    type TypedFacts =
        { Project: ProjectFacts
          Policy: PolicyFacts option
          Capabilities: CapabilityFacts
          Tooling: ToolingFacts option }

    // ── Diagnostics (FR-013, D7) ──

    /// Which of the four files a diagnostic concerns.
    type FsggFile =
        | Project
        | Policy
        | Capabilities
        | Tooling

    /// Where in a file a diagnostic points (best available): a dotted field path, an offending
    /// id, and/or a 1-based line. `Field`/`Id` are `None` when not applicable.
    type Locator =
        { Field: string option
          Id: string option
          Line: int option }

    /// The CLOSED set of stable diagnostic ids — one per malformed class named in the spec
    /// (SC-003). The set is closed so tests assert exactly one fixture per id.
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

    /// A stable-id, located, explained record of why a declaration was rejected (FR-013).
    type Diagnostic =
        { Id: DiagnosticId
          File: FsggFile
          Locator: Locator
          Message: string }

    /// The single result of validation (FR-006): typed facts on success, or a non-empty,
    /// deterministically-ordered diagnostic list on failure — NEVER partial facts on failure.
    type Validation =
        | Valid of TypedFacts
        | Invalid of Diagnostic list

    // ── Stable rendering of a diagnostic id (for messages, tests, and any later JSON) ──

    /// The stable wire token for a `DiagnosticId` (e.g. `DuplicateId` → `"duplicateId"`).
    /// Deterministic and total.
    val diagnosticIdToken: id: DiagnosticId -> string
