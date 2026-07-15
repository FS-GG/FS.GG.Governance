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
open FS.GG.Governance.Gates.Model              // GateId, GatePrerequisite
open FS.GG.Governance.GateRun.Model            // GateOutcome
open FS.GG.Governance.EvidenceReuse.Model      // RecomputeCause
open FS.GG.Governance.FreshnessKey.Model       // FreshnessKey, Revision, RuleHash, GeneratorVersion, ArtifactHash
open FS.GG.Governance.Provenance.Model         // BuilderIdentity — JSON-3
open FS.GG.Governance.CommandKind.Model        // KindedCommandRun — JSON-3
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

    /// The freshness-key object — `check`, `domain`, `cost`, `environment`, then `command` (null when absent).
    /// Unifies the byte-identical Gates/Route copies.
    val writeFreshnessKey: w: Utf8JsonWriter -> key: FreshnessKey -> unit

    /// One prerequisite: `RequiresCommand c` → `{ "requiresCommand": "<commandId>" }`. Gates/Route copies.
    val writePrerequisite: w: Utf8JsonWriter -> prereq: GatePrerequisite -> unit

    /// The cache-eligibility object — `reusable` + `evidence`, `mustRecompute` + `cause`, or `notEvaluated`.
    /// Unifies the byte-identical Audit/Route copies.
    val writeCacheEligibility: w: Utf8JsonWriter -> verdict: CacheEligibilityVerdict option -> unit

    // JSON-3: the newtype value un-wrappers + the per-run audit writer the AttestationJson / ProvenanceJson
    // projections used to hand-copy verbatim. `exitCodeValue` / `durationNanos` are consumed only by
    // `writeRun`, so they are NOT exposed here (hidden in JsonWriters.fs).

    /// `Revision s` → `s`.
    val revisionValue: Revision -> string

    /// `RuleHash s` → `s`.
    val ruleHashValue: RuleHash -> string

    /// `GeneratorVersion s` → `s`.
    val generatorVersionValue: GeneratorVersion -> string

    /// `ArtifactHash s` → `s`.
    val artifactValue: ArtifactHash -> string

    /// `BuilderIdentity s` → `s`.
    val builderValue: BuilderIdentity -> string

    /// One command run — field order `kind`, `identity`, `exitCode`, `durationNanos`. `identity` reuses the
    /// F032 identity verbatim (via `Audit.runIdentity`); `durationNanos` is sensed metadata, never identity.
    /// Unifies the byte-identical AttestationJson/ProvenanceJson copies.
    val writeRun: w: Utf8JsonWriter -> run: KindedCommandRun -> unit
