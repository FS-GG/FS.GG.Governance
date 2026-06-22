namespace FS.GG.Governance.CacheEligibilityJson

open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility

// The F042 cache-eligibility.json projection (US1–US4). Renders the F041 `CacheEligibilityReport` into the
// deterministic, versioned `cache-eligibility.json` PER-CHANGE cache-eligibility document text via a
// hand-driven `System.Text.Json` `Utf8JsonWriter` walk — the net10.0 shared-framework mechanism the kernel's
// `Json.fs` and F020's `RouteJson.fs` / F021's `GatesJson.fs` / F025's `AuditJson.fs` use, so NO new
// dependency (FR-014). PURE and TOTAL (FR-007/FR-008): no I/O, no git, no clock, no cache lookup against a
// real store, no freshness key/hash, no input resolved, the opaque evidence reference never dereferenced,
// never throws. Emit-only: it re-derives/re-classifies/re-runs/re-orders NOTHING (the `CacheEligibilityReport`
// already fixed each per-gate verdict and the `GateId`-ordinal entry order with its structural duplicate
// tiebreak). It honours F041's two hard rules: every `mustRecompute` entry names its cause (no-hide, FR-004),
// and a `reusable` entry carries only its opaque evidence reference (necessary-not-sufficient, FR-003). No
// visibility modifiers — the surface is CacheEligibilityJson.fsi (Principle II); every token helper and
// sub-object writer below is hidden by its absence from the .fsi, the `Kernel/Json.fs` + `AuditJson.fs`
// precedent.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CacheEligibilityJson =

    /// The declared schema-version token stamped into every emitted document (FR-013). A fixed,
    /// deterministic string literal — never derived from a clock, environment, or input value.
    let schemaVersion = "fsgg.cache-eligibility/v1"

    // ── internal writer plumbing (hidden — absent from CacheEligibilityJson.fsi) ──

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string. Default
    /// `Utf8JsonWriter` options ⇒ no indentation ⇒ deterministic, compact output (the `Json.fs` `writeToString`
    /// precedent shared by F020/F021/F025).
    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    // ── tagged sub-object writers (hidden) — each emits its documented field order verbatim ──
    // Every `match` rendering a token is EXHAUSTIVE over the closed DU with NO wildcard (research D4), so a
    // future verdict/cause case is a compile error here, never a silently mis-tokened field. The IDENTITY/token
    // renderers are REUSED from public upstream: `gateIdValue` (F018), `referenceValue` (F030),
    // `categoryToken` (F029).

    /// The tagged `cause` object (no-hide, FR-004) — field order `kind`, then `categories` for `inputsChanged`.
    /// `NoPriorEvidence` ⇒ `{ kind:"noPriorEvidence" }` (NO `categories` field — distinct from
    /// `inputsChanged []`, FR-006). `InputsChanged cats` ⇒ `{ kind:"inputsChanged", categories:[…] }`, the
    /// categories named via `categoryToken` in the report's order — none dropped, added, or truncated.
    let writeCause (w: Utf8JsonWriter) (cause: RecomputeCause) =
        w.WriteStartObject()

        match cause with
        | NoPriorEvidence -> w.WriteString("kind", "noPriorEvidence")
        | InputsChanged cats ->
            w.WriteString("kind", "inputsChanged")
            w.WritePropertyName "categories"
            w.WriteStartArray()
            for c in cats do
                w.WriteStringValue(categoryToken c)
            w.WriteEndArray()

        w.WriteEndObject()

    /// The tagged `verdict` object — field order `kind`, then payload. `Reusable ref` ⇒ `{ kind:"reusable",
    /// evidence:<referenceValue ref> }` (only `kind` + the opaque reference verbatim, never parsed/dereferenced
    /// — FR-003). `MustRecompute cause` ⇒ `{ kind:"mustRecompute", cause:<cause-object> }` (always a cause, no
    /// `evidence` field — FR-004).
    let writeVerdict (w: Utf8JsonWriter) (verdict: CacheEligibilityVerdict) =
        w.WriteStartObject()

        match verdict with
        | Reusable ref ->
            w.WriteString("kind", "reusable")
            w.WriteString("evidence", EvidenceReuse.referenceValue ref)
        | MustRecompute cause ->
            w.WriteString("kind", "mustRecompute")
            w.WritePropertyName "cause"
            writeCause w cause

        w.WriteEndObject()

    /// One entry — field order `gate`, `verdict`. `gate` is `gateIdValue entry.Gate` verbatim (FR-010), never
    /// re-parsed even across a `:` separator.
    let writeEntry (w: Utf8JsonWriter) (entry: CacheEligibilityEntry) =
        w.WriteStartObject()
        w.WriteString("gate", gateIdValue entry.Gate)
        w.WritePropertyName "verdict"
        writeVerdict w entry.Verdict
        w.WriteEndObject()

    // ── the public entry point ──

    let ofReport (report: CacheEligibilityReport) : string =
        // One linear walk of the already-ordered `CacheEligibilityReport`, writing the top-level object in the
        // FIXED order schemaVersion → entries. The entries are emitted in the report's existing `GateId`-ordinal
        // order (with F041's structural duplicate tiebreak), re-sorting NOTHING (FR-005). Each verdict and its
        // payload is carried VERBATIM from the entry value — no reuse decision re-run, no cache lookup, no
        // freshness key/hash, no input resolved, the opaque evidence reference never dereferenced (FR-002/
        // FR-008). PURE and TOTAL: the empty report yields a present, empty `entries` array — a valid success
        // (FR-009).
        writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WritePropertyName "entries"
            w.WriteStartArray()
            for entry in CacheEligibility.entries report do
                writeEntry w entry
            w.WriteEndArray()
            w.WriteEndObject())
