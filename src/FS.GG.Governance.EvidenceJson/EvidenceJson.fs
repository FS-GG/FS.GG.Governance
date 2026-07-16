namespace FS.GG.Governance.EvidenceJson

open System.Text.Json
open FS.GG.Governance.Kernel
open FS.GG.Governance.JsonText // 073: the shared deterministic-emit helper
open FS.GG.Governance.JsonWriters // 073: the shared sub-object/map writers (module-qualified)
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.EvidenceReuse.Model
// NOTE: NodeFreshness's `Fresh`/`Stale`/`Unresolved` cases collide by name with `Kernel.Freshness`
// (`Fresh`/`Stale`) and `FreshnessResolution.Model.ResolutionOutcome` (`Resolved`/`Unresolved`); the
// `NodeFreshness.` qualifier below keeps the match unambiguous. `FreshnessResolution.Model` is therefore
// deliberately NOT opened here (only `missingFactToken` from the `FreshnessResolution` module is needed).

// The wire model — the implementation of the types declared in EvidenceJson.fsi (the .fsi is the signature;
// the definitions live here). `Unresolved` carries a fully-qualified `MissingFact list` so the conflicting
// `FreshnessResolution.Model` open is avoided.
type NodeFreshness =
    | Fresh
    | Stale of cause: RecomputeCause
    | Unresolved of missing: FS.GG.Governance.FreshnessResolution.Model.MissingFact list
    | Unknown

type EvidenceNode =
    { Id: string
      Declared: EvidenceState
      Effective: EvidenceState
      Freshness: NodeFreshness
      Source: string }

type EvidenceContent =
    | WellFormed of nodes: EvidenceNode list * dependencies: (string * string) list
    | Malformed of failure: GraphError<string>

type EvidenceDocument =
    { Content: EvidenceContent
      Disclosures: (string * string) list }

