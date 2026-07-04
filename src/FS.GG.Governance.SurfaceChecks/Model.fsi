// The shared cross-domain finding vocabulary for F24 surface checks.
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// Every F24 domain pack (package/docs/skill/design) produces exactly this `SurfaceFinding` shape and consumes
// a `SurfaceCheckRequest` derived from one F23 `ProductClassification`. The vocabulary REUSES the F023
// `Severity`/`Maturity`/`RunMode`/`Profile`/`EnforcementInput` and the F014 `SurfaceId`/`SurfaceClass`/
// `EvidenceTag`/`GovernedPath` rather than redefining them — this row adds NO new `SurfaceClass`, schema
// field, or enforcement constant (FR-013, FR-014). `enforcementInputOf` builds the F023 input from a
// finding; the verdict is computed by the existing `deriveEffectiveSeverity` (reuse only).

namespace FS.GG.Governance.SurfaceChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// Which domain pack a request routes to. Closed; one case per F24 domain (FR-008).
    type CheckDomain =
        | PackageDomain
        | DocsDomain
        | SkillDomain
        | DesignDomain

    /// The precise locus a finding points at (FR-004/FR-006: name the exact thing).
    /// File is repo-relative, forward-slash normalized (normalizePath). Detail is the stable,
    /// domain-specific locus token (member name, transcript id, link target, entry id).
    type FindingLocation =
        { File: GovernedPath
          Detail: string }

    /// One deterministic-or-advisory finding from a surface check.
    /// BaseSeverity = Blocking for deterministic checks, Advisory for judgement-heavy (FR-011).
    /// EvidenceTag binds produced evidence back to the F23-declared tag (FR-009); None when the surface
    /// declared no tag (still a valid finding). IsInputState = true ⇒ missing/malformed input, not a rule
    /// violation and never a fabricated pass (FR-012). Message/Code/Detail are deterministic — no
    /// abs-path/clock/username (FR-010).
    type SurfaceFinding =
        { Domain: CheckDomain
          Surface: SurfaceId
          Code: string
          Location: FindingLocation
          BaseSeverity: Severity
          Maturity: Maturity
          EvidenceTag: EvidenceTag option
          IsInputState: bool
          Message: string }

    /// One unit of work derived from a single F23 ProductClassification (D4). The dispatcher builds one
    /// request per applicable routed surface and feeds it to the matching pack's evaluate. EvidenceTag is
    /// looked up from the surface declaration.
    type SurfaceCheckRequest =
        { Domain: CheckDomain
          Surface: SurfaceId
          Class: SurfaceClass
          Path: GovernedPath
          EvidenceTag: EvidenceTag option }

    /// Stable render token for a domain (token table, no clock/locale).
    val checkDomainToken: domain: CheckDomain -> string

    /// Stable sort ordinal for a domain (PackageDomain=0 … DesignDomain=3) — the Composition sort key.
    val checkDomainOrdinal: domain: CheckDomain -> int

    /// Stable render token for a severity (`advisory` | `blocking`).
    val severityToken: severity: Severity -> string

    /// Build the rollup input for a finding under a run mode + profile (reuses F023 verbatim — no
    /// truth-table logic here). The verdict is computed by the existing `deriveEffectiveSeverity`.
    val enforcementInputOf: finding: SurfaceFinding -> mode: RunMode -> profile: Profile -> EnforcementInput

    /// Build one finding bound to `request`'s surface + declared evidence tag, for the given `domain` and
    /// `maturity`. `source` is re-normalized to a repo-relative forward-slash `GovernedPath` (FR-010). The
    /// shared body the four *Checks packs used to hand-copy (111/A6) — each keeps a one-line wrapper binding
    /// its own domain / maturity / path-source.
    val mkFinding:
        domain: CheckDomain ->
        maturity: Maturity ->
        request: SurfaceCheckRequest ->
        source: GovernedPath ->
        code: string ->
        detail: string ->
        severity: Severity ->
        isInput: bool ->
        message: string ->
            SurfaceFinding

    /// Run a read, reifying BOTH an `Error` and a thrown exception into `Error` (`read threw: <msg>`) so a
    /// sensor never throws out of itself. Shared by the *Checks sense edges (111/A6).
    val safe: read: (unit -> Result<'a, string>) -> Result<'a, string>
