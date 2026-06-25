namespace FS.GG.Governance.RefreshJson

open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel

// The F057 refresh.json projection. Renders one already-decided `RefreshDecision` into the deterministic,
// versioned `refresh.json` document text via a hand-driven `System.Text.Json` `Utf8JsonWriter` walk — the
// net10.0 shared-framework mechanism the AuditJson/ReleaseJson/VerifyJson projections use, so NO new
// dependency. PURE and TOTAL (FR-007): no I/O, no git, no clock, never throws. Emit-only: it re-derives /
// re-classifies / re-sorts NOTHING (the `Loop` already fixed the outcome, per-view status, drifted
// categories, and declared order). No visibility modifiers — the surface is RefreshJson.fsi (Principle II).
// Each closed-enum `match` is EXHAUSTIVE with NO wildcard, so a future outcome/status case is a compile
// error here, never a silently mis-tokened field.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RefreshJson =

    let schemaVersion = "fsgg.refresh/v1"

    // ── internal writer plumbing (hidden — absent from RefreshJson.fsi) ──

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string. Default
    /// `Utf8JsonWriter` options ⇒ no indentation ⇒ deterministic, compact output (the shared `writeToString`
    /// precedent).
    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    // ── closed-enum token helpers (hidden) — each EXHAUSTIVE with NO wildcard ──

    let outcomeToken (outcome: RefreshOutcome) : string =
        match outcome with
        | NothingToRefresh -> "nothing-to-refresh"
        | ViewsRegenerated -> "views-regenerated"
        | StaleUnresolved' -> "stale-unresolved"
        | UsageError' -> "usage-error"
        | InputUnavailable -> "input-unavailable"
        | ToolError -> "tool-error"

    let statusToken (status: CurrencyStatus) : string =
        match status with
        | Current -> "current"
        | Regenerated _ -> "regenerated"
        | WouldRegenerate _ -> "would-regenerate"
        | StaleUnresolved _ -> "stale-unresolved"
        | NotEvaluated -> "not-evaluated"

    /// The `reason` for a `stale-unresolved` view, else `None` — the FR-016 reason is emitted ONLY for that
    /// status, never fabricated for a current/regenerated view.
    let reasonOf (status: CurrencyStatus) : string option =
        match status with
        | StaleUnresolved reason -> Some reason
        | Current
        | Regenerated _
        | WouldRegenerate _
        | NotEvaluated -> None

    // ── views[] (hidden) — one entry per view, in declared manifest order (never re-sorted) ──

    let writeView (w: Utf8JsonWriter) (view: ViewDecision) =
        w.WriteStartObject()
        w.WriteString("id", view.Entry.ViewId)
        w.WriteString("kind", viewKindToken view.Entry.Kind)
        w.WriteString("output", view.Entry.OutputPath)
        w.WriteString("status", statusToken view.Status)
        w.WritePropertyName "drifted"
        w.WriteStartArray()
        for cat in view.Drifted do
            w.WriteStringValue(categoryToken cat)
        w.WriteEndArray()

        match reasonOf view.Status with
        | Some reason -> w.WriteString("reason", reason)
        | None -> ()

        w.WriteEndObject()

    // ── the public entry point ──

    let ofRefreshDecision (decision: RefreshDecision) : string =
        // One linear walk, top-level object in the FIXED order schemaVersion → outcome → dryRun → summary →
        // views. Everything is carried VERBATIM from the decision value — re-deriving / re-sorting NOTHING;
        // `views` stays in the decision's declared order.
        writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WriteString("outcome", outcomeToken decision.Outcome)
            w.WriteBoolean("dryRun", decision.DryRun)

            w.WritePropertyName "summary"
            w.WriteStartObject()
            w.WriteNumber("regenerated", decision.RegeneratedCount)
            w.WriteNumber("current", decision.CurrentCount)
            w.WriteNumber("unresolved", decision.UnresolvedCount)
            w.WriteNumber("notEvaluated", decision.NotEvaluatedCount)
            w.WriteEndObject()

            w.WritePropertyName "views"
            w.WriteStartArray()
            for v in decision.Views do
                writeView w v
            w.WriteEndArray()

            w.WriteEndObject())
