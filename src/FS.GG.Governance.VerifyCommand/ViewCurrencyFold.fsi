// Curated public signature for the stale-generated-view (currency) verdict fold (076 Phase C seam,
// extracted from Loop.fs). This .fsi is the SOLE declaration of the module's public surface (Principle II);
// ViewCurrencyFold.fs carries NO top-level access modifiers. PURE over `Profile` + `CurrencyFinding list` +
// `ShipDecision` — host-`Model`-free, lifted verbatim from `Loop.viewCurrencyBlocks`/`foldViewCurrencyVerdict`/
// `viewCurrencyDetail`. Mirrors `SurfaceFold`: the fold reuses the EXISTING `deriveEffectiveSeverity` (through
// the leaf's `decisionOf`) — no new rule/severity (FR-007/FR-009); `findings = []` is the identity ⇒
// byte-identical verify.json (FR-004). Compiled BEFORE `Loop.fs`.

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.Enforcement.Enforcement   // Profile, EnforcementDecision
open FS.GG.Governance.Ship.Model                 // ShipDecision

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ViewCurrencyFold =

    /// True iff any currency finding is effective-Blocking at `RunMode.Verify` under the active profile, via
    /// the EXISTING `deriveEffectiveSeverity` (through the leaf's `decisionOf`). Reuse only — no new rule.
    val viewCurrencyBlocks:
        profile: Profile -> findings: FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding list -> bool

    /// Fold the stale-view findings into the (already-rolled/relocated) decision: a blocking finding flips
    /// Verdict/ExitCodeBasis to blocked; otherwise the identity. `findings = []` ⇒ byte-identical (FR-004).
    val foldViewCurrencyVerdict:
        profile: Profile ->
        findings: FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding list ->
        decision: ShipDecision ->
            ShipDecision

    /// Pair each finding with its `EnforcementDecision` (Verify run mode + active profile) for the additive
    /// `generatedViews` projection — carries both base + effective severity + the lever reason. The concrete
    /// return type is pinned here (C1): each finding paired with its Verify-mode `EnforcementDecision`.
    val viewCurrencyDetail:
        profile: Profile ->
        findings: FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding list ->
            (FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding * EnforcementDecision) list
