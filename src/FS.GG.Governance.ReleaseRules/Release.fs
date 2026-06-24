// The pure, total release-gate evaluation + rollup (F053). Visibility lives in Release.fsi (Constitution
// Principle II); this file carries NO top-level access modifiers. The per-rule classifier, the reason
// builder, the `EnforcementInput` builder, and the three-way partition helper live ONLY here and are
// absent from Release.fsi (the Enforcement.fs / Ship.fs hidden-helper precedent).
//
// REUSES F023 `deriveEffectiveSeverity` for every per-finding decision (FR-003) and the F024 `Ship.Model`
// `Verdict`/`ExitCodeBasis` result types VERBATIM, re-applying the F024 partition rule — it does NOT call
// `Ship.rollup` (release rules are not a `RouteResult` — research D1) and edits NO frozen core (FR-009).
// PURE and TOTAL (FR-007): no I/O, no clock, never throws; byte-identical for identical input.

namespace FS.GG.Governance.ReleaseRules

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Release =

    // ── Pure-data lookups (total over the closed kind set; the story tests read them directly) ──

    let releaseRuleKindToken (kind: ReleaseRuleKind) : string =
        match kind with
        | VersionBump -> "versionBump"
        | PackageMetadata -> "packageMetadata"
        | TemplatePins -> "templatePins"
        | PublishPlan -> "publishPlan"
        | TrustedPublishing -> "trustedPublishing"
        | Provenance -> "provenance"

    let releaseRuleKindOrdinal (kind: ReleaseRuleKind) : int =
        match kind with
        | VersionBump -> 0
        | PackageMetadata -> 1
        | TemplatePins -> 2
        | PublishPlan -> 3
        | TrustedPublishing -> 4
        | Provenance -> 5

    let factFor (facts: ReleaseFacts) (kind: ReleaseRuleKind) : FactState =
        match Map.tryFind kind facts.States with
        | Some state -> state
        | None -> Unrecoverable

    // ── Hidden per-rule classifier + reason builder (research D7; absent from Release.fsi) ──

    /// The product-neutral, deterministic reason for one rule's outcome: the kind token, the governed
    /// surface id, and the outcome basis ("met" / "not met" / "no recoverable evidence"). No host paths,
    /// timestamps, or product vocabulary beyond the declared ids (research D7).
    let reasonFor (kind: ReleaseRuleKind) (surface: SurfaceId) (state: FactState) : string =
        let (SurfaceId surfaceId) = surface
        let token = releaseRuleKindToken kind

        let basis =
            match state with
            | Met -> "is met"
            | Unmet -> "is not met"
            | Unrecoverable -> "has no recoverable evidence"

        sprintf "release rule '%s' on surface '%s' %s" token surfaceId basis

    /// Classify one rule against its governing fact: `Met` ⇒ `Satisfied`; `Unmet`/`Unrecoverable` ⇒
    /// `Violated` (fail-safe, FR-005). Total over the closed `FactState`.
    let outcomeOf (state: FactState) : RuleOutcome =
        match state with
        | Met -> Satisfied
        | Unmet
        | Unrecoverable -> Violated

    // ── Hidden EnforcementInput builder + three-way partition (research D1/D2/D6; absent from Release.fsi) ──

    /// Build one finding's F023 `EnforcementInput` from its declared `BaseSeverity` + `Maturity`, fixed to
    /// `RunMode.Release` / `Profile.Release` (research D2/D6) — fed VERBATIM into `deriveEffectiveSeverity`.
    let inputFor (finding: ReleaseFinding) : EnforcementInput =
        { BaseSeverity = finding.BaseSeverity
          Maturity = finding.Maturity
          Mode = RunMode.Release
          Profile = Profile.Release }

    /// Re-apply the F024 partition rule, gated by the finding's outcome (research D1). Returns the bucket
    /// index: 0 = Blockers, 1 = Warnings, 2 = Passing. A `Satisfied` finding is never a concern (⇒ Passing);
    /// a `Violated` finding follows the F024 (base, effective) classification. Total; exhaustive match.
    let bucketOf (item: EnforcedReleaseFinding) : int =
        match item.Finding.Outcome with
        | Satisfied -> 2 // a satisfied rule is never a concern
        | Violated ->
            match item.Decision.EffectiveSeverity, item.Decision.BaseSeverity with
            | Blocking, _ -> 0 // effective blocking ⇒ blocker
            | Advisory, Blocking -> 1 // base blocking relaxed to advisory ⇒ visible warning
            | Advisory, Advisory -> 2 // base advisory, never escalated ⇒ visible in passing

    // ── The pure functions ──

    let evaluate (rules: ReleaseRule list) (facts: ReleaseFacts) : ReleaseFinding list =
        rules
        |> List.map (fun rule ->
            let state = factFor facts rule.Kind

            { Kind = rule.Kind
              Surface = rule.Surface
              Outcome = outcomeOf state
              BaseSeverity = rule.BaseSeverity
              Maturity = rule.Maturity
              Reason = reasonFor rule.Kind rule.Surface state })
        |> List.sortBy (fun f ->
            let (SurfaceId surfaceId) = f.Surface
            releaseRuleKindOrdinal f.Kind, surfaceId)

    let rollup (findings: ReleaseFinding list) : ReleaseDecision =
        // One enforced finding per finding — F023's decision carried verbatim, none dropped (FR-006).
        let enforced =
            findings
            |> List.map (fun finding ->
                { Finding = finding
                  Decision = deriveEffectiveSeverity (inputFor finding) })

        // Disjoint, exhaustive three-way partition; each list preserves the `evaluate` order (stable).
        let blockers = enforced |> List.filter (fun i -> bucketOf i = 0)
        let warnings = enforced |> List.filter (fun i -> bucketOf i = 1)
        let passing = enforced |> List.filter (fun i -> bucketOf i = 2)

        let verdict = if List.isEmpty blockers then Pass else Fail

        let exitCodeBasis =
            match verdict with
            | Pass -> Clean
            | Fail -> Blocked

        { Verdict = verdict
          Blockers = blockers
          Warnings = warnings
          Passing = passing
          ExitCodeBasis = exitCodeBasis }

    let evaluateRelease (rules: ReleaseRule list) (facts: ReleaseFacts) : ReleaseDecision =
        rollup (evaluate rules facts)
