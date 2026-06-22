// CONTRACT (Phase 1, F045): the new public signature for the audit.json projection after the
// cache-eligibility embed. This is the authoritative shape the implementation must match; the live
// src/FS.GG.Governance.AuditJson/AuditJson.fsi is edited to this and the surface baseline re-blessed.
//
// Delta from F025: `ofShipDecision` gains a second `CacheEligibilityReport option` parameter, and
// `schemaVersion` bumps to "fsgg.audit/v2". Nothing else on the surface changes.

namespace FS.GG.Governance.AuditJson

open FS.GG.Governance.Ship.Model
open FS.GG.Governance.CacheEligibility.Model   // NEW: CacheEligibilityReport (transitively via the F041 ProjectReference)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AuditJson =

    /// The declared schema-version token stamped into every emitted document (FR-013). Bumped to
    /// "fsgg.audit/v2" for the embedded cache-eligibility contract so consumers detect the change.
    /// A fixed, deterministic constant — never derived from a clock, environment, or input value.
    val schemaVersion: string

    /// Project an F024 `ShipDecision` together with an OPTIONAL F041 `CacheEligibilityReport` into the
    /// deterministic, versioned `audit.json` document text.
    ///
    /// `cache = None` is the NOT-EVALUATED state (today's `fsgg ship`, which resolves no freshness
    /// inputs): the document renders `cacheEligibilityEvaluated: false` and every GATE item's
    /// `cacheEligibility` is `{ kind:"notEvaluated" }` (FR-012). `cache = Some report` renders
    /// `cacheEligibilityEvaluated: true` and, per GATE item (in any of blockers/warnings/passing) matched
    /// by `GateId`, that gate's verdict — `reusable` (+ the opaque evidence reference), `mustRecompute`
    /// (+ its no-hide cause), or `notEvaluated` for a gate item absent from the report (FR-005).
    /// FINDING items carry NO `cacheEligibility` field — cache is gate-scoped (FR-004, SC-002).
    ///
    /// ADDITIVE / NO-HIDE (FR-008, FR-011): the `verdict`, `exitCodeBasis`, every item's section, and the
    /// six-field `enforcement` detail are byte-identical to the F025-only projection of the same
    /// `ShipDecision` (modulo the new per-gate `cacheEligibility` field, the top-level
    /// `cacheEligibilityEvaluated` flag, and the bumped `schemaVersion`). A `reusable` verdict on a
    /// base-`blocking` gate leaves it in the blockers section with full enforcement detail — the cache
    /// verdict relaxes, hides, or alters NO enforcement, severity, section, or ship outcome.
    ///
    /// PURE and TOTAL (FR-010): no file, process, clock, network, git, or cache-store access; the opaque
    /// evidence reference is rendered verbatim and never dereferenced (FR-011); computes no freshness
    /// key, hash, or cache decision; never throws for any well-typed inputs (including `None`, an empty
    /// report, and a clean empty decision). DETERMINISTIC (FR-007): identical inputs ⇒ byte-identical
    /// text; cache entries follow the document's existing composite item order; a duplicate `GateId` in
    /// the report resolves to the first entry by report order.
    val ofShipDecision: decision: ShipDecision -> cache: CacheEligibilityReport option -> string
