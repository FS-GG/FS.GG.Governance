// Curated public signature contract for the route-domain types of route gate selection (F019).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, YAML-free values the `Route.select` join
// returns: the deterministic selected-gate set (each gate annotated with WHY it was selected), the
// carried F017 findings, and the rolled-up route cost. They REUSE the upstream typed values rather
// than redefining them — the F018 `Gate`/`GateRegistry`, the F014 `GovernedPath`/`Cost`, and the F017
// `FindingReport` — because this feature consumes already-typed F015/F017/F018 outputs; it re-parses
// no YAML, re-routes no globs, re-builds no registry, and re-classifies no finding (FR-008, FR-012).
// Every emitted collection is in deterministic ordinal order (FR-007, SC-005). No field carries raw
// YAML, host paths, timestamps, severity, enforcement, freshness verdict, or ship verdict (FR-011,
// FR-012, SC-007).

namespace FS.GG.Governance.Route

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── Why a gate was selected (the route reason, FR-004) ──

    /// One changed path that `Routed` to a selected gate's domain, paired with the matching glob it
    /// won on (the F015 `Routed` `matchedGlob` — the "rule"). The "why this gate" link from a changed
    /// path to a selected gate. Both fields are F014-normalized declared `GovernedPath`s — never host
    /// paths or raw YAML (FR-012). A single gate reached by several paths carries several of these,
    /// sorted by normalized `Path` ordinal (FR-007).
    type SelectingPath =
        { Path: GovernedPath
          MatchedGlob: GovernedPath }

    // ── One selected gate with its route trace (key entity "Selected gate", FR-002/FR-004) ──

    /// One registry `Gate` a change selected, annotated with the route trace explaining the
    /// selection. The embedded F018 `Gate` carries the declared `Id` (`GateId`), `Domain`, `Cost`,
    /// and the rest of the *Gate identities* metadata VERBATIM — this feature re-derives none of it
    /// (FR-010, FR-012). `SelectingPaths` names every `Routed` path that reached the gate's domain and
    /// the glob each won on, deduplicated onto this single gate entry (one gate, however many paths
    /// reached it — FR-002) and sorted by normalized path ordinal (FR-007). A gate's declared
    /// `FreshnessKey` is carried (inside `Gate`) but NEVER evaluated here (FR-011).
    type SelectedGate =
        { Gate: Gate
          SelectingPaths: SelectingPath list }

    // ── The rolled-up route cost (key entity, FR-006, research D5) ──

    /// The total declared cost of a route as a MULTISET of the closed `Cost` tiers: the count of
    /// DISTINCT selected gates in each tier (a gate reached by several paths counted once, FR-006).
    /// A multiset — not a summed scalar — because F014's `Cost` is a closed ordered class with NO
    /// declared numeric weights; summing would invent magnitudes F014 never states (research D5, the
    /// same "no invented semantics" call F018 made for prerequisite edges). The identity/zero
    /// aggregate (an empty selection) is all-zero counts, a valid successful route, never an error
    /// (FR-006, FR-009). Deterministic: identical inputs yield identical counts (SC-004).
    /// Phase 11 (cost & cache) MAY refine this into a weighted total once weights are declared.
    type CostRollup =
        { Cheap: int
          Medium: int
          High: int
          Exhaustive: int }

    // ── The aggregate result (key entity "Route result / route trace", FR-001/FR-005/FR-007) ──

    /// The deterministic route trace: the selected gates (sorted by `GateId` ordinal so identical
    /// inputs yield a byte-identical list and re-ordering the input paths or the registry's gates
    /// never changes it — FR-007, SC-005), the F017 `FindingReport` CARRIED THROUGH UNCHANGED (the
    /// single value that explains both what runs and what is unclassified — FR-005), and the rolled-up
    /// `Cost`. The source of route.json's *selected gates* / *matched rules* / *unmatched governed
    /// paths* / *cost* fields. An EMPTY `SelectedGates` is a valid, successful outcome — never an
    /// error and never a "select everything" fallback (FR-003, FR-009). No severity, enforcement,
    /// freshness verdict, or ship verdict (FR-011, SC-007).
    type RouteResult =
        { SelectedGates: SelectedGate list
          Findings: FindingReport
          Cost: CostRollup }
