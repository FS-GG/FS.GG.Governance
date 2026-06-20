// Curated public signature contract for the classifier entry point of unknown-governed-path
// findings (F017).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Findings.fs carries NO `private`/`internal`/`public` modifiers on
// top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Findings.fs body exists (Principle I). `findUnknownGovernedPaths` is PURE and TOTAL (FR-011,
// FR-012): no I/O, no git, no clock, never throws, and byte-for-byte identical for identical
// input (FR-009, SC-004). It consumes the already-typed F014 facts and the F015 routing
// outcomes; it re-parses no `.fsgg` YAML, re-normalizes no path, re-validates no catalog, and
// senses no git (FR-011, FR-014). This is the decision F015 deferred (its `UnmatchedInRoot`
// "carries no domain and asserts no finding/severity"); F017 makes it.

namespace FS.GG.Governance.Findings

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Findings =

    /// Decide, for each routed candidate path, whether it is an unknown-governed-path finding.
    ///
    /// Inputs: the F014 `TypedFacts` (only `Capabilities.Surfaces` is read — for the declared
    /// `Routine` suppressors and `ProtectedSurface` escalators) and the F015 `RouteReport` whose
    /// `Routings` already classified every candidate path as `Routed` / `UnmatchedInRoot` /
    /// `OutOfScope`. The report's `Diagnostics` are NOT consumed. Surface membership is decided on
    /// the normalized `GovernedPath` form by the same segment-prefix relation routing used for the
    /// governed root (FR-014); this function does NOT re-derive the governed root — it trusts the
    /// routing outcome (edge case "Governed root is a subdirectory").
    ///
    /// For each candidate path, by its routing outcome:
    ///   • `Routed`          — NEVER a finding, even on a protected boundary; this feature flags
    ///                         only unclassified paths (FR-005).
    ///   • `OutOfScope`      — NEVER a finding; no global default-deny (FR-003).
    ///   • `UnmatchedInRoot` — classified against the declared surfaces by the documented
    ///                         precedence (contracts/precedence.md, FR-002/FR-004/FR-006/FR-007):
    ///                           1. within a declared `ProtectedSurface` ⇒ a finding with
    ///                              `Id = UnknownProtectedBoundaryPath` and
    ///                              `Zone = ProtectedBoundaryUnknown sid` (escalate; protected
    ///                              OUTRANKS routine — `Protected > Routine > Ordinary`);
    ///                           2. else within a declared `Routine` surface ⇒ NO finding
    ///                              (suppressed — an explicitly-declared unmanaged region);
    ///                           3. else ⇒ a finding with `Id = UnknownGovernedPath` and
    ///                              `Zone = GovernedRootUnknown` (ordinary).
    ///
    /// Plane uniformity & dedup (FR-010, SC-007): the decision is purely path+surface keyed, so an
    /// unclassified in-root path yields the same finding whichever F016 plane it came from; a path
    /// present more than once in `Routings` collapses to a SINGLE finding (documented dedup:
    /// group by normalized path, keep the ordinal-first routing). The plane does not enter the
    /// decision and is not retained on the finding in this MVP (FR-010 "MAY be retained").
    ///
    /// Determinism (FR-009, SC-004): `Findings` is sorted by normalized path (ordinal) then
    /// finding-id token; re-ordering the input paths or the authored surface declarations does not
    /// change the result. An empty result is a valid success, not an error (FR-012). Nothing in a
    /// finding carries raw YAML, host paths, timestamps, or product vocabulary beyond declared
    /// domain/surface ids (FR-008, SC-006).
    val findUnknownGovernedPaths: facts: TypedFacts -> report: RouteReport -> FindingReport
