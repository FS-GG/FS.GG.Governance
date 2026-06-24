// CONTRACT DRAFT (Phase 1) for specs/054-release-facts-sensing.
// The shipped Sensing.fsi (in src/FS.GG.Governance.ReleaseFactsSensing/) is the SOLE declaration of the
// module's public surface (Constitution Principle II). The matching Sensing.fs carries NO
// `private`/`internal`/`public` modifiers — the per-family classifiers (the version dotted-numeric "bumped
// past" compare, the metadata containment check, the pin-resolution check, the posture subset check) and the
// snapshot builders live ONLY in the .fs and are absent here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Sensing.fs body exists
// (Principle I). `deriveFacts` is PURE and TOTAL (FR-008, FR-009): no I/O, no git, no clock, no process, no
// document; defined for every expectation/recovered combination (including all-absent ⇒ all-`Unrecoverable`);
// NEVER throws; structurally identical for identical input (SC-003). It REUSES the F053 `ReleaseFacts`/
// `FactState`/`ReleaseRuleKind` verbatim as its output vocabulary (research D1) and senses NOTHING itself —
// the impure reads happen at the `Interpreter` edge and arrive as a `RecoveredEvidence` bundle (research D3).

namespace FS.GG.Governance.ReleaseFactsSensing

open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Sensing =

    /// The six recognized release families in `ReleaseRuleKind` declaration order (`VersionBump` ..
    /// `Provenance`) — which is the `releaseRuleKindOrdinal` order, so this list, the `deriveFacts` per-family
    /// iteration, and the diagnostics ordering are one and the same. Exposed so a caller/test can assert the
    /// output covers exactly these six and iterate deterministically (FR-009, SC-006). Total; a fixed list, never empty.
    val releaseFamilies: ReleaseRuleKind list

    /// Derive the per-family `FactState` and the observed-evidence snapshot from the caller's `expectations`
    /// and the `recovered` evidence the edge gathered (US1+US2). For each of the six families, in
    /// `releaseRuleKindOrdinal` order (research D6):
    ///   • expectation ABSENT for the family            ⇒ `Unrecoverable`, snapshot `None`, a diagnostic;
    ///   • recovered evidence is `Error` (absent/unreadable/unparseable) ⇒ `Unrecoverable`, `None`, a diagnostic;
    ///   • recovered evidence SATISFIES the expectation ⇒ `Met`,   snapshot `Some` observed evidence;
    ///   • recovered evidence does NOT satisfy it       ⇒ `Unmet`, snapshot `Some` observed evidence.
    /// `Unmet` and `Unrecoverable` are kept DISTINCT (FR-003): "read it and it fell short" vs "could not read
    /// it / no criterion." An unreadable source NEVER becomes a fabricated `Met` (FR-004).
    ///
    /// The returned `Facts.States` ALWAYS contains exactly the six families (FR-009, SC-006) — never partial.
    /// Every collection in the snapshot is emitted in a fixed order (sorted fields/pins/tokens, diagnostics by
    /// `releaseRuleKindOrdinal`), so identical inputs yield a structurally identical `SensedRelease` (FR-008, SC-003).
    /// `Facts` is the F053 `ReleaseFacts` type, so it feeds `Release.evaluate` with no adaptation (FR-002).
    ///
    /// TOTAL: defined for every expectation/recovered combination (including all-absent ⇒ all-`Unrecoverable`,
    /// the all-missing edge case). Never throws. PURE: no I/O, clock, process, or document (FR-008).
    val deriveFacts: expectations: ReleaseExpectations -> recovered: RecoveredEvidence -> SensedRelease
