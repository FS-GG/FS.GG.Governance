// The stale-generated-view (currency) verdict fold (076 Phase C seam). Visibility lives in
// ViewCurrencyFold.fsi (Principle II) — no top-level access modifiers here. PURE and host-`Model`-free:
// mirrors `SurfaceFold` over the currency vocabulary. Bindings lifted verbatim from `Loop.fs`; `Loop` now
// calls `ViewCurrencyFold.foldViewCurrencyVerdict`/`viewCurrencyDetail` at its projection sites, changing no
// emitted byte.

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.Enforcement.Enforcement // RunMode (Verify), Profile, Severity (Blocking), EnforcementDecision
open FS.GG.Governance.Ship.Model               // ShipDecision, Verdict (Fail), ExitCodeBasis

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement // F070: stale-view finding vocabulary + fold

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ViewCurrencyFold =

    // True iff any currency finding is effective-Blocking at `RunMode.Verify` under the active profile, via the
    // EXISTING `deriveEffectiveSeverity` (through the leaf's `decisionOf`). Reuse only — no new rule/severity.
    // A finding configured `block-on-ship`/`block-on-release` is effective-Advisory under verify (a warning,
    // FR-009); a `block-on-pr` finding blocks under verify only under a `strict`/`release` profile (C1).
    let viewCurrencyBlocks (profile: Profile) (findings: CE.CurrencyFinding list) : bool =
        findings
        |> List.exists (fun f -> (CE.decisionOf f Verify profile).EffectiveSeverity = Blocking)

    let foldViewCurrencyVerdict
        (profile: Profile)
        (findings: CE.CurrencyFinding list)
        (decision: ShipDecision)
        : ShipDecision =
        if viewCurrencyBlocks profile findings then
            { decision with
                Verdict = Fail
                ExitCodeBasis = ExitCodeBasis.Blocked }
        else
            decision

    // F070: pair each finding with its EnforcementDecision (Verify run mode + active profile) for the
    // additive `generatedViews` projection — carries both base + effective severity + the lever reason.
    let viewCurrencyDetail (profile: Profile) (findings: CE.CurrencyFinding list) =
        findings |> List.map (fun f -> f, CE.decisionOf f Verify profile)
