// Curated public signature contract for the pure sub-object/map writer leaf (feature 073).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the
// matching JsonWriters.fs carries NO access modifiers. Drafted .fsi-first (Principle I). References
// JsonTokens (writeExecution calls dispositionToken).
//
// Pure, total sub-object writers + gate-map helpers shared by the *Json projections. Each writer emits
// its documented field order VERBATIM; each map helper is a first-by-list-order-wins fold keyed on the
// gate-id string. Output is byte-identical to today's projections (feature 073 acceptance gate: goldens
// unchanged). No I/O; never throws.
//
// SCOPE NOTE: only TRULY byte-identical copies are exposed here. VerifyJson's `writeCauseValue` and its
// `writeExecution` (`GateOutcome option`, explicit nulls, the divergent `not-executed` token), and the
// per-projection `writeEnforcement` (Audit's `modeToken d.Mode` vs Verify's literal `"verify"`) DIVERGE
// and stay local in their projections.

namespace FS.GG.Governance.JsonWriters

open System.Text.Json
open FS.GG.Governance.Gates.Model              // GateId
open FS.GG.Governance.GateRun.Model            // GateOutcome
open FS.GG.Governance.EvidenceReuse.Model      // RecomputeCause
open FS.GG.Governance.CacheEligibility.Model   // CacheEligibilityReport, CacheEligibilityVerdict

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JsonWriters =

    /// The tagged `cause` object — `kind`, then `categories[]` for `inputsChanged`. `noPriorEvidence`
    /// emits no `categories` field. Unifies the five byte-identical projection copies.
    val writeCause: w: Utf8JsonWriter -> cause: RecomputeCause -> unit

    /// First-by-report-order-wins map from gate-id string to cache-eligibility verdict.
    val verdictByGate: report: CacheEligibilityReport -> Map<string, CacheEligibilityVerdict>

    /// First-by-list-order-wins map from gate-id string to gate outcome.
    val outcomeByGate: execution: (GateId * GateOutcome) list -> Map<string, GateOutcome>

    /// The per-gate `execution` object — `disposition`, then optional `exitCode` / `passed` (omitted when
    /// absent). The Audit/Route form over a present `GateOutcome`.
    val writeExecution: w: Utf8JsonWriter -> outcome: GateOutcome -> unit
