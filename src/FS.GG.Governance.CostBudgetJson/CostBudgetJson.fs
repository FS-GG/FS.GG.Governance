namespace FS.GG.Governance.CostBudgetJson

open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget.Findings

// The F25 cost-budget.json projection (US2). Renders the budgeted `CacheDecisionReport` + the cost/cache
// findings into the deterministic, versioned `fsgg.cost-budget/v1` document via a hand-driven
// `System.Text.Json` `Utf8JsonWriter` walk — the net10.0 shared-framework mechanism every `*Json` projection
// uses, so NO new dependency. PURE and TOTAL: no I/O, no git, no clock, the opaque `EvidenceRef` rendered
// verbatim, never throws. Emit-only: it re-derives/re-orders NOTHING structural (the report is already
// GateId-ordinal, the findings already (GateId, kind)-sorted). The ONE documented exception (JSON-5): the
// `overBudget.reason` string is a HUMAN MESSAGE composed here at emit time from closed-enum wire tokens
// (`gateIdValue` + `costToken`) — see `writeDecision`. It is deliberately NOT carried on `BudgetReason`: the
// model is wire-independent, and threading the string up would push the JSON token vocabulary (`costToken`)
// into the pure domain core `Budget.fs`, a worse coupling than composing it once at this projection edge. The
// synthesis is still deterministic, closed-enum, and byte-identical for identical input (no clock/env/float/
// culture), so it re-derives no FACT — the underlying cause lives in the findings section, not here. Every
// token `match` is EXHAUSTIVE with no wildcard, so a future DU case is a compile error here. No access
// modifiers — the surface is CostBudgetJson.fsi (Principle II).

open FS.GG.Governance.JsonText // 073: the shared deterministic-emit helper JsonText.writeToString
open FS.GG.Governance.JsonTokens // 073: the shared closed-enum token helpers (module-qualified)
open FS.GG.Governance.JsonWriters // 073: the shared sub-object/map writers (module-qualified)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CostBudgetJson =

    let schemaVersion = "fsgg.cost-budget/v1"

    // ── internal writer plumbing (hidden — absent from CostBudgetJson.fsi) ──

    // ── closed-enum token helpers (exhaustive, no wildcard) ──

    let reviewToken (review: AgentReviewMark) : string =
        match review with
        | Deterministic -> "deterministic"
        | AgentReviewed _ -> "agentReviewed"

    let deferralToken (cls: DeferralClass) : string =
        match cls with
        | Skipped -> "skipped"
        | Deferred -> "deferred"

    let findingKindToken (kind: CostFindingKind) : string = kindToken kind

    // ── tagged sub-object writers (hidden) — each emits its documented field order verbatim ──

    /// The tagged `decision` object — exactly one shape. `overBudget` carries `class`, `ceiling`, and a
    /// deterministic `reason` string (the underlying cause is surfaced in the findings section, not here).
    /// `reason` is the ONE emit-time composition in this projection (JSON-5, see file header): a human message
    /// built from closed-enum wire tokens, intentionally NOT carried on the wire-independent `BudgetReason`.
    let writeDecision (w: Utf8JsonWriter) (gate: GateId) (decision: CacheDecision) =
        w.WriteStartObject()

        match decision with
        | Reuse evidence ->
            w.WriteString("kind", "reuse")
            w.WriteString("evidence", EvidenceReuse.referenceValue evidence)
        | Recompute cause ->
            w.WriteString("kind", "recompute")
            w.WritePropertyName "cause"
            JsonWriters.writeCause w cause
        | OverBudget reason ->
            w.WriteString("kind", "overBudget")
            w.WriteString("class", deferralToken reason.Class)
            w.WriteString("ceiling", JsonTokens.costToken reason.Ceiling)

            let reasonText =
                sprintf
                    "%s (%s) exceeds the %s budget"
                    (gateIdValue gate)
                    (JsonTokens.costToken reason.Cost)
                    (JsonTokens.costToken reason.Ceiling)

            w.WriteString("reason", reasonText)

        w.WriteEndObject()

    /// One decision entry — field order `gate`, `cost`, `review`, `decision`.
    let writeEntry (w: Utf8JsonWriter) (entry: CacheDecisionEntry) =
        w.WriteStartObject()
        w.WriteString("gate", gateIdValue entry.Gate)
        w.WriteString("cost", JsonTokens.costToken entry.Cost)
        w.WriteString("review", reviewToken entry.Review)
        w.WritePropertyName "decision"
        writeDecision w entry.Gate entry.Decision
        w.WriteEndObject()

    /// One finding — field order `gate`, `kind`, `baseSeverity`, `categories` (ONLY for `stale`), `message`.
    let writeFinding (w: Utf8JsonWriter) (finding: CostFinding) =
        w.WriteStartObject()
        w.WriteString("gate", gateIdValue finding.Gate)
        w.WriteString("kind", findingKindToken finding.Kind)
        w.WriteString("baseSeverity", JsonTokens.severityToken finding.BaseSeverity)

        match finding.Kind with
        | Stale cats ->
            w.WritePropertyName "categories"
            w.WriteStartArray()
            for c in cats do
                w.WriteStringValue(categoryToken c)
            w.WriteEndArray()
        | SyntheticTaint
        | NoEvidence -> ()

        w.WriteString("message", finding.Message)
        w.WriteEndObject()

    // ── the public entry point ──

    let ofReport (report: CacheDecisionReport) (findings: CostFinding list) : string =
        let (CacheDecisionReport entries) = report

        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)

            w.WritePropertyName "decisions"
            w.WriteStartArray()
            for entry in entries do
                writeEntry w entry
            w.WriteEndArray()

            w.WritePropertyName "findings"
            w.WriteStartArray()
            for finding in findings do
                writeFinding w finding
            w.WriteEndArray()

            w.WriteEndObject())
