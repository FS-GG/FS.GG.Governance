namespace FS.GG.Governance.AuditJson

open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RuleIdentity         // 068: the additive per-finding `ruleId` source-prefixed token
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CommandRecord.Model       // F052: ExitCode (the execution embed's exit code)
open FS.GG.Governance.GateRun.Model             // F052: GateDisposition, GateOutcome
open FS.GG.Governance.RefreshJson               // F070: RefreshModel.viewKindToken for the generatedViews kind

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement // F070: the stale-view finding vocabulary

// The F025 audit.json projection (US1–US4). Renders the F024 `ShipDecision` into the deterministic,
// versioned `audit.json` WHOLE-CHANGE verdict document text via a hand-driven `System.Text.Json`
// `Utf8JsonWriter` walk — the net10.0 shared-framework mechanism the kernel's `Json.fs` and F020's
// `RouteJson.fs` / F021's `GatesJson.fs` use, so NO new dependency (FR-014). PURE and TOTAL (FR-008):
// no I/O, no git, no clock, never throws. Emit-only: it re-derives/re-classifies/re-partitions/
// re-sorts NOTHING (the `ShipDecision` already fixed the verdict, the exit-code basis, the three-way
// partition, and each section's already-fixed composite order). No visibility modifiers — the surface
// is AuditJson.fsi (Principle II); every token helper and sub-object writer below is hidden by its
// absence from the .fsi, the `Kernel/Json.fs` + `RouteJson.fs` + `GatesJson.fs` precedent.

