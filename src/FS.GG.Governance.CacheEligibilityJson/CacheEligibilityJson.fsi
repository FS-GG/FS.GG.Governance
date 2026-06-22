// Curated public signature contract for the cache-eligibility.json projection (F042).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching CacheEligibilityJson.fs carries NO `private`/`internal`/`public` modifiers on top-level
// bindings — visibility is presence/absence here. Every JSON writer and closed-enum token helper lives ONLY
// in the .fs and is absent here, exactly as `FS.GG.Governance.Kernel.Json`, `FS.GG.Governance.RouteJson`,
// `FS.GG.Governance.GatesJson`, and `FS.GG.Governance.AuditJson` keep their writer/token plumbing off their
// .fsi.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any CacheEligibilityJson.fs
// body exists (Principle I). `ofReport` is the PURE, TOTAL projection (FR-007/FR-008): it renders one
// already-typed, already-ordered F041 `CacheEligibilityReport` into the deterministic, versioned
// `cache-eligibility.json` document text — the stable, machine-readable PER-CHANGE cache-eligibility contract
// the later route/audit emission rows, CI cost dashboards, agents, generated readiness views, and humans read
// instead of an in-memory value. It performs no I/O, no git, no clock, never throws, and is byte-for-byte
// identical for identical input (FR-007, SC-002). It re-derives, re-classifies, re-runs, and re-orders nothing
// (the `CacheEligibilityReport` already fixed each per-gate verdict and the `GateId`-ordinal entry order with
// its structural duplicate tiebreak); it re-runs no reuse decision (FR-002), makes no cache lookup against a
// real store, computes no freshness key or hash, resolves none of the inputs, never dereferences the opaque
// evidence reference (FR-008), maps no numeric process exit code, and emits no severity, ship verdict, host
// path, timestamp, environment value, or provenance reference (FR-012). Serialization uses the net10.0
// shared-framework `System.Text.Json` — NO new `PackageReference` (FR-014).
//
// Sibling of F020 `FS.GG.Governance.RouteJson` (the per-change route view), F021
// `FS.GG.Governance.GatesJson` (the whole-catalog gate view), and F025 `FS.GG.Governance.AuditJson` (the
// whole-change ship verdict): this projects the per-change CACHE-ELIGIBILITY verdict (`CacheEligibilityReport`).
// It honours F041's two hard rules: every `mustRecompute` entry names its cause (the no-hide rule, FR-004),
// and a `reusable` entry asserts only "prior evidence may be reused" — it carries no skip action, severity,
// ship verdict, or exit-code basis (necessary-not-sufficient, FR-003).

namespace FS.GG.Governance.CacheEligibilityJson

open FS.GG.Governance.CacheEligibility.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CacheEligibilityJson =

    /// The declared schema-version token stamped into every emitted document and recorded as the document's
    /// `schemaVersion` field (FR-013), so consumers can branch on the contract version and detect changes
    /// without string-scraping the output. A fixed, deterministic constant (`"fsgg.cache-eligibility/v1"`) —
    /// never derived from a clock, environment, or input value.
    val schemaVersion: string

    /// Project an F041 `CacheEligibilityReport` into its deterministic, versioned `cache-eligibility.json`
    /// document text.
    ///
    /// Emits one top-level JSON object with fields in the FIXED order `schemaVersion`, `entries` (the wire
    /// contract is fixed in contracts/cache-eligibility-json-document.md):
    ///   • `schemaVersion` — the fixed constant above (FR-013), never derived from a clock/environment/input.
    ///   • `entries`       — `CacheEligibility.entries report`, one entry per report entry in the report's
    ///                       existing `GateId`-ordinal order (with F041's structural duplicate tiebreak)
    ///                       preserved verbatim, re-sorting nothing (FR-001/FR-005). ALWAYS PRESENT; an empty
    ///                       report renders as `"entries": []` (FR-009) — never omitted, never a placeholder.
    ///                       Each entry carries `gate` (the declared `GateId` via `gateIdValue`, never
    ///                       re-parsed even across a `:` separator — FR-010) then a tagged `verdict` object:
    ///                       a `Reusable ref` renders `{ kind:"reusable", evidence:<referenceValue ref> }`
    ///                       (only `kind` + the opaque evidence reference verbatim — FR-003); a
    ///                       `MustRecompute cause` renders `{ kind:"mustRecompute", cause:<cause-object> }`
    ///                       where the cause is `{ kind:"noPriorEvidence" }` or `{ kind:"inputsChanged",
    ///                       categories:[<categoryToken c> …] }` naming exactly the changed categories in the
    ///                       report's order — none dropped, added, or truncated (the no-hide rule, FR-004).
    ///                       `noPriorEvidence` (no `categories` field) is DISTINCT from `inputsChanged` with
    ///                       `categories: []` (FR-006).
    ///
    /// PURE and TOTAL (FR-007/FR-008): no file, process, clock, network, or git access; no cache lookup against
    /// a real store; no freshness key/hash computed; none of the inputs resolved; the opaque evidence reference
    /// rendered verbatim but never parsed or dereferenced; never throws for any well-typed
    /// `CacheEligibilityReport`; an EMPTY report (`CacheEligibilityReport []`) projects to a valid document
    /// `{ schemaVersion, entries: [] }` — a success, never an error and never a "must recompute by default"
    /// placeholder entry. DETERMINISTIC (FR-007, SC-002): identical report inputs yield byte-for-byte identical
    /// text; the projection adds no ordering decision beyond the fixed field sequence, preserving the report's
    /// already-fixed entry order verbatim (so two reports equal as values but assembled from differently-ordered
    /// candidate inputs project identically — SC-003). The document carries NO wall-clock timestamp,
    /// host/absolute path, raw freshness input, computed freshness key or hash, environment value, numeric
    /// process exit code, severity, ship verdict, exit-code basis, or provenance/attestation reference (FR-012,
    /// SC-007).
    val ofReport: report: CacheEligibilityReport -> string
