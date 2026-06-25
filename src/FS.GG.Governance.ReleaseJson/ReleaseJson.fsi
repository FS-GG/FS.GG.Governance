// Curated public signature contract for the F055 release.json projection.
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching ReleaseJson.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here. Every `Utf8JsonWriter` walk and closed-enum token helper lives
// ONLY in the .fs and is absent here, exactly as `FS.GG.Governance.AuditJson` /
// `FS.GG.Governance.CacheEligibilityJson` keep their writer/token plumbing off their .fsi.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any ReleaseJson.fs
// body exists (Principle I). `ofRelease` is the PURE, TOTAL projection (FR-008): it renders one
// already-typed, already-partitioned F053 `ReleaseDecision` plus the F054 `SensedRelease` observed-
// evidence snapshot into the deterministic, versioned `release.json` WHOLE-RELEASE audit document text —
// the stable, machine-readable release-gate contract CI gates, dashboards, agents, and humans read
// instead of an in-memory value. It performs no I/O, no git, no clock, never throws, and is byte-for-byte
// identical for identical input (FR-008, SC-003). It re-derives, re-classifies, and re-evaluates nothing
// (the `ReleaseDecision` already fixed the verdict / exit-code basis / three-way partition, and the
// `SensedRelease` already fixed each family's fact state and evidence ordering); it maps no numeric
// process exit code and emits no host path, timestamp, or environment value. Serialization uses the
// net10.0 shared-framework `System.Text.Json` — NO new `PackageReference` (FR-014).
//
// Sibling of F025 `AuditJson` (the whole-change ship verdict) and F042 `CacheEligibilityJson` (the
// per-change cache verdict): this projects the WHOLE-RELEASE gate verdict (`ReleaseDecision` +
// `SensedRelease`). Every `mustRecompute`-style no-hide rule is preserved: every declared rule yields one
// `rules[]` entry (none dropped, none fabricated — FR-007/FR-013), and an `unrecoverable` family renders a
// `null` evidence object — never a fabricated `met` (FR-005).

namespace FS.GG.Governance.ReleaseJson

open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseReport.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReleaseJson =

    /// The declared schema-version token stamped into every emitted document and recorded as the
    /// document's `schemaVersion` field, so consumers can branch on the contract version. A fixed,
    /// deterministic constant (`"fsgg.release/v2"` — the F26 additive bump; every v1 field unchanged in
    /// shape and order) — never derived from a clock, environment, or input.
    val schemaVersion: string

    /// Project the F26 ReleaseReport (the single source of truth, FR-012) into `fsgg.release/v2`. Renders
    /// the v1 fields VERBATIM from `report.Decision` + `report.Sensed` (schemaVersion, verdict,
    /// exitCodeBasis, rules, evidence — the publish-plan/posture/template-pin precondition state + reason
    /// surface through the existing `rules` array, no new field), then appends exactly three additive
    /// fields in fixed order: `packageEvidence` (per-project surface/outcome/version/digest, sorted by
    /// surface then artifact path), `versionPolicy` (per-project version verdict), and `attestation` (a
    /// self-contained identity reference; the full summary lives in the attestation.json sidecar). PURE,
    /// TOTAL, byte-identical for identical input.
    val ofReleaseReport: report: ReleaseReport -> string

    /// Project an F053 `ReleaseDecision` + an F054 `SensedRelease` into the deterministic, versioned
    /// `release.json` whole-release audit document text.
    ///
    /// Emits one top-level JSON object with fields in the FIXED order `schemaVersion`, `verdict`,
    /// `exitCodeBasis`, `rules`, `evidence` (the wire contract fixed in contracts/release.schema.md):
    ///   • `schemaVersion`  — the fixed constant above, never derived from a clock/environment/input.
    ///   • `verdict`        — `decision.Verdict` ("pass"/"fail"), carried VERBATIM, never recomputed.
    ///   • `exitCodeBasis`  — `decision.ExitCodeBasis` ("clean"/"blocked"), VERBATIM, never mapped to a
    ///                        numeric process exit code.
    ///   • `rules`          — one entry per declared rule, recovered from the decision's three partition
    ///                        lists and emitted in the F053 stable composite order (`releaseRuleKindOrdinal`
    ///                        then surface id). Each entry carries `kind` (`releaseRuleKindToken`),
    ///                        `surface`, `factState` ("met"/"unmet"/"unrecoverable", from
    ///                        `sensed.Facts.States`), `outcome` ("satisfied"/"violated"), `baseSeverity`,
    ///                        `effectiveSeverity` (the F023 derived value), and the product-neutral
    ///                        `reason`. On every successful run there are exactly six entries (one per
    ///                        family, FR-013/SC-006) — none dropped, none fabricated.
    ///   • `evidence`       — the F054 `ReleaseSnapshot`: `surface`, then per-family `version`/`metadata`/
    ///                        `pins`/`publishPlan`/`trustedPublishing`/`provenance` (the object when the
    ///                        evidence was recovered, `null` when `unrecoverable` — never a fabricated
    ///                        `met`, FR-005), and the ordinal-sorted `diagnostics`.
    ///
    /// PURE and TOTAL (FR-008): no file, process, clock, network, or git access; never throws for any
    /// well-typed input; an empty/clean decision projects to a valid document. DETERMINISTIC (FR-008,
    /// SC-003): identical inputs yield byte-for-byte identical text; reordering the decision's input
    /// partition lists never changes the output (the composite re-ordering is total and value-keyed). The
    /// document carries NO wall-clock timestamp, host/absolute path, environment value, or numeric process
    /// exit code (SC-007).
    val ofRelease: decision: ReleaseDecision -> sensed: SensedRelease -> string
