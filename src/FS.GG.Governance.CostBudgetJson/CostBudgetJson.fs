namespace FS.GG.Governance.CostBudgetJson

open System.IO
open System.Text
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
// verbatim, never throws. Emit-only: it re-derives/re-orders NOTHING (the report is already GateId-ordinal,
// the findings already (GateId, kind)-sorted). Every token `match` is EXHAUSTIVE with no wildcard, so a
// future DU case is a compile error here. No access modifiers — the surface is CostBudgetJson.fsi
// (Principle II).

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CostBudgetJson =

    let schemaVersion = "fsgg.cost-budget/v1"

    // ── internal writer plumbing (hidden — absent from CostBudgetJson.fsi) ──

    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    // ── closed-enum token helpers (exhaustive, no wildcard) ──

    let costToken (cost: Cost) : string =
        match cost with
        | Cheap -> "cheap"
        | Medium -> "medium"
        | High -> "high"
        | Exhaustive -> "exhaustive"

    let reviewToken (review: AgentReviewMark) : string =
        match review with
        | Deterministic -> "deterministic"
        | AgentReviewed _ -> "agentReviewed"

    let deferralToken (cls: DeferralClass) : string =
        match cls with
        | Skipped -> "skipped"
        | Deferred -> "deferred"

    let severityToken (severity: Severity) : string =
        match severity with
        | Advisory -> "advisory"
        | Blocking -> "blocking"

    let findingKindToken (kind: CostFindingKind) : string = kindToken kind

    // ── tagged sub-object writers (hidden) — each emits its documented field order verbatim ──

    /// The recompute `cause` object — reuses the F042 `CacheEligibilityJson` shape verbatim:
    /// `{ kind:"noPriorEvidence" }` or `{ kind:"inputsChanged", categories:[…] }` named via `categoryToken`.
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

    /// The tagged `decision` object — exactly one shape. `overBudget` carries `class`, `ceiling`, and a
    /// deterministic `reason` string (the underlying cause is surfaced in the findings section, not here).
    let writeDecision (w: Utf8JsonWriter) (gate: GateId) (decision: CacheDecision) =
        w.WriteStartObject()

        match decision with
        | Reuse evidence ->
            w.WriteString("kind", "reuse")
            w.WriteString("evidence", EvidenceReuse.referenceValue evidence)
        | Recompute cause ->
            w.WriteString("kind", "recompute")
            w.WritePropertyName "cause"
            writeCause w cause
        | OverBudget reason ->
            w.WriteString("kind", "overBudget")
            w.WriteString("class", deferralToken reason.Class)
            w.WriteString("ceiling", costToken reason.Ceiling)

            let reasonText =
                sprintf
                    "%s (%s) exceeds the %s budget"
                    (gateIdValue gate)
                    (costToken reason.Cost)
                    (costToken reason.Ceiling)

            w.WriteString("reason", reasonText)

        w.WriteEndObject()

    /// One decision entry — field order `gate`, `cost`, `review`, `decision`.
    let writeEntry (w: Utf8JsonWriter) (entry: CacheDecisionEntry) =
        w.WriteStartObject()
        w.WriteString("gate", gateIdValue entry.Gate)
        w.WriteString("cost", costToken entry.Cost)
        w.WriteString("review", reviewToken entry.Review)
        w.WritePropertyName "decision"
        writeDecision w entry.Gate entry.Decision
        w.WriteEndObject()

    /// One finding — field order `gate`, `kind`, `baseSeverity`, `categories` (ONLY for `stale`), `message`.
    let writeFinding (w: Utf8JsonWriter) (finding: CostFinding) =
        w.WriteStartObject()
        w.WriteString("gate", gateIdValue finding.Gate)
        w.WriteString("kind", findingKindToken finding.Kind)
        w.WriteString("baseSeverity", severityToken finding.BaseSeverity)

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

        writeToString (fun w ->
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
