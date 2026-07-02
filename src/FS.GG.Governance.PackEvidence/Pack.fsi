// Curated public signature contract for the packed-evidence operations (F26, P1).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Pack.fs carries NO access modifiers; the per-outcome reason/verdict helpers are ABSENT here (private by
// omission) and the semantic-version comparator now lives in the shared `SemVer` module (ReleaseRules,
// review M-ADPT-1). All operations are PURE, TOTAL, DETERMINISTIC (FR-010): no
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

    // ── 088 Breaking-Change (API-Compat) gate: pure verdict + delta + coverage (data-model §3–§5) ──

    /// 088: the semantic magnitude of packed-vs-baseline, REUSING the same comparator behind `versionPolicy`
    /// (numeric core segments; build metadata ignored; pre-release < release). `MajorBump` iff the packed
    /// major segment strictly exceeds the baseline's; any other strictly-forward bump ⇒ `MinorOrPatchBump`;
    /// equal-or-downgrade ⇒ `NoForwardChange`; an absent baseline OR absent packed version ⇒ `NoBaselineDelta`.
    /// PURE, TOTAL, never throws. `versionPolicy` semantics are left unchanged (this is an additive helper).
    val versionDelta: baseline: string option -> packed: string option -> VersionDelta

    /// 088: combine the sensor break signal with the version delta into the governing `ApiCompatibility`
    /// `FactState` (the authoritative D4 table). PURE, TOTAL, deterministic.
    ///   • `NoBreakingChanges`         + any                              ⇒ `Some Met`
    ///   • `BreakingChanges _`         + `MajorBump`                      ⇒ `Some Met`
    ///   • `BreakingChanges _`         + `MinorOrPatchBump`/`NoForwardChange`/`NoBaselineDelta` ⇒ `Some Unmet`
    ///   • `NoBaseline`                + any                              ⇒ `Some Met` (vacuous, FR-009)
    ///   • `Indeterminate _`           + any                              ⇒ `Some Unrecoverable` (fail-safe, FR-008)
    ///   • `NotPackable`               + any                              ⇒ `None` (not covered, FR-007)
    /// `None` means no rule fact is emitted for the package; the host lists it `NotCovered` in coverage. This
    /// row set is the project's break-detection / additive-change verdict corpus (SC-002 / SC-003).
    val apiCompatibilityFact: signal: ApiBreakSignal -> delta: VersionDelta -> FactState option

    /// 088: the explicit, deterministic per-package coverage outcome for ONE package's signal+delta, so a
    /// package is reported `Checked`/`NoBaselineYet`/`NotCovered` and never silently clean (FR-007 / SC-001).
    /// `NotPackable` / `Indeterminate` ⇒ `NotCovered reason`; `NoBaseline` ⇒ `NoBaselineYet`; a compared
    /// package ⇒ `Checked` carrying its graded `FactState`. PURE, TOTAL.
    val coverageOutcome: signal: ApiBreakSignal -> delta: VersionDelta -> ApiCompatCoverageOutcome

    /// 088: build the deterministic, `SurfaceId`-sorted coverage list from each package's sensed signal +
    /// version delta — every package present, zero silent passes (FR-007 / SC-001). PURE, TOTAL.
    val apiCompatCoverage: packages: (SurfaceId * ApiBreakSignal * VersionDelta) list -> ApiCompatCoverage list

    /// 088: the single cross-package `ApiCompatibility` `FactState` the host overlays onto the sensed facts
    /// (the worst-of, mirroring `factContributions`' all-`Met`-or-`Unmet` rollup): `Unrecoverable` if any
    /// covered package is indeterminate, else `Unmet` if any breaks under an inadequate bump, else `Met`.
    /// Packages with no emitted fact (`NotPackable`) do not contribute. An EMPTY/all-uncovered set ⇒ `Met`
    /// (vacuously satisfied — reported via coverage, never a silent block). PURE, TOTAL.
    val apiCompatibilityRollup: packages: (ApiBreakSignal * VersionDelta) list -> FactState
