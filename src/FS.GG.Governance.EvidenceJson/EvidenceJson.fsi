// Curated public signature contract for the effective-evidence evidence.json projection (069).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// EvidenceJson.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here. Every JSON writer and closed-enum token helper lives ONLY in the .fs and is absent
// here, exactly as `FS.GG.Governance.Kernel.Json`, `FS.GG.Governance.RouteJson`, `FS.GG.Governance.AuditJson`,
// and `FS.GG.Governance.CacheEligibilityJson` keep their writer/token plumbing off their .fsi.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any EvidenceJson.fs body
// exists (Principle I). `ofReport` is the PURE, TOTAL projection (FR-006/FR-007): it renders one already-typed
// `EvidenceDocument` into the deterministic, versioned `evidence.json` document text — the stable,
// machine-readable PER-CHANGE effective-evidence contract the readiness views, CI dashboards, agents, and
// humans read instead of an in-memory value. It performs no I/O, no git, no clock, never throws, and is
// byte-for-byte identical for identical input (FR-006, SC-002).
//
// Sibling of `FS.GG.Governance.RouteJson` / `GatesJson` / `AuditJson` / `CacheEligibilityJson`: this projects
// the per-change EFFECTIVE-EVIDENCE world. It honours the feature's hard rules: declared AND effective are
// BOTH shown per node — taint surfaces as the delta, never a silent overwrite (FR-002); a malformed graph
// surfaces the named `GraphError` and OMITS the per-node map (FR-004); a non-effective node names *why* via
// its freshness cause, and `Unknown` is the only causeless freshness — never a guessed `Fresh` (FR-003).
//
// LEAF (research D7): references ONLY `FS.GG.Governance.Kernel` (`EvidenceState`, `GraphError<'id>`) plus the
// freshness-cause vocabulary `FS.GG.Governance.FreshnessResolution` (`MissingFact`) and
// `FS.GG.Governance.EvidenceReuse` (`RecomputeCause`); `InputCategory` arrives transitively. NO host/Cli edge,
// so no cycle. Disclosures are carried as already-rendered `(rule, justification)` string pairs so the leaf
// stays free of any `Host` reference.

namespace FS.GG.Governance.EvidenceJson

open FS.GG.Governance.Kernel
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.FreshnessResolution.Model

/// Per-node freshness with a no-hide cause (FR-003). `Stale`/`Unresolved` name *why* a node is not current;
/// `Unknown` is an honest null-equivalent for a node with no joinable freshness signal — NEVER a guessed
/// `Fresh`, and never a fabricated cause.
type NodeFreshness =
    /// The node's recorded evidence still describes its current artifacts.
    | Fresh
    /// The node is stale; `cause` names why (`noPriorEvidence` | `inputsChanged categories`).
    | Stale of cause: RecomputeCause
    /// The node's freshness could not be resolved; `missing` NON-EMPTY, each named via `missingFactToken`.
    | Unresolved of missing: MissingFact list
    /// No joinable freshness signal — an explicit honest null, never a guessed `Fresh`.
    | Unknown

/// One evidence node in a well-formed graph. `Declared` AND `Effective` are BOTH present (FR-002): taint
/// surfaces as the delta between them, never as a silent overwrite of `Declared`.
type EvidenceNode =
    { Id: string
      Declared: EvidenceState
      Effective: EvidenceState
      Freshness: NodeFreshness
      Source: string }

/// The well-formed/malformed content split. A graph failure means the per-node effective map is NOT emitted
/// (FR-004): the document carries the named failure INSTEAD of a partial/guessed map.
type EvidenceContent =
    | WellFormed of nodes: EvidenceNode list * dependencies: (string * string) list
    | Malformed of failure: GraphError<string>

/// The complete value `ofReport` renders. `schemaVersion` is stamped by the projection, not carried here.
/// `Disclosures` are already-rendered `(rule, justification)` pairs carried through from the report; `[]` when
/// none.
type EvidenceDocument =
    { Content: EvidenceContent
      Disclosures: (string * string) list }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceJson =

    /// The declared schema-version token stamped into every emitted document and recorded as the document's
    /// `schemaVersion` field (FR-001), so consumers can branch on the contract version. A fixed, deterministic
    /// constant (`"fsgg.evidence/v1"`) — never derived from a clock, environment, or input value.
    val schemaVersion: string

    /// Project an `EvidenceDocument` into its deterministic, versioned `evidence.json` document text.
    ///
    /// Emits one top-level JSON object. WELL-FORMED graph (field order `schemaVersion`, `graphFailure`,
    /// `nodes`, `dependencies`, `disclosures`): `graphFailure` is `null`; `nodes` ascending by `id`, each with
    /// BOTH `declared` and `effective` `EvidenceState` tokens plus its `freshness` object and `source`;
    /// `dependencies` sorted by `(dependent, dependency)`; `disclosures` sorted by `(rule, justification)`.
    /// MALFORMED graph (field order `schemaVersion`, `graphFailure`, `disclosures`): `graphFailure` is the named
    /// failure object and `nodes`/`dependencies` are OMITTED ENTIRELY (FR-004, SC-003) — never empty arrays,
    /// never a partial map.
    ///
    /// PURE and TOTAL (FR-006/FR-007): no file, process, clock, network, or git access; an EXHAUSTIVE
    /// wildcard-free token match over every closed DU (`EvidenceState`, `GraphError`, `NodeFreshness`,
    /// `RecomputeCause`); never throws for any well-typed `EvidenceDocument`; an EMPTY graph
    /// (`WellFormed ([], [])`) projects to a valid document `{ …, "nodes": [], … }` — a success, never an error.
    /// DETERMINISTIC (FR-006, SC-002): identical documents yield byte-for-byte identical text; every collection
    /// is rendered in a stable order; category/missing token lists keep their core order (none dropped, added,
    /// or truncated). The document carries NO wall-clock timestamp, host/absolute path, environment value,
    /// numeric exit code, severity, ship verdict, exit-code basis, or provenance reference.
    val ofReport: document: EvidenceDocument -> string