// The 069 evidence.json projection (US1–US3). Renders an `EvidenceDocument` into the deterministic, versioned
// `evidence.json` PER-CHANGE effective-evidence document text via a hand-driven `System.Text.Json`
// `Utf8JsonWriter` walk — the net10.0 shared-framework mechanism the kernel's `Json.fs` and the sibling
// `RouteJson.fs` / `AuditJson.fs` / `CacheEligibilityJson.fs` projections use, so NO new dependency. PURE and
// TOTAL (FR-006/FR-007): no I/O, no git, no clock, never throws. Emit-only: it re-derives/re-classifies/
// re-runs NOTHING (the `EvidenceDocument` already fixed each node's declared/effective state, freshness cause,
// and any graph failure). It honours the feature's hard rules: declared AND effective are BOTH shown per node
// (FR-002); a malformed graph emits the named failure and NO per-node map (FR-004); a non-effective node names
// its freshness cause and `Unknown` is the only causeless freshness (FR-003). No visibility modifiers — the
// surface is EvidenceJson.fsi (Principle II); every token helper and sub-object writer below is hidden by its
// absence from the .fsi (the Kernel/Json.fs + AuditJson.fs precedent). Every `match` rendering a token is
// EXHAUSTIVE over its closed DU with NO wildcard, so a future EvidenceState / GraphError / NodeFreshness /
// RecomputeCause case is a compile error here, never a silently mis-tokened field.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceJson =

    let schemaVersion = "fsgg.evidence/v1"

    // ── internal writer plumbing (hidden — absent from EvidenceJson.fsi) ──

    // ── closed-DU token renderers (hidden) — exhaustive, NO wildcard ──

    /// The `EvidenceState` wire token — the closed six-case Kernel DU (FR-005). `Skipped` is a DISTINCT token
    /// from `Failed`/`Pending` (INV-2). Exhaustive: a future state is a compile error here.
    /// DIVERGENCE — DO NOT UNIFY: this `fsgg.evidence/v1` spelling is Capitalized
    /// (`Pending`/`Real`/`Synthetic`/…) and deliberately DIFFERS from the lowercase spelling
    /// (`pending`/`real`/`synthetic`/…) emitted by `Kernel.Json.stateToken` for the kernel
    /// effective-state contract. The two are independent emit contracts, so the strings DIVERGE:
    /// folding them into one shared `stateToken` would change this contract's bytes silently (and the
    /// kernel side additionally round-trips). This Capitalized spelling is pinned by
    /// EvidenceJson.Tests/ProjectionTests; the lowercase side by Kernel.Tests/JsonTests
    /// (cf. the `localOrCi`/`local-or-ci` divergence in JsonTokens).
    let stateToken (state: EvidenceState) : string =
        match state with
        | Pending -> "Pending"
        | Real -> "Real"
        | Synthetic -> "Synthetic"
        | Failed -> "Failed"
        | Skipped -> "Skipped"
        | AutoSynthetic -> "AutoSynthetic"

    /// The tagged `freshness` object (no-hide, FR-003) — `fresh` | `stale`+cause | `unresolved`+missing |
    /// `unknown`. `Unknown` is the only causeless freshness — an explicit honest null-equivalent, never a
    /// guessed `fresh`. Exhaustive over `NodeFreshness`.
    let writeFreshness (w: Utf8JsonWriter) (freshness: NodeFreshness) =
        w.WriteStartObject()

        match freshness with
        | NodeFreshness.Fresh -> w.WriteString("kind", "fresh")
        | NodeFreshness.Stale cause ->
            w.WriteString("kind", "stale")
            w.WritePropertyName "cause"
            JsonWriters.writeCause w cause
        | NodeFreshness.Unresolved missing ->
            w.WriteString("kind", "unresolved")
            w.WritePropertyName "missing"
            w.WriteStartArray()
            for fact in missing do
                w.WriteStringValue(FreshnessResolution.missingFactToken fact)
            w.WriteEndArray()
        | NodeFreshness.Unknown -> w.WriteString("kind", "unknown")

        w.WriteEndObject()

    /// One evidence node — field order `id`, `declared`, `effective`, `freshness`, `source`. Declared AND
    /// effective are BOTH emitted (FR-002); the `id`/`source` strings are carried verbatim (never re-parsed).
    let writeNode (w: Utf8JsonWriter) (node: EvidenceNode) =
        w.WriteStartObject()
        w.WriteString("id", node.Id)
        w.WriteString("declared", stateToken node.Declared)
        w.WriteString("effective", stateToken node.Effective)
        w.WritePropertyName "freshness"
        writeFreshness w node.Freshness
        w.WriteString("source", node.Source)
        w.WriteEndObject()

    /// The named `graphFailure` object (FR-004) — the closed `GraphError<string>` rendered by name. `Cycle`
    /// keeps its witness order; `UnknownNode`/`AutoSyntheticDeclared` name the offending node. Exhaustive.
    let writeGraphFailure (w: Utf8JsonWriter) (failure: GraphError<string>) =
        w.WriteStartObject()

        match failure with
        | Cycle cycle ->
            w.WriteString("kind", "cycle")
            w.WritePropertyName "nodes"
            w.WriteStartArray()
            for id in cycle do
                w.WriteStringValue id
            w.WriteEndArray()
        | UnknownNode node ->
            w.WriteString("kind", "unknownNode")
            w.WriteString("node", node)
        | AutoSyntheticDeclared node ->
            w.WriteString("kind", "autoSyntheticDeclared")
            w.WriteString("node", node)

        w.WriteEndObject()

    /// The `disclosures` array — one `{ rule, justification }` object per pair, sorted by `(rule, justification)`.
    let writeDisclosures (w: Utf8JsonWriter) (disclosures: (string * string) list) =
        w.WritePropertyName "disclosures"
        w.WriteStartArray()

        for rule, justification in disclosures |> List.sortBy id do
            w.WriteStartObject()
            w.WriteString("rule", rule)
            w.WriteString("justification", justification)
            w.WriteEndObject()

        w.WriteEndArray()

    // ── the public entry point ──

    let ofReport (document: EvidenceDocument) : string =
        // One linear walk writing the top-level object in its FIXED field order. The WELL-FORMED shape carries
        // a null `graphFailure`, the sorted `nodes`/`dependencies`, then `disclosures`; the MALFORMED shape
        // carries the named `graphFailure` and OMITS `nodes`/`dependencies` entirely (FR-004), then
        // `disclosures`. Every collection is rendered in a stable order; cause/missing token lists keep their
        // CORE order. PURE and TOTAL: the empty graph yields a present, empty `nodes` array — a valid success.
        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)

            match document.Content with
            | WellFormed(nodes, dependencies) ->
                w.WriteNull "graphFailure"

                w.WritePropertyName "nodes"
                w.WriteStartArray()
                for node in nodes |> List.sortBy (fun n -> n.Id) do
                    writeNode w node
                w.WriteEndArray()

                w.WritePropertyName "dependencies"
                w.WriteStartArray()
                for dependent, dependency in dependencies |> List.sortBy id do
                    w.WriteStartObject()
                    w.WriteString("dependent", dependent)
                    w.WriteString("dependency", dependency)
                    w.WriteEndObject()
                w.WriteEndArray()

            | Malformed failure ->
                w.WritePropertyName "graphFailure"
                writeGraphFailure w failure

            writeDisclosures w document.Disclosures
            w.WriteEndObject())
