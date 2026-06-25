// Curated public signature contract for the route.json projection (F020).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching RouteJson.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here. Every JSON writer and closed-enum token helper lives ONLY in
// the .fs and is absent here, exactly as `FS.GG.Governance.Kernel.Json` keeps its writer/token
// plumbing off `Json.fsi`.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any RouteJson.fs
// body exists (Principle I). `ofRouteResult` is the PURE, TOTAL projection (FR-008): it renders one
// already-typed, already-ordered F019 `RouteResult` into the deterministic, versioned `route.json`
// document text — the stable machine-readable contract the later `fsgg route`/`fsgg ship`, CI, agents,
// generated readiness views, and optional Governance consumers read. It performs no I/O, no git, no
// clock, never throws, and is byte-for-byte identical for identical input (FR-007, SC-002). It
// re-derives, re-sorts, and re-classifies nothing (the `RouteResult` already fixed every order); it
// computes no severity, enforcement, cache-eligibility verdict, or ship verdict (FR-011) and emits no
// raw YAML, host path, timestamp, or environment value (FR-012). Serialization uses the net10.0
// shared-framework `System.Text.Json` — NO new `PackageReference` (FR-015).

namespace FS.GG.Governance.RouteJson

open FS.GG.Governance.Route.Model
open FS.GG.Governance.Gates.Model              // GateId (F052 execution embed matched by gate)
open FS.GG.Governance.CacheEligibility.Model   // F045: CacheEligibilityReport (via the F041 ProjectReference)
open FS.GG.Governance.GateRun.Model            // F052: GateOutcome (the per-gate execution embed)
open FS.GG.Governance.ProductSurfaces.Model    // F23: ProductSurfaceReport (the additive productSurfaces section)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RouteJson =

    /// The declared schema-version token stamped into every emitted document and recorded as the
    /// document's `schemaVersion` field (FR-013), so consumers can branch on the contract version and
    /// detect changes without string-scraping the output. A fixed, deterministic constant — never
    /// derived from a clock, environment, or input value. Bumped to `"fsgg.route/v2"` for the F045
    /// embedded cache-eligibility contract so consumers detect the new shape.
    val schemaVersion: string

    /// Project an F019 `RouteResult` together with an OPTIONAL F041 `CacheEligibilityReport` into its
    /// deterministic, versioned `route.json` document text.
    ///
    /// Emits one top-level JSON object with fields in the FIXED order
    /// `schemaVersion`, `selectedGates`, `findings`, `cost`, `cacheEligibilityEvaluated` (the wire
    /// contract is fixed in contracts/route-json-document.md):
    ///   • `selectedGates` — one object per `SelectedGate`, in the result's `GateId` ordinal order,
    ///                       carrying the gate's declared `id` (via `Gates.gateIdValue`, never
    ///                       re-parsed — FR-010), `domain`, `description`, `cost`, `timeout`, `owner`,
    ///                       `maturity`, `productCheck`, `prerequisites`, the carried `freshnessKey`
    ///                       INPUTS (never a cache verdict — FR-014), `selectingPaths`
    ///                       (`{ path; matchedGlob }`, in normalized-path order), and (F045) the
    ///                       per-gate `cacheEligibility` verdict object matched by `GateId`. No gate
    ///                       the result did not select appears, and no gate/cost/path/finding is
    ///                       invented (FR-002, FR-003, FR-004).
    ///   • `findings`      — the carried F017 `FindingReport` rendered UNCHANGED, in its F017 order;
    ///                       an empty report renders as a present, empty array (FR-005). Findings carry
    ///                       NO `cacheEligibility` verdict — cache is gate-scoped (F045 FR-004).
    ///   • `cost`          — the per-tier `CostRollup` `{ cheap; medium; high; exhaustive }`, every
    ///                       declared tier present with its integer count including zero; never a
    ///                       summed scalar (FR-006).
    ///   • `cacheEligibilityEvaluated` — (F045) the always-present cache-eligibility section flag:
    ///                       `false` for `cache = None`, `true` for `cache = Some _`. It survives the
    ///                       empty-gate-list edge and distinguishes "no cache step ran" from "an
    ///                       evaluated report with no reusable gate".
    ///
    /// `cache = None` is the NOT-EVALUATED state (today's `fsgg route`, which resolves no freshness
    /// inputs): `cacheEligibilityEvaluated: false` and every selected gate's `cacheEligibility` is
    /// `{ kind:"notEvaluated" }` (FR-012). `cache = Some report` renders `cacheEligibilityEvaluated:
    /// true` and, per selected gate matched by `GateId`, that gate's verdict — `reusable` (+ the opaque
    /// evidence reference), `mustRecompute` (+ its no-hide cause), or `notEvaluated` for a selected gate
    /// absent from the report (FR-005). `Some (CacheEligibilityReport [])` is an evaluated-but-empty
    /// report — `evaluated: true` with every gate `notEvaluated` — distinct from `None`.
    ///
    /// ADDITIVE (F045 FR-008): every existing field — selected gates, route trace, freshness-key
    /// inputs, findings, cost — is byte-identical to the F020-only projection of the same `RouteResult`
    /// (modulo the new per-gate `cacheEligibility` field, the top-level `cacheEligibilityEvaluated`
    /// flag, and the bumped `schemaVersion`). The findings array carries NO verdict (FR-004).
    ///
    /// PURE and TOTAL (FR-008, FR-009, F045 FR-010): no file, process, clock, network, or git access;
    /// the opaque evidence reference is rendered verbatim and never dereferenced (F045 FR-011); computes
    /// no freshness key, hash, or cache decision; never throws for any well-typed inputs (including
    /// `None`, an empty report, and an EMPTY route — no selected gates, empty findings, all-zero cost —
    /// which projects to a valid document with empty sections, all-zero cost, and the cache section
    /// present, a success, never an error and never a "select everything" fallback). DETERMINISTIC
    /// (FR-007, SC-002): identical inputs yield byte-for-byte identical text; the projection adds no
    /// ordering decision beyond the fixed field sequence, preserving `RouteResult`'s already-fixed
    /// collection orders verbatim; cache entries follow the document's existing `GateId`-ordinal gate
    /// order; a duplicate `GateId` in the report resolves to the first entry by report order. The
    /// document carries NO severity, profile, mode, enforcement, ship verdict, blocker, warning,
    /// exit-code basis, raw YAML, host/absolute path, timestamp, or environment value (FR-011, FR-012,
    /// SC-007).
    ///
    /// F052: an additive trailing `execution` parameter carries the per-gate `GateOutcome`s matched by
    /// `GateId`. When NON-EMPTY, each selected-gate entry gains an `execution` object beside its F045
    /// `cacheEligibility` — `{ disposition: "executed"|"reused"|"notExecuted", exitCode: <int>, passed:
    /// <bool> }` (`exitCode`/`passed` omitted for `notExecuted`). When EMPTY (the default the emitter's own
    /// goldens use), NO `execution` object is written and the output is BYTE-IDENTICAL to the F045-era
    /// projection (FR-009, D6). The embed is gate-scoped: findings never carry it.
    val ofRouteResult:
        result: RouteResult ->
        cache: CacheEligibilityReport option ->
        execution: (GateId * GateOutcome) list ->
            string

    /// F23 (additive, non-breaking): the same projection as `ofRouteResult` plus an additive
    /// `productSurfaces` array carrying the F23 product-surface classification. The array is emitted as the
    /// document's LAST top-level field ONLY when `productSurfaces.Classifications` is non-empty; an EMPTY
    /// report writes NO `productSurfaces` field, so the output is BYTE-IDENTICAL to `ofRouteResult result
    /// cache execution` (the F052 default-empty precedent — existing goldens are untouched, schemaVersion is
    /// unchanged). Each entry is `{ path, capability, surface, class, tier, tierDeclared, alternative }`,
    /// sorted by path then surface (the report's own deterministic order). PURE and TOTAL, like
    /// `ofRouteResult`; the classification is consumed verbatim — no re-classification, no re-sort.
    val ofRouteResultWithProductSurfaces:
        result: RouteResult ->
        cache: CacheEligibilityReport option ->
        execution: (GateId * GateOutcome) list ->
        productSurfaces: ProductSurfaceReport ->
            string
