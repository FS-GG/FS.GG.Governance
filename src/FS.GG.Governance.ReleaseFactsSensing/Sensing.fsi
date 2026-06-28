// Curated public signature contract for the PURE release-facts derivation (F054).
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the matching
// Sensing.fs carries NO `private`/`internal`/`public` modifiers — the per-family classifiers (the version
// dotted-numeric "bumped past" compare, the metadata containment check, the pin-resolution check, the posture
// subset check) and the snapshot builders live ONLY in the .fs and are absent here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Sensing.fs body exists
// (Principle I). `deriveFacts` is PURE and TOTAL (FR-008, FR-009): no I/O, no git, no clock, no process, no
// document; defined for every expectation/recovered combination (including all-absent ⇒ all-`Unrecoverable`);
// NEVER throws; structurally identical for identical input (SC-003). It REUSES the F053 `ReleaseFacts`/
// `FactState`/`ReleaseRuleKind` verbatim as its output vocabulary (research D1) and senses NOTHING itself —
// the impure reads happen at the `Interpreter` edge and arrive as a `RecoveredEvidence` bundle (research D3).
//
// 088 SENSOR HOME (T001): the ApiCompat output PARSE (pure `string -> ApiBreakSignal`,
// `Sensing.parseApiCompatOutput`) is folded INTO this leaf rather than spawning a new
// `FS.GG.Governance.ApiCompat` leaf — it keeps the new dependency scope tightest (one extra leaf reference to
// PackEvidence for the `ApiBreakSignal` vocabulary) and keeps the "all families always present" maintenance
// in one place. The `ApiCompatibility` family is sensed at the host detector overlay (cross-package worst-of,
// mirroring the F065 pack-evidence join), so `deriveFacts` emits it `Unrecoverable` (no repo-file criterion)
// until the host overlays the graded detector value — exactly as it emits `VersionBump` from repo files and
// the host overlays the pack-derived `VersionBump`.

namespace FS.GG.Governance.ReleaseFactsSensing

open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.ReleaseFactsSensing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Sensing =

    /// The seven recognized release families in `ReleaseRuleKind` declaration order (`VersionBump` ..
    /// `ApiCompatibility`) — which is the `releaseRuleKindOrdinal` order, so this list, the `deriveFacts`
    /// per-family iteration, and the diagnostics ordering are one and the same. Exposed so a caller/test can
    /// assert the output covers exactly these seven and iterate deterministically (FR-009, SC-006). Total; a
    /// fixed list, never empty. 088: `ApiCompatibility` is the seventh family — `deriveFacts` emits it
    /// `Unrecoverable` (host-overlaid, not repo-sensed; see the SENSOR HOME note above).
    val releaseFamilies: ReleaseRuleKind list

    /// 088 (T014): parse ONE package's captured ApiCompat / Package-Validation output into an `ApiBreakSignal`
    /// (the pure `string -> ApiBreakSignal` sensor parse; T001 sensor home). The detector `.fsx` emits a
    /// normalized line protocol the host interpreter feeds here; raw ApiCompat `CPxxxx` diagnostic lines are
    /// ALSO recognized. Control markers (one per package, emitted by the detector): `NOTPACKABLE` ⇒
    /// `NotPackable`; `NOBASELINE` ⇒ `NoBaseline` (FR-009); `ERROR <reason>` ⇒ `Indeterminate` (FR-008);
    /// `OK` / `NOBREAKINGCHANGES` ⇒ `NoBreakingChanges`. Break lines — normalized
    /// `BREAK <removed|signature|type-removed|other:<label>> <local|inherited:<surface>> <member…>` or a raw
    /// `CP0001`(type removed)/`CP0002`(member removed)/`CP000n`(signature) line — accumulate into
    /// `BreakingChanges`. FAIL-SAFE and TOTAL (FR-008): empty / whitespace-only / unrecognized-only output ⇒
    /// `Indeterminate`, NEVER `NoBreakingChanges`; never throws.
    val parseApiCompatOutput: output: string -> ApiBreakSignal

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
    /// The returned `Facts.States` ALWAYS contains exactly the seven families (FR-009, SC-006) — never partial.
    /// `ApiCompatibility` is always `Unrecoverable` here (host-overlaid; not repo-sensed — see SENSOR HOME).
    /// Every collection in the snapshot is emitted in a fixed order (sorted fields/pins/tokens, diagnostics by
    /// `releaseRuleKindOrdinal`), so identical inputs yield a structurally identical `SensedRelease` (FR-008, SC-003).
    /// `Facts` is the F053 `ReleaseFacts` type, so it feeds `Release.evaluate` with no adaptation (FR-002).
    ///
    /// TOTAL: defined for every expectation/recovered combination (including all-absent ⇒ all-`Unrecoverable`,
    /// the all-missing edge case). Never throws. PURE: no I/O, clock, process, or document (FR-008).
    val deriveFacts: expectations: ReleaseExpectations -> recovered: RecoveredEvidence -> SensedRelease
