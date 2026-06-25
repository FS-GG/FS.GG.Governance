// Curated public signature contract for the F057 refresh.json projection.
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching RefreshJson.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here. Every `Utf8JsonWriter` walk and closed-enum token helper lives
// ONLY in the .fs, exactly as `AuditJson` / `ReleaseJson` / `VerifyJson` keep their writer/token plumbing
// off their .fsi.
//
// `ofRefreshDecision` is the PURE, TOTAL projection (FR-007): it renders one already-decided
// `RefreshDecision` into the deterministic, versioned `refresh.json` document text — the stable,
// machine-readable refresh contract CI, dashboards, agents, and humans read instead of an in-memory value.
// It performs no I/O, no git, no clock, never throws, and is byte-for-byte identical for identical input
// (FR-007, SC-004). It re-derives, re-classifies, and re-sorts NOTHING (the `Loop` already fixed the
// outcome, per-view status, drifted categories, and declared order); it maps no numeric process exit code
// and emits no host path, timestamp, or environment value. Serialization uses the net10.0 shared-framework
// `System.Text.Json` — NO new `PackageReference`. The drifted categories render via the F029
// `FreshnessKey.categoryToken` verbatim.

namespace FS.GG.Governance.RefreshJson

open FS.GG.Governance.RefreshJson.RefreshModel

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RefreshJson =

    /// The declared schema-version token stamped into every emitted document as `schemaVersion`. A fixed,
    /// deterministic constant (`"fsgg.refresh/v1"`) — never derived from a clock, environment, or input.
    val schemaVersion: string

    /// Project a `RefreshDecision` into the deterministic, versioned `refresh.json` document text.
    ///
    /// Emits one top-level JSON object with fields in the FIXED order `schemaVersion`, `outcome`, `dryRun`,
    /// `summary`, `views` (the wire contract fixed in contracts/refresh.schema.md):
    ///   • `schemaVersion` — the fixed constant above.
    ///   • `outcome`       — the run's category token, carried VERBATIM (never mapped to a numeric exit code).
    ///   • `dryRun`        — `decision.DryRun`, VERBATIM.
    ///   • `summary`       — `{ regenerated, current, unresolved, notEvaluated }`, the decision's counts.
    ///   • `views`         — one entry per view, in DECLARED manifest order (never re-sorted). Each carries
    ///                       `id`, `kind` (`viewKindToken`), `output`, `status`
    ///                       (`current`/`regenerated`/`would-regenerate`/`stale-unresolved`/`not-evaluated`),
    ///                       `drifted` (the `categoryToken` list — omitted/empty when not stale), and
    ///                       `reason` (present ONLY for `stale-unresolved`, FR-016). A `stale-unresolved`
    ///                       view is NEVER coerced to `current` (FR-010).
    ///
    /// PURE and TOTAL (FR-007): no file, process, clock, network, or git access; never throws.
    /// DETERMINISTIC (SC-004): identical input yields byte-for-byte identical text; the document carries NO
    /// wall-clock timestamp, host/absolute path, environment value, or numeric process exit code.
    val ofRefreshDecision: decision: RefreshDecision -> string
