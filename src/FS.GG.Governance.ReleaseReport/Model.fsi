// Curated public signature contract for the publication-report types (F26, P1).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO access modifiers. REUSES the upstream vocabulary verbatim (opened, never redefined):
// F024 `Verdict`/`ExitCodeBasis` (Ship), F053 `ReleaseDecision`/`ReleaseRuleKind`/`FactState` (ReleaseRules),
// F26 `PackEvidenceSet` (PackEvidence), `AttestationSummary` (Attestation). Carries the F53 decision VERBATIM
// — never re-derives a verdict (FR-012).

namespace FS.GG.Governance.ReleaseReport

open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// One publication precondition surfaced first-class (FR-006), projected from the F54 ReleaseSnapshot +
    /// the F53 finding. Product-neutral.
    type PreconditionEvidence =
        { Kind: ReleaseRuleKind
          State: FactState
          Reason: string }

    /// The whole publication boundary as one immutable, presentation-free value (FR-012). Carries the F53
    /// decision VERBATIM — never re-derives a verdict or exit-code basis. `Sensed` is the F54 SensedRelease
    /// carried VERBATIM so the report is the genuine single source of truth (FR-012, D3): the `release.json`
    /// v1 `rules`/`evidence` blocks render from it, and `Preconditions` is its first-class projection
    /// (the JSON v2 contract renders the v1 fields "from report.Decision + the sensed snapshot").
    type ReleaseReport =
        { Decision: ReleaseDecision
          Sensed: SensedRelease
          Package: PackEvidenceSet
          Preconditions: PreconditionEvidence list
          Attestation: AttestationSummary
          ReleaseExitCodeBasis: ExitCodeBasis }

    /// The ADVISORY projection fsgg verify surfaces (FR-005): the same evidence, explicitly non-blocking.
    type VerifyReleasePreview =
        { Verdict: Verdict
          Package: PackEvidenceSet
          Preconditions: PreconditionEvidence list
          Attestation: AttestationSummary
          Advisory: bool }
