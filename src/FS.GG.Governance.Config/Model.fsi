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

    /// The surface classification (FR-011, D6). `Routine` names an explicitly-declared
    /// unmanaged region; it is NEVER produced for undeclared files (those yield no surface
    /// fact at all — US3 scenario 3). The MVP cases (F014) are joined by the F23 product kinds
    /// (`capabilities.yml` schemaVersion 2): `package`/`docs`/`skill`/`design`/`sampleApp`/
    /// `generatedProduct` (data-model §1.1). The mapping to/from the YAML `kind` token is total and
    /// single-sourced in `Schema` (parse) + `surfaceClassToken` (render); an unknown token ⇒
    /// `MalformedValue` (FR-012). `PackageSurface`, `ReleaseSurface`, and `GeneratedProductRoot`
    /// are the protected boundaries the F017 escalating-boundary set widens to (FR-003).
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

    /// The declared depth of a cost-tiered generated-product check (FR-005, D4). A CLOSED, ORDERED
    /// class: `StructuralScan < RestoreBuild < FocusedTests < FullVerify < ReleaseValidation`. The
    /// ordering is given by `generatedProductTierRank` (1..5); the YAML token by
    /// `generatedProductTierToken`. Distinct from the generic, unchanged `Cost` class.
    type GeneratedProductTier =
        | StructuralScan
        | RestoreBuild
        | FocusedTests
        | FullVerify
        | ReleaseValidation

    // ── Identity newtypes (kept distinct so cross-references are type-checked) ──

    type ProjectId = ProjectId of string
    type DomainId = DomainId of string
    type ProfileId = ProfileId of string
    type SurfaceId = SurfaceId of string
    type CheckId = CheckId of string
    type CommandId = CommandId of string
    type Owner = Owner of string
    type TimeoutLimit = TimeoutLimit of seconds: int

    // ── F23 product-surface attributes (single-string newtypes, D3) ──

    /// A label tying a surface to its currency evidence; the per-domain check that produces the
    /// evidence is F24. A declared `EvidenceTag` without its check is a known, non-error state (FR-016).
    type EvidenceTag = EvidenceTag of string

    /// The template a `generatedProduct` root was instantiated from (meaningful on `GeneratedProductRoot`).
    type TemplateProfile = TemplateProfile of string

    /// The pin fixing a package surface; the drift check that compares against it is F24
    /// (meaningful on `PackageSurface`).
    type Baseline = Baseline of string

    // ── governance.yml ──

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
    /// scenario 2). The three F23 product attributes are OPTIONAL — all `None` for an MVP-shaped
    /// surface (a subset declaration is valid, not an error; data-model §1.4). They are parsed only
    /// under `capabilities.yml` v2; an unknown field still ⇒ `UnknownField`.
    type Surface =
        { Id: SurfaceId
          Class: SurfaceClass
          Paths: GovernedPath list
          Owner: Owner
          Maturity: Maturity
          // — F23 optional product attributes —
          EvidenceTag: EvidenceTag option
          TemplateProfile: TemplateProfile option
          Baseline: Baseline option }

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
          Maturity: Maturity
          // — F23 — present on cost-tiered generated-product checks (data-model §1.5) —
          Tier: GeneratedProductTier option }

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
        /// A PRESENT `.fsgg` file whose bytes could not be READ (an I/O error at the Loader edge), distinct
        /// from `EmptyFile` (present but blank). Carries the underlying read error in its message so the
        /// failure is diagnosable and never masquerades as an empty file or an absent optional file.
        | UnreadableFile
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

    /// The stable wire token for a `SurfaceClass` (e.g. `PackageSurface` → `"package"`). Total,
    /// deterministic, and single-sourced: a new `SurfaceClass` case is a compile error here until it
    /// gets a token (the closed-set discipline, D2). Mirrors the `Schema` parse mapping.
    val surfaceClassToken: cls: SurfaceClass -> string

    /// The rank of a `GeneratedProductTier` in its closed order, 1 (`StructuralScan`) .. 5
    /// (`ReleaseValidation`). Deterministic and total (D4).
    val generatedProductTierRank: tier: GeneratedProductTier -> int

    /// The stable wire token for a `GeneratedProductTier` (e.g. `FocusedTests` → `"focusedTests"`).
    /// Total and deterministic; a new case is a compile error here until it gets a token (D4).
    val generatedProductTierToken: tier: GeneratedProductTier -> string

    // ── Path normalization (single-sourced — F016 research D7) ──

    /// Normalize a raw declared/sensed path into the canonical `GovernedPath` form: separators
    /// unified (`/` and `\` → `/`), `.` and empty segments dropped, `..` resolved against the
    /// segment stack, kept relative to the governed root. Pure string logic (never
    /// `Path.GetFullPath`), so no absolute host path can leak (SC-002/SC-005). TOTAL: a `..` that
    /// would escape the root is RETAINED as a leading `..` segment rather than failing — callers
    /// that must reject escape (F014 `Schema.validate`) test for a `..` segment in the result;
    /// callers that must REPRESENT out-of-root paths (F016 git/CI sensing) keep them. The empty
    /// path normalizes to `"."`. This is the ONE implementation of the governed-path form, reused
    /// by F015 routing and F016 sensing so every `GovernedPath` is byte-identical.
    val normalizePath: raw: string -> GovernedPath
