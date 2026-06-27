// Curated public signature contract for the ADR-0002 evidence mapping (F081, US1).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Mapping.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// Design-first artifact: drafted in FSI before any Mapping.fs body exists (Principle I). PURE and
// TOTAL. It reproduces ADR-0002's accepted evidence mapping row-for-row (research D4, FR-014): the
// declared `DeclaredState` tokens map to `Kernel.EvidenceState` token-for-token; `Deferred`/
// `AcceptedDeferral` map to `Skipped` (FR-004); a `Stale = true` node carries a `StaleEvidence`
// diagnostic alongside its underlying mapped state (FR-006). The mapped graph is built with the kernel
// `Evidence.build` and taint-closed with `Evidence.effective` VERBATIM (FR-007) — the consumer reuses
// the domain-neutral kernel, it re-implements no taint rule.

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Kernel                   // EvidenceState
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Mapping =

    /// Map a declared evidence block to the kernel `Evidence.build` inputs: the `(id, EvidenceState)`
    /// node list and the carried `(dependent, dependency)` edge list, plus the per-node diagnostics
    /// (one `StaleEvidence` per `Stale = true` node — FR-006). The `Result` is `Error` only when the
    /// block itself cannot be mapped (defence-in-depth on a declared `autoSynthetic` token that slipped
    /// past `Reader.parse` — research D4); otherwise `Ok (nodes, deps)`. Total; never throws.
    val mapEvidence:
        source: string ->
        block: EvidenceBlock ->
            Result<(string * EvidenceState) list * (string * string) list, Diagnostic> * Diagnostic list

    /// Build the evidence graph from the mapped nodes/edges and compute the transitive synthetic-taint
    /// closure (`Evidence.build` then `Evidence.effective`). A `Failed` or `AutoSynthetic` EFFECTIVE
    /// state makes the derived evidence gate blocking-capable (research D3/D4). Returns `Error` with a
    /// `Diagnostic` when `Evidence.build` refuses the graph (e.g. a declared `AutoSynthetic` — defence
    /// in depth). Total; never throws.
    val effectiveStates:
        nodes: (string * EvidenceState) list ->
        deps: (string * string) list ->
            Result<Map<string, EvidenceState>, Diagnostic>
