// Curated public signature contract for the F070 stale-generated-view finding vocabulary (the analogue of
// FS.GG.Governance.SurfaceChecks.Model).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// CurrencyEnforcement.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — the
// `currencyComparand` filler is hidden by its ABSENCE here.
//
// PURE and TOTAL: no I/O, no clock, no git, no environment, never throws, byte-identical for identical input.
// `decideCurrency` re-expresses the F057 per-view currency decision over the F029 `FreshnessKey.matches`/`diff`
// comparator VERBATIM (recorded vs sensed FreshnessInputs differing only in the source-digest set + generator
// version, revisions held equal — research D1/D4); `findingsOf` is the opt-in gate (D5); `enforcementInputOf`/
// `decisionOf` fold through the existing F023 `deriveEffectiveSeverity` (reuse only — NO new severity, run
// mode, profile, maturity value, or truth-table branch — FR-003).

namespace FS.GG.Governance.CurrencyEnforcement

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.Enforcement.Enforcement

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CurrencyEnforcement =

    /// Why a generated view is stale, for the self-describing finding (FR-005). Closed; product-neutral.
    type StaleCause =
        /// Source-digest mismatch / older-than-sources: the drifted input categories that drove staleness,
        /// carried verbatim from the WouldRegenerate/Regenerated CurrencyStatus (reuses F029 InputCategory).
        | SourceDrift of drifted: InputCategory list
        /// Currency could not be determined (no declared sources, missing manifest entry, unreadable lock).
        /// Carries the StaleUnresolved reason verbatim; NEVER coerced to Current (FR-008).
        | Undeterminable of reason: string

    /// One stale (or undeterminable) generated-view finding, ready to fold. `ViewId`/`Kind` name the stale
    /// view (FR-005); `Cause` names what makes it stale. `BaseSeverity` = Blocking (so the configured maturity
    /// can block — D5); `Maturity` is the configured dial. The pair (BaseSeverity, Maturity) feeds the F023
    /// truth table verbatim — no new enforcement constant.
    type CurrencyFinding =
        { ViewId: string
          Kind: ViewKind
          Cause: StaleCause
          BaseSeverity: Severity
          Maturity: Maturity }

    /// Decide one view's currency by reusing F029 `FreshnessKey.diff` VERBATIM (recorded vs sensed
    /// FreshnessInputs differing only in the source-digest set + generator version, revisions held equal —
    /// research D1/D4). Produces the existing `RefreshModel.ViewDecision`/`CurrencyStatus`. PURE — the edge
    /// supplies the recorded provenance and the freshly-sensed digests/version as data; this never reads the
    /// filesystem. A sense failure (`Error`) yields `StaleUnresolved` (never `Current` — FR-008); a missing
    /// recorded provenance (`None`) is likewise `StaleUnresolved`, never silently passed.
    val decideCurrency:
        entry: GenerationEntry ->
        recorded: (ArtifactHash list * GeneratorVersion) option ->
        sensed: Result<ArtifactHash list * GeneratorVersion, string> ->
            ViewDecision

    /// The OPT-IN gate (D5). `None` ⇒ `[]` (unconfigured ⇒ byte-identity). `Some m` ⇒ one finding per
    /// stale/unresolved view (`Current`/`NotEvaluated` ⇒ none), `BaseSeverity = Blocking`, `Maturity = m`, in
    /// declared manifest order. TOTAL.
    val findingsOf: maturity: Maturity option -> views: ViewDecision list -> CurrencyFinding list

    /// The fail-closed finding for a `refresh.yml` that is PRESENT but unreadable/unparseable: `Undeterminable`
    /// (carrying the read/parse `reason`), `BaseSeverity = Blocking`, at the STRICTEST maturity (`BlockOnPr`)
    /// so a corrupt manifest blocks from PR onward. The configured dial is unknowable when the file cannot be
    /// read, so this is NOT gated by `findingsOf` — it must never silently pass (FR-008). Emitted by the
    /// CurrencySensing edge (which distinguishes an ABSENT manifest, `[]`, from a present-but-unreadable one).
    val manifestUnreadableFinding: reason: string -> CurrencyFinding

    /// Build the F023 rollup input for a finding under a run mode + profile (reuses F023 verbatim — NO
    /// truth-table logic here). Mirrors `SurfaceChecks.Model.enforcementInputOf` exactly.
    val enforcementInputOf: finding: CurrencyFinding -> mode: RunMode -> profile: Profile -> EnforcementInput

    /// The per-finding enforcement decision = `deriveEffectiveSeverity (enforcementInputOf finding mode
    /// profile)`. Carries both base and effective severity + reason for the no-hide projection (FR-006).
    /// TOTAL, deterministic.
    val decisionOf: finding: CurrencyFinding -> mode: RunMode -> profile: Profile -> EnforcementDecision

    /// Stable, kebab-case wire token for a `StaleCause` discriminant (`source-drift` | `undeterminable`), for
    /// the `generatedViews` `cause` field. TOTAL, exhaustive (no wildcard).
    val staleCauseToken: cause: StaleCause -> string
