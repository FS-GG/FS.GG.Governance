// PROPOSED public surface for the NEW pure leaf FS.GG.Governance.JsonWriters (feature 073).
// SOLE public-surface declaration (Principle II); JsonWriters.fs carries no access modifiers.
// Drafted .fsi-first (Principle I). References JsonTokens (writers call token helpers).
//
// Pure, total sub-object writers + gate-map helpers shared by the *Json projections. Each
// writer emits its documented field order VERBATIM; each map helper is a first-by-list-order-
// wins fold keyed on the gate-id string. Output is byte-identical to today's projections
// (feature 073 acceptance gate: goldens unchanged). No I/O; never throws.

namespace FS.GG.Governance.JsonWriters

open System.Text.Json
open FS.GG.Governance.Gates.Model              // GateId, GateDisposition
open FS.GG.Governance.GateRun.Model            // GateOutcome
open FS.GG.Governance.CommandRecord.Model      // ExitCode
open FS.GG.Governance.EvidenceReuse.Model      // RecomputeCause
open FS.GG.Governance.CacheEligibility.Model   // CacheEligibilityReport, CacheEligibilityVerdict
open FS.GG.Governance.Enforcement.Model        // EnforcementDecision

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JsonWriters =

    /// The tagged `cause` object — `kind`, then `categories[]` for `inputsChanged`.
    /// `noPriorEvidence` emits no `categories` field. Unifies the six copies, including
    /// VerifyJson's `writeCauseValue`.
    val writeCause: w: Utf8JsonWriter -> cause: RecomputeCause -> unit

    /// First-by-report-order-wins map from gate-id string to cache-eligibility verdict.
    val verdictByGate: report: CacheEligibilityReport -> Map<string, CacheEligibilityVerdict>

    /// First-by-list-order-wins map from gate-id string to gate outcome.
    val outcomeByGate: execution: (GateId * GateOutcome) list -> Map<string, GateOutcome>

    /// The per-gate `execution` object — `disposition`, then optional `exitCode` / `passed`.
    val writeExecution: w: Utf8JsonWriter -> outcome: GateOutcome -> unit

    /// The nested `enforcement` object — the six F023 enforcement fields verbatim.
    val writeEnforcement: w: Utf8JsonWriter -> decision: EnforcementDecision -> unit

// Note: only byte-identical copies move here. Projection-specific writers (single-use, or
// embedding a projection-specific field set) stay in their projection — including
// ReleaseJson's single-use `writeNullableString`/`writeNullableInt` (out of scope, D3).
