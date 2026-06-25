// Curated public signature contract for the packed-evidence operations (F26, P1).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Pack.fs carries NO access modifiers; the semantic-version comparator and the per-outcome reason/verdict
// helpers are ABSENT here (private by omission). All operations are PURE, TOTAL, DETERMINISTIC (FR-010): no
// clock/filesystem/git/process access, never throw, byte-identical for identical inputs regardless of input
// order.

namespace FS.GG.Governance.PackEvidence

open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.PackEvidence.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Pack =

    /// Compare a packed version against an optional baseline (the packed-version-is-truth rule, D1).
    /// None baseline ⇒ NoBaseline; None packed ⇒ NotPackable. Versions are compared by semantic-version
    /// precedence (numeric core segments numerically — `1.10.0 > 1.9.0`; build metadata ignored; a
    /// pre-release lower than its release). PURE, TOTAL, never throws.
    val versionPolicy: baseline: string option -> packed: string option -> VersionVerdict

    /// Build the deterministic PackEvidenceSet from the released baselines + the sensed pack outcomes.
    /// Verdicts sorted (SurfaceId, ArtifactPath); Runs preserved in carried order; empty outcomes ⇒
    /// NoPackableProjects = true (D6). PURE, TOTAL.
    val evaluatePack: baselines: Map<SurfaceId, string> -> outcomes: PackOutcome list -> PackEvidenceSet

    /// Derive the Met/Unmet contributions for the VersionBump / PackageMetadata / Provenance families from
    /// real pack output (merged over the F54 sensed facts at the edge — packed evidence wins on these three,
    /// D1). A Packed+Bumped (or Packed+NoBaseline) project keeps a family Met; an Unbumped/Downgraded marks
    /// VersionBump Unmet; a PackedNoArtifact/PackFailed marks all three Unmet (no artifact ⇒ no metadata, no
    /// provenance). NoPackableProjects ⇒ Map.empty (vacuously satisfied, D6). PURE, TOTAL.
    val factContributions: set: PackEvidenceSet -> Map<ReleaseRuleKind, FactState>
