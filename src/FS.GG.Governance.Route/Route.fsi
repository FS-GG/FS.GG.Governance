// Curated public signature contract for the route-selection entry point of route gate selection
// (F019).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Route.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Route.fs body
// exists (Principle I). `select` is PURE and TOTAL (FR-008, FR-009): no I/O, no git, no clock, never
// throws, and byte-for-byte identical for identical input (FR-007, SC-005). It consumes the
// already-typed F018 registry, F015 routing outcomes, and F017 findings; it re-parses no `.fsgg`
// YAML, re-normalizes no path, re-routes no glob, re-builds no registry, re-classifies no finding,
// and senses no git (FR-008, FR-013). It is the Phase-2 *route resolution core*: it establishes the
// selected-gate set + route trace the later `fsgg route`/`fsgg ship`, route/audit JSON, and ship
// verdict rows consume — but it serializes nothing, runs nothing, and enforces nothing (FR-011).

namespace FS.GG.Governance.Route

open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Route =

    /// Select the gates a change must run, with a route trace explaining each selection.
    ///
    /// Inputs (all already typed by upstream rows — none recomputed here):
    ///   • `registry` — the F018 `GateRegistry` (the stable gates, one per declared check, each
    ///                  carrying its `Domain` and declared `Cost`). Selected from, never rebuilt.
    ///   • `report`   — the F015 `RouteReport`; only `Routings` (the per-path `RoutingResult`) is
    ///                  read. `report.Diagnostics` is NOT consumed (the F017 precedent).
    ///   • `findings` — the F017 `FindingReport`, carried onto the result UNCHANGED (FR-005).
    ///
    /// Selection (FR-002, FR-003, FR-010):
    ///   • For each path classified `Routed (d, glob, _)`, select EVERY registry gate whose declared
    ///     `Domain` equals `d` (by id equality — the `GateId` string is NEVER re-parsed to recover a
    ///     domain, FR-010), recording `{Path; MatchedGlob = glob}` against each as a `SelectingPath`.
    ///   • The change's selected set is the UNION across all `Routed` paths, DEDUPLICATED by `GateId`:
    ///     a gate reached by several paths appears once, carrying all its selecting paths (FR-002).
    ///   • `UnmatchedInRoot` and `OutOfScope` paths select NO gate; there is no "select everything"
    ///     fallback (FR-003). A `Routed` path whose domain has no gate selects nothing and is not an
    ///     error. An `AmbiguousRoute` diagnostic on a `Routed` path does not change selection — the
    ///     already-resolved domain is used as-is (this feature re-resolves no ambiguity).
    ///
    /// Cost rollup (FR-006, research D5): a `CostRollup` multiset counting the DISTINCT selected gates
    /// per `Cost` tier (each shared gate counted once). An empty selection yields the all-zero
    /// identity — a valid success, not an error.
    ///
    /// Determinism (FR-007, SC-005): `SelectedGates` is sorted by `GateId` ordinal and each gate's
    /// `SelectingPaths` by normalized path ordinal; re-ordering the input candidate paths or the
    /// registry's gate list does not change the result. The carried `Findings` are placed unchanged
    /// in their already-deterministic F017 order (FR-005). Nothing in the result carries raw YAML,
    /// host paths, timestamps, severity, enforcement, a freshness verdict, or a ship verdict
    /// (FR-011, FR-012, SC-007).
    ///
    /// Totality (FR-008, FR-009): succeeds over any well-typed inputs — no error, no partial result.
    /// An empty `report.Routings`, an empty `registry`, or no `Routed` path reaching a gate's domain
    /// all yield an empty selected-gate set with the all-zero cost — a valid, successful empty route.
    /// Pure: no I/O, git, clock, severity, enforcement, freshness evaluation, execution, JSON, or CLI
    /// (FR-008, FR-011, FR-013).
    val select:
        registry: GateRegistry -> report: RouteReport -> findings: FindingReport -> RouteResult
