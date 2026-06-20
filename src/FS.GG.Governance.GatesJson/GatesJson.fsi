// Curated public signature contract for the gates.json projection (F021).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching GatesJson.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here. Every JSON writer and closed-enum token helper lives ONLY in
// the .fs and is absent here, exactly as `FS.GG.Governance.Kernel.Json` and `FS.GG.Governance.RouteJson`
// keep their writer/token plumbing off their .fsi.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any GatesJson.fs
// body exists (Principle I). `ofGateRegistry` is the PURE, TOTAL projection (FR-008): it renders one
// already-typed, already-ordered F018 `GateRegistry` into the deterministic, versioned `gates.json`
// document text — the stable machine-readable WHOLE-CATALOG contract the later `fsgg` commands, CI,
// agents, generated readiness views, and humans read to learn what gates a repository declares,
// independent of any particular change. It performs no I/O, no git, no clock, never throws, and is
// byte-for-byte identical for identical input (FR-007, SC-002). It re-derives, re-sorts, and
// re-classifies nothing (the `GateRegistry` already fixed the gate order and every gate's carried
// order); it computes no severity, profile, enforcement, cache-eligibility verdict, per-change
// selection, or ship verdict (FR-011) and emits no raw YAML, host path, timestamp, or environment
// value (FR-012). Serialization uses the net10.0 shared-framework `System.Text.Json` — NO new
// `PackageReference` (FR-015).
//
// Sibling of F020 `FS.GG.Governance.RouteJson`: that projects the PER-CHANGE `RouteResult`; this
// projects the WHOLE-CATALOG `GateRegistry`. The per-gate field set here is the F020 `selectedGates[*]`
// entry MINUS the route-specific `selectingPaths`; the shared gate fields render identically.

namespace FS.GG.Governance.GatesJson

open FS.GG.Governance.Gates.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GatesJson =

    /// The declared schema-version token stamped into every emitted document and recorded as the
    /// document's `schemaVersion` field (FR-013), so consumers can branch on the contract version and
    /// detect changes without string-scraping the output. A fixed, deterministic constant
    /// (`"fsgg.gates/v1"`) — never derived from a clock, environment, or input value.
    val schemaVersion: string

    /// Project an F018 `GateRegistry` into its deterministic, versioned `gates.json` document text.
    ///
    /// Emits one top-level JSON object with fields in the FIXED order `schemaVersion`, `gates` (the
    /// wire contract is fixed in contracts/gates-json-document.md):
    ///   • `gates` — one object per `Gate` in the registry, in the registry's `GateId` ordinal order,
    ///               each carrying the gate's declared `id` (via `Gates.gateIdValue`, never re-parsed —
    ///               FR-010), `domain`, `description`, `cost`, `timeout`, `owner`, `maturity`,
    ///               `productCheck`, `prerequisites` (each `{ requiresCommand }`, in carried order,
    ///               present-and-empty when none — FR-004), and the carried `freshnessKey` INPUTS
    ///               (`{ check; domain; cost; environment; command }`, never a cache verdict — FR-014).
    ///               No gate absent from the registry appears, and no gate/prerequisite/cost/timeout/
    ///               freshness key is invented (FR-002, FR-003). The carried `timeout` and `maturity`/
    ///               `cost` render verbatim — no timeout re-derived (FR-006), no maturity-as-enforcement
    ///               and no weighted cost scalar (FR-005).
    ///
    /// PURE and TOTAL (FR-008, FR-009): no file, process, clock, network, or git access; never throws
    /// for any well-typed `GateRegistry`; an EMPTY registry (no declared checks) projects to a valid
    /// document with an empty `gates` array — a success, never an error and never a placeholder gate.
    /// DETERMINISTIC (FR-007, SC-002): identical registry inputs yield byte-for-byte identical text;
    /// the projection adds no ordering decision beyond the fixed field sequence, preserving the
    /// registry's already-fixed `GateId` order and each gate's carried order verbatim (so two
    /// registries equal as values but assembled from differently-ordered checks project identically —
    /// SC-003). The document carries NO severity, profile, mode, enforcement, cache-eligibility
    /// verdict, per-change gate selection, route trace, ship verdict, blocker, warning, exit-code
    /// basis, raw YAML, host/absolute path, timestamp, or environment value (FR-011, FR-012, SC-007).
    val ofGateRegistry: registry: GateRegistry -> string
