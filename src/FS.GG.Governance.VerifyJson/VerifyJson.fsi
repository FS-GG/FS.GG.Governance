// Curated public signature contract for the F056 verify.json projection.
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching VerifyJson.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here. Every `Utf8JsonWriter` walk and closed-enum token helper lives
// ONLY in the .fs and is absent here, exactly as `FS.GG.Governance.AuditJson` /
// `FS.GG.Governance.CacheEligibilityJson` / `FS.GG.Governance.ReleaseJson` keep their writer/token
// plumbing off their .fsi.
//
// Design-first artifact: drafted and exercised before any VerifyJson.fs body exists (Principle I).
// `ofVerifyDecision` is the PURE, TOTAL projection (FR-008): it renders one already-typed,
// already-partitioned F024 `ShipDecision` (rolled at `RunMode.Verify`) plus the F041
// `CacheEligibilityReport` and the F052 per-gate execution outcomes into the deterministic, versioned
// `verify.json` WHOLE-CHANGE pre-PR verification document text — the stable, machine-readable verification
// contract a pre-PR CI step, dashboards, agents, and humans read instead of an in-memory value. It performs
// no I/O, no git, no clock, never throws, and is byte-for-byte identical for identical input (FR-007/FR-008,
// SC-004). It re-derives, re-classifies, and re-evaluates nothing (the `ShipDecision` already fixed the
// verdict / exit-code basis / three-way partition, the `CacheEligibilityReport` already fixed each per-gate
// verdict, and the execution list already fixed each per-gate disposition); it maps no numeric process exit
// code and emits no host path, timestamp, username, or environment value. Serialization uses the net10.0
// shared-framework `System.Text.Json` — NO new `PackageReference` (FR-014).
//
// Sibling of F025 `AuditJson` (the whole-change ship verdict): this projects the whole-change VERIFY verdict
// plus a first-class `currency` section (fresh/reused, recomputed, unresolved). The `mode` field is always
// `"verify"`. An `unrecoverable`/uncertain check is never rendered as a fabricated pass (FR-005): the verdict
// and partition are carried verbatim from the decision.

namespace FS.GG.Governance.VerifyJson

open FS.GG.Governance.Ship.Model              // ShipDecision
open FS.GG.Governance.Gates.Model             // GateId
open FS.GG.Governance.GateRun.Model           // GateOutcome
open FS.GG.Governance.CacheEligibility.Model  // CacheEligibilityReport
open FS.GG.Governance.ReleaseReport.Model     // VerifyReleasePreview (F26)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VerifyJson =

    /// The declared schema-version token stamped into every emitted document and recorded as the
    /// document's `schemaVersion` field, so consumers can branch on the contract version. A fixed,
    /// deterministic constant (`"fsgg.verify/v1"`) — never derived from a clock, environment, or input.
    val schemaVersion: string

    /// Project an F024 `ShipDecision` (rolled at `RunMode.Verify`) + the F041 `CacheEligibilityReport`
    /// (the per-gate freshness/reuse disposition) + the F052 per-gate execution outcomes into the
    /// deterministic, versioned `verify.json` whole-change verification document text.
    ///
    /// Emits one top-level JSON object with fields in the FIXED order `schemaVersion`, `verdict`,
    /// `exitCodeBasis`, `blockers`, `warnings`, `passing`, `currency` (the wire contract fixed in
    /// contracts/verify.schema.md):
    ///   • `schemaVersion`  — the fixed constant above, never derived from a clock/environment/input.
    ///   • `verdict`        — `decision.Verdict` ("pass"/"blocked"), carried VERBATIM, never recomputed.
    ///   • `exitCodeBasis`  — `decision.ExitCodeBasis` ("clean"/"blocked"), VERBATIM, never mapped to a
    ///                        numeric process exit code.
    ///   • `blockers`/`warnings`/`passing` — one entry per enforced item, in the decision's already-fixed
    ///                        composite order. Each entry carries `id` (a tagged `gate`/`finding` object),
    ///                        `enforcement` (`baseSeverity`, `maturity`, `mode` (always `"verify"`),
    ///                        `profile`, `effectiveSeverity`, `reason`), the per-gate `cache` verdict
    ///                        (`null` for a finding or an unevaluated gate), and the per-gate `execution`
    ///                        outcome (`null` for a finding or a not-run gate).
    ///   • `currency`       — `fresh` (reusable evidence references), `recomputed` (must-recompute causes),
    ///                        `unresolved` (selected gates with no resolved freshness facts), each in the
    ///                        report's already-fixed order. No new sensing, no new severity path.
    ///
    /// PURE and TOTAL (FR-008): no file, process, clock, network, or git access; never throws for any
    /// well-typed input; an empty/clean decision projects to a valid document. DETERMINISTIC (FR-007/FR-008,
    /// SC-004): identical inputs yield byte-for-byte identical text. The document carries NO wall-clock
    /// timestamp, host/absolute path, username, environment value, or numeric process exit code.
    val ofVerifyDecision:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        execution: (GateId * GateOutcome) list ->
            string

    /// F24 (additive, non-breaking): the same projection as `ofVerifyDecision` plus an additive
    /// `surfaceChecks` array carrying the F24 product-surface findings. The array is emitted as the
    /// document's LAST top-level field ONLY when `findings` is non-empty; an EMPTY list writes NO
    /// `surfaceChecks` field, so the output is BYTE-IDENTICAL to `ofVerifyDecision decision cache execution`
    /// (the F23 default-empty precedent — existing goldens untouched, `schemaVersion` unchanged). Each entry
    /// is `{ domain, surface, code, file, detail, severity, inputState, evidenceTag?, message }`, emitted in
    /// the caller's (Composition.run) order — re-sorting NOTHING. `evidenceTag` is omitted when the surface
    /// declared none (FR-009); `severity` is the base severity (advisory entries appear but never change the
    /// exit code — FR-011). PURE and TOTAL, like `ofVerifyDecision`.
    val ofVerifyDecisionWithSurfaceChecks:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        execution: (GateId * GateOutcome) list ->
        findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list ->
            string

    /// F26 (additive, non-breaking): the same projection as `ofVerifyDecisionWithSurfaceChecks` plus an
    /// advisory `releaseReadiness` block carrying the F26 `VerifyReleasePreview`. The block is the
    /// document's LAST top-level field, emitted ONLY when `preview` is `Some`; a `None` preview writes NO
    /// block, so the output is BYTE-IDENTICAL to `ofVerifyDecisionWithSurfaceChecks decision cache execution
    /// findings` (the F24 optional-additive precedent — `schemaVersion` stays `fsgg.verify/v1`, existing
    /// goldens untouched, verify's exit code decided WITHOUT the preview). The block carries `advisory:
    /// true`, the previewed `verdict`, and the same `packageEvidence`/`versionPolicy`/`attestation` shape as
    /// `release.json` v2. PURE and TOTAL.
    val ofVerifyDecisionWithPreview:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        execution: (GateId * GateOutcome) list ->
        findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list ->
        preview: VerifyReleasePreview option ->
            string

    /// F070: the additive overload carrying the stale-generated-view currency findings + their F023
    /// `EnforcementDecision`s. Emits an additive `generatedViews` array (omitted when empty ⇒ byte-identical to
    /// `ofVerifyDecisionWithPreview`, FR-004). Existing entry points untouched (FR-010).
    val ofVerifyDecisionWithGeneratedViews:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        execution: (GateId * GateOutcome) list ->
        findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list ->
        preview: VerifyReleasePreview option ->
        generatedViews:
            (FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding *
             FS.GG.Governance.Enforcement.Enforcement.EnforcementDecision) list ->
            string
