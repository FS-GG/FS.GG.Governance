// The pure release-gate result vocabulary (F053). Visibility lives in Model.fsi (Constitution Principle
// II); this file carries NO top-level access modifiers. These are data declarations only — no behavior,
// so nothing is stubbed. They REUSE F014 `Maturity`/`SurfaceId`, F023 `Severity`/`EnforcementDecision`,
// and F024 `Verdict`/`ExitCodeBasis` verbatim (research D1/D5/D6); no F014/F023/F024 type is redefined.

namespace FS.GG.Governance.ReleaseRules

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type ReleaseRuleKind =
        | VersionBump
        | PackageMetadata
        | TemplatePins
        | PublishPlan
        | TrustedPublishing
        | Provenance

    type FactState =
        | Met
        | Unmet
        | Unrecoverable

    type ReleaseRule =
        { Kind: ReleaseRuleKind
          Surface: SurfaceId
          BaseSeverity: Severity
          Maturity: Maturity }

    type ReleaseFacts = { States: Map<ReleaseRuleKind, FactState> }

    type RuleOutcome =
        | Satisfied
        | Violated

    type ReleaseFinding =
        { Kind: ReleaseRuleKind
          Surface: SurfaceId
          Outcome: RuleOutcome
          BaseSeverity: Severity
          Maturity: Maturity
          Reason: string }

    type EnforcedReleaseFinding =
        { Finding: ReleaseFinding
          Decision: EnforcementDecision }

    type ReleaseDecision =
        { Verdict: Verdict
          Blockers: EnforcedReleaseFinding list
          Warnings: EnforcedReleaseFinding list
          Passing: EnforcedReleaseFinding list
          ExitCodeBasis: ExitCodeBasis }
