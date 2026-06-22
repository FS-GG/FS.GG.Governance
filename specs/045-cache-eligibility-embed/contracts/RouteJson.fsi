// CONTRACT (Phase 1, F045): the new public signature for the route.json projection after the
// cache-eligibility embed. This is the authoritative shape the implementation must match; the live
// src/FS.GG.Governance.RouteJson/RouteJson.fsi is edited to this and the surface baseline re-blessed.
//
// Delta from F020: `ofRouteResult` gains a second `CacheEligibilityReport option` parameter, and
// `schemaVersion` bumps to "fsgg.route/v2". Nothing else on the surface changes.

namespace FS.GG.Governance.RouteJson

open FS.GG.Governance.Route.Model
open FS.GG.Governance.CacheEligibility.Model   // NEW: CacheEligibilityReport (transitively via the F041 ProjectReference)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RouteJson =

    /// The declared schema-version token stamped into every emitted document (FR-013). Bumped to
    /// "fsgg.route/v2" for the embedded cache-eligibility contract so consumers detect the change.
    /// A fixed, deterministic constant — never derived from a clock, environment, or input value.
    val schemaVersion: string

    /// Project an F019 `RouteResult` together with an OPTIONAL F041 `CacheEligibilityReport` into the
    /// deterministic, versioned `route.json` document text.
    ///
    /// `cache = None` is the NOT-EVALUATED state (today's `fsgg route`, which resolves no freshness
    /// inputs): the document renders `cacheEligibilityEvaluated: false` and every selected gate's
    /// `cacheEligibility` is `{ kind:"notEvaluated" }` (FR-012). `cache = Some report` renders
    /// `cacheEligibilityEvaluated: true` and, per selected gate matched by `GateId`, that gate's verdict
    /// — `reusable` (+ the opaque evidence reference), `mustRecompute` (+ its no-hide cause), or
    /// `notEvaluated` for a selected gate absent from the report (FR-005). `Some (CacheEligibilityReport
    /// [])` is an evaluated-but-empty report — `evaluated: true` with every gate `notEvaluated` —
    /// distinct from `None`.
    ///
    /// ADDITIVE (FR-008): every existing field — selected gates, route trace, freshness-key inputs,
    /// findings, cost — is byte-identical to the F020-only projection of the same `RouteResult` (modulo
    /// the new `cacheEligibility` per-gate field, the top-level `cacheEligibilityEvaluated` flag, and the
    /// bumped `schemaVersion`). The findings array carries NO verdict (cache is gate-scoped — FR-004).
    ///
    /// PURE and TOTAL (FR-010): no file, process, clock, network, git, or cache-store access; the opaque
    /// evidence reference is rendered verbatim and never dereferenced (FR-011); computes no freshness
    /// key, hash, or cache decision; never throws for any well-typed inputs (including `None`, an empty
    /// report, and an empty route). DETERMINISTIC (FR-007): identical inputs ⇒ byte-identical text;
    /// cache entries follow the document's existing `GateId`-ordinal gate order; a duplicate `GateId` in
    /// the report resolves to the first entry by report order.
    val ofRouteResult: result: RouteResult -> cache: CacheEligibilityReport option -> string
