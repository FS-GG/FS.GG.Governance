// Curated public signature contract for the route entry point of path-to-capability routing
// (F015).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Routing.fs carries NO `private`/`internal`/`public` modifiers on
// top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Routing.fs body exists (Principle I). `route` is PURE and TOTAL (FR-011, research D7): no
// I/O, no git, no clock, never throws, and byte-for-byte identical for identical input
// (FR-012, SC-002). It assumes the typed facts are already valid per F014 and the candidate
// paths are already normalized; it does NOT re-parse YAML, re-normalize paths, or re-validate
// the catalog (FR-003, FR-014, research D8).

namespace FS.GG.Governance.Routing

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Routing =

    /// Route each candidate path to at most one capability domain, deterministically.
    ///
    /// Inputs: the F014 `TypedFacts` (the declared `ProjectFacts.GovernedRoot`, the
    /// `CapabilityFacts.Domains`, and the `CapabilityFacts.PathMap` of `glob → domain`) and a
    /// caller-supplied set of already-normalized candidate paths (research D8). Producing that
    /// path set from git/CI is a later feature (FR-011, FR-016).
    ///
    /// For each candidate path (FR-004, FR-007, FR-008):
    ///   • OUT OF SCOPE  — not under `GovernedRoot` (segment-prefix test) ⇒ `OutOfScope`.
    ///   • MATCHED       — one or more path-map globs match ⇒ `Routed` with the precedence
    ///                     winner: exact-literal › greater literal specificity › single-segment
    ///                     `*` over cross-segment `**` › ordinal glob tiebreak (FR-005). A path
    ///                     matching ≥1 glob is NEVER left unrouted.
    ///   • IN-ROOT MISS  — under `GovernedRoot` but no glob matches ⇒ `UnmatchedInRoot` (no
    ///                     finding/severity decided here — deferred, FR-007/FR-016).
    ///
    /// Diagnostics (FR-006, FR-009, FR-010):
    ///   • `AmbiguousRoute`         — emitted (with the still-deterministic winner) when the top
    ///                                two matching globs are co-specific (`Glob.isAmbiguousPair`).
    ///   • `ConflictingGlobBinding` — emitted when two path-map entries normalize to the same glob
    ///                                string but bind different domains (catalog-shape finding).
    ///   • `UnsupportedGlobSyntax`  — emitted for any path-map glob failing `Glob.checkSyntax`;
    ///                                such a glob is excluded from matching rather than silently
    ///                                never-matching.
    ///
    /// Determinism (FR-012, SC-002/SC-003): the returned `RouteReport` lists routings sorted by
    /// normalized path and diagnostics sorted by (id, path, glob); re-ordering the authored path
    /// map does not change the report. Nothing in the result carries raw YAML or product
    /// vocabulary beyond the declared domains (SC-005).
    val route: facts: TypedFacts -> candidatePaths: GovernedPath list -> RouteReport