open FS.GG.Governance.JsonText // 073: the shared deterministic-emit helper JsonText.writeToString
open FS.GG.Governance.JsonTokens // 073: the shared closed-enum token helpers (module-qualified)
open FS.GG.Governance.JsonWriters // 073: the shared sub-object/map writers (module-qualified)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AuditJson =

    /// The declared schema-version token stamped into every emitted document (FR-013). A fixed,
    /// deterministic string literal — never derived from a clock, environment, or input value. Bumped
    /// to "fsgg.audit/v2" for the F045 embedded cache-eligibility contract.
    let schemaVersion = "fsgg.audit/v2"

    // ── internal writer plumbing (hidden — absent from AuditJson.fsi) ──

    // ── six closed-enum token helpers (hidden) ──
    // Each `match` is EXHAUSTIVE over the closed DU with NO wildcard (research D3), so a future
    // verdict/basis/severity/maturity/mode/profile case is a compile error here, never a silently
    // mis-tokened field. Enforcement's own token helpers are hidden across the assembly boundary, so
    // the severity/maturity/mode/profile helpers are local (the GatesJson precedent); the two IDENTITY
    // renderers are REUSED from public upstream (`Gates.gateIdValue`, `Findings.findingIdToken`).

    let verdictToken (verdict: Verdict) : string =
        match verdict with
        | Pass -> "pass"
        | Fail -> "fail"

    let modeToken (mode: RunMode) : string =
        match mode with
        | Sandbox -> "sandbox"
        | Inner -> "inner"
        | Focused -> "focused"
        | Verify -> "verify"
        | Gate -> "gate"
        | RunMode.Release -> "release"

    // ── sub-object writers (hidden) — each emits its documented field order verbatim ──

    /// The nested `enforcement` object — field order `baseSeverity`, `maturity`, `mode`, `profile`,
    /// `effectiveSeverity`, `reason` (contracts/audit-json-document.md). The six F023
    /// `EnforcementDecision` fields carried VERBATIM in record order — none dropped, none re-derived,
    /// none re-ordered (FR-006). `baseSeverity` and `effectiveSeverity` are SEPARATE fields, so a
    /// relaxed base-`Blocking` item shows both and the no-hide rule is observable (FR-011). The
    /// free-text `reason` is written through the writer's string API so JSON-escaping is the writer's
    /// job — never manual (FR-012).
    let writeEnforcement (w: Utf8JsonWriter) (d: EnforcementDecision) =
        w.WriteStartObject()
        w.WriteString("baseSeverity", JsonTokens.severityToken d.BaseSeverity)
        w.WriteString("maturity", JsonTokens.maturityToken d.Maturity)
        w.WriteString("mode", modeToken d.Mode)
        w.WriteString("profile", JsonTokens.profileToken d.Profile)
        w.WriteString("effectiveSeverity", JsonTokens.severityToken d.EffectiveSeverity)
        w.WriteString("reason", d.Reason)
        w.WriteEndObject()

    // ── F045: the per-gate `cacheEligibility` verdict object is written via
    //    `JsonWriters.writeCacheEligibility` (111/A4 — the byte-identical Audit/Route copy now lives in the
    //    shared writer leaf; it never dereferences the opaque evidence reference, FR-010/11). ──

    // ── F052: the embedded per-gate execution outcome (additive, default-empty ⇒ output unchanged) ──

    /// One enforced item — a TAGGED object discriminated by `kind` (research D5). Matches the closed
    /// `EnforcedItemId` exhaustively (no wildcard):
    ///   • `GateItem g`                       → `kind:"gate"`, `id` (via `gateIdValue`, never re-parsed
    ///                                          even across a `:` separator — FR-010), `enforcement`,
    ///                                          and (F045) the per-gate `cacheEligibility` verdict matched
    ///                                          by `GateId` via `lookup`, as the item's LAST field.
    ///   • `FindingItem (fid, GovernedPath p)`→ `kind:"finding"`, `id` (via `findingIdToken`), `path`
    ///                                          (the unwrapped `GovernedPath` verbatim — FR-010),
    ///                                          `enforcement`. NO `cacheEligibility` — cache is
    ///                                          gate-scoped (F045 FR-004, SC-002).
    /// A gate item has NO `path` field (absent, not `null`; the `kind` tag disambiguates).
    let writeItem
        (w: Utf8JsonWriter)
        (lookup: GateId -> CacheEligibilityVerdict option)
        (execLookup: GateId -> GateOutcome option)
        (item: EnforcedItem)
        =
        w.WriteStartObject()

        // 068: each item carries a stable, source-prefixed `ruleId` as the FIRST field after `id` —
        // `gate (gateIdValue g)` for a typed gate, `boundary (findingIdToken fid)` for a kernel boundary
        // finding. Derived at emit time from the identity the item already holds; reads no profile / mode /
        // effective severity / message, so it is invariant across every profile and run mode (FR-003).
        // Dispatch stays exhaustive over the closed `EnforcedItemId` (no wildcard).
        match item.Id with
        | GateItem g ->
            w.WriteString("kind", "gate")
            w.WriteString("id", gateIdValue g)
            w.WriteString("ruleId", RuleIdentity.ruleIdToken (RuleIdentity.gate (gateIdValue g)))
        | FindingItem(fid, GovernedPath path) ->
            w.WriteString("kind", "finding")
            w.WriteString("id", findingIdToken fid)
            w.WriteString("ruleId", RuleIdentity.ruleIdToken (RuleIdentity.boundary (findingIdToken fid)))
            w.WriteString("path", path)

        w.WritePropertyName "enforcement"
        writeEnforcement w item.Decision

        // F045: only GATE items carry the cache verdict (matched by GateId); finding items carry none.
        match item.Id with
        | GateItem g ->
            w.WritePropertyName "cacheEligibility"
            JsonWriters.writeCacheEligibility w (lookup g)
        | FindingItem _ -> ()

        // F052: only GATE items carry the execution outcome (matched by GateId), beside cacheEligibility.
        // Written ONLY when an outcome is supplied; absent (default-empty) ⇒ byte-identical to F045 (D6).
        match item.Id with
        | GateItem g ->
            match execLookup g with
            | Some outcome ->
                w.WritePropertyName "execution"
                JsonWriters.writeExecution w outcome
            | None -> ()
        | FindingItem _ -> ()

        w.WriteEndObject()

    /// One section array — walks the carried item list in its existing F024 composite order, emitting
    /// each item via `writeItem`, re-sorting NOTHING (FR-007). An empty list emits a present, empty
    /// array (FR-005, FR-009).
    let writeSection
        (w: Utf8JsonWriter)
        (lookup: GateId -> CacheEligibilityVerdict option)
        (execLookup: GateId -> GateOutcome option)
        (name: string)
        (items: EnforcedItem list)
        =
        w.WritePropertyName name
        w.WriteStartArray()
        for item in items do
            writeItem w lookup execLookup item
        w.WriteEndArray()

    // F070: the additive `generatedViews` array (stale-generated-view findings folded through the existing
    // F023 truth table). Each entry carries the view id/kind, the stale cause, the drifted categories (or the
    // undeterminable detail), and BOTH base and effective severity + the lever-naming reason (no-hide, FR-006).
    // Sorted by viewId; written ONLY when non-empty ⇒ absent ⇒ byte-identical to the pre-F070 projection (FR-004).
    let writeGeneratedView (w: Utf8JsonWriter) (finding: CE.CurrencyFinding) (decision: EnforcementDecision) =
        w.WriteStartObject()
        w.WriteString("viewId", finding.ViewId)
        w.WriteString("kind", RefreshModel.viewKindToken finding.Kind)
        w.WriteString("cause", CE.staleCauseToken finding.Cause)

        match finding.Cause with
        | CE.SourceDrift drifted ->
            w.WritePropertyName "drifted"
            w.WriteStartArray()

            for category in drifted do
                w.WriteStringValue(categoryToken category)

            w.WriteEndArray()
        | CE.Undeterminable reason -> w.WriteString("detail", reason)

        w.WriteString("baseSeverity", JsonTokens.severityToken finding.BaseSeverity)
        w.WriteString("effectiveSeverity", JsonTokens.severityToken decision.EffectiveSeverity)
        w.WriteString("reason", decision.Reason)
        w.WriteEndObject()

    let writeGeneratedViews (w: Utf8JsonWriter) (views: (CE.CurrencyFinding * EnforcementDecision) list) =
        match views with
        | [] -> ()
        | _ ->
            w.WritePropertyName "generatedViews"
            w.WriteStartArray()

            for finding, decision in views |> List.sortBy (fun (f, _) -> f.ViewId) do
                writeGeneratedView w finding decision

            w.WriteEndArray()

    // ── the public entry point ──

    // F070: the ship.json/audit.json emitter. Walks the already-ordered `ShipDecision` once, writing the
    // top-level object in the FIXED order schemaVersion → verdict → exitCodeBasis → blockers → warnings →
    // passing → cacheEligibilityEvaluated → (generatedViews when non-empty). The verdict/basis are carried
    // VERBATIM from the decision value — never recomputed from the item sections (FR-002), never mapped to a
    // numeric process exit code (FR-003). Each section is emitted in its existing composite order, re-sorting
    // NOTHING (FR-007). PURE and TOTAL: the empty/clean decision yields three present, empty arrays with
    // verdict:"pass" / exitCodeBasis:"clean" and the cache section present — a valid success (FR-008/FR-009).
    //
    // F045: the cache verdict is matched per GATE item by `GateId`. `lookup` is built once — `None` ⇒ every
    // gate item `notEvaluated`; `Some report` ⇒ the first-by-report-order verdict map. The top-level
    // `cacheEligibilityEvaluated` flag is `false` for `None`, `true` for `Some _` (FR-012).
    let ofShipDecisionWithGeneratedViews
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (generatedViews: (CE.CurrencyFinding * EnforcementDecision) list)
        : string =
        let lookup: GateId -> CacheEligibilityVerdict option =
            match cache with
            | None -> fun _ -> None
            | Some report ->
                let byGate = JsonWriters.verdictByGate report
                fun gateId -> Map.tryFind (gateIdValue gateId) byGate

        // F052: the per-gate execution lookup, built once from the supplied outcomes (empty ⇒ always None).
        let execByGate = JsonWriters.outcomeByGate execution
        let execLookup: GateId -> GateOutcome option = fun gateId -> Map.tryFind (gateIdValue gateId) execByGate

        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WriteString("verdict", verdictToken decision.Verdict)
            w.WriteString("exitCodeBasis", JsonTokens.basisToken decision.ExitCodeBasis)
            writeSection w lookup execLookup "blockers" decision.Blockers
            writeSection w lookup execLookup "warnings" decision.Warnings
            writeSection w lookup execLookup "passing" decision.Passing
            w.WriteBoolean("cacheEligibilityEvaluated", Option.isSome cache)
            writeGeneratedViews w generatedViews
            w.WriteEndObject())

    // The no-generated-views entry point. Delegates to the overload with an empty view list — `writeGeneratedViews`
    // omits the array when empty, so this is byte-identical to the former hand-copied body (#56/A5, FR-013).
    let ofShipDecision
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        : string =
        ofShipDecisionWithGeneratedViews decision cache execution []
