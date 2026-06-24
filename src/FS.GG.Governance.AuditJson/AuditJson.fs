namespace FS.GG.Governance.AuditJson

open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CommandRecord.Model       // F052: ExitCode (the execution embed's exit code)
open FS.GG.Governance.GateRun.Model             // F052: GateDisposition, GateOutcome

// The F025 audit.json projection (US1–US4). Renders the F024 `ShipDecision` into the deterministic,
// versioned `audit.json` WHOLE-CHANGE verdict document text via a hand-driven `System.Text.Json`
// `Utf8JsonWriter` walk — the net10.0 shared-framework mechanism the kernel's `Json.fs` and F020's
// `RouteJson.fs` / F021's `GatesJson.fs` use, so NO new dependency (FR-014). PURE and TOTAL (FR-008):
// no I/O, no git, no clock, never throws. Emit-only: it re-derives/re-classifies/re-partitions/
// re-sorts NOTHING (the `ShipDecision` already fixed the verdict, the exit-code basis, the three-way
// partition, and each section's already-fixed composite order). No visibility modifiers — the surface
// is AuditJson.fsi (Principle II); every token helper and sub-object writer below is hidden by its
// absence from the .fsi, the `Kernel/Json.fs` + `RouteJson.fs` + `GatesJson.fs` precedent.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AuditJson =

    /// The declared schema-version token stamped into every emitted document (FR-013). A fixed,
    /// deterministic string literal — never derived from a clock, environment, or input value. Bumped
    /// to "fsgg.audit/v2" for the F045 embedded cache-eligibility contract.
    let schemaVersion = "fsgg.audit/v2"

    // ── internal writer plumbing (hidden — absent from AuditJson.fsi) ──

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string. Default
    /// `Utf8JsonWriter` options ⇒ no indentation ⇒ deterministic, compact output (the `Json.fs`
    /// `writeToString` precedent shared by F020/F021).
    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

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

    let basisToken (basis: ExitCodeBasis) : string =
        match basis with
        | Clean -> "clean"
        | Blocked -> "blocked"

    let severityToken (severity: Severity) : string =
        match severity with
        | Advisory -> "advisory"
        | Blocking -> "blocking"

    let maturityToken (maturity: Maturity) : string =
        match maturity with
        | Observe -> "observe"
        | Warn -> "warn"
        | BlockOnPr -> "blockOnPr"
        | BlockOnShip -> "blockOnShip"
        | BlockOnRelease -> "blockOnRelease"

    let modeToken (mode: RunMode) : string =
        match mode with
        | Sandbox -> "sandbox"
        | Inner -> "inner"
        | Focused -> "focused"
        | Verify -> "verify"
        | Gate -> "gate"
        | RunMode.Release -> "release"

    let profileToken (profile: Profile) : string =
        match profile with
        | Light -> "light"
        | Standard -> "standard"
        | Strict -> "strict"
        | Profile.Release -> "release"

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
        w.WriteString("baseSeverity", severityToken d.BaseSeverity)
        w.WriteString("maturity", maturityToken d.Maturity)
        w.WriteString("mode", modeToken d.Mode)
        w.WriteString("profile", profileToken d.Profile)
        w.WriteString("effectiveSeverity", severityToken d.EffectiveSeverity)
        w.WriteString("reason", d.Reason)
        w.WriteEndObject()

    // ── F045: the embedded cache-eligibility verdict (the reused F042 vocabulary + one new case) ──
    // The IDENTITY/token renderers are REUSED VERBATIM from public upstream — `referenceValue` (F030),
    // `categoryToken` (F029), `gateIdValue` (F018) — exactly as F042's `CacheEligibilityJson.fs` and the
    // sibling F045 `RouteJson.fs`. Each `match` is EXHAUSTIVE over the closed DU with NO wildcard, so a
    // future F041 verdict/cause case is a compile error here, never a silently mis-tokened field. The
    // render NEVER dereferences the opaque evidence reference, computes no key/hash/decision (FR-010/11).

    /// First-by-report-order-wins lookup from the report (research D4). On a duplicate `GateId` the FIRST
    /// entry by the report's LIST POSITION wins (the fold keeps the earliest add) — deterministic and
    /// total, keyed purely on `CacheEligibility.entries` order, never re-derived from the `GateId` value.
    let verdictByGate (report: CacheEligibilityReport) : Map<string, CacheEligibilityVerdict> =
        CacheEligibility.entries report
        |> List.fold
            (fun m e ->
                let k = gateIdValue e.Gate
                if Map.containsKey k m then m else Map.add k e.Verdict m)
            Map.empty

    /// The tagged `cause` object (no-hide, FR-009) — field order `kind`, then `categories` for
    /// `inputsChanged`. `NoPriorEvidence` ⇒ `{ kind:"noPriorEvidence" }` (NO `categories` field). The
    /// categories are named via `categoryToken` in the report's order — none dropped, added, truncated.
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

    /// The per-gate `cacheEligibility` verdict object — field order `kind`, then payload. `Some (Reusable
    /// ref)` ⇒ `{ kind:"reusable", evidence:<referenceValue ref> }` (only the opaque reference verbatim,
    /// never parsed/dereferenced — FR-011). `Some (MustRecompute cause)` ⇒ `{ kind:"mustRecompute",
    /// cause:<cause-object> }` (always a cause, no `evidence` field — FR-009). `None` (no matching report
    /// entry, or `cache = None`) ⇒ `{ kind:"notEvaluated" }` — NEVER rendered as `reusable` (FR-005).
    let writeCacheEligibility (w: Utf8JsonWriter) (verdict: CacheEligibilityVerdict option) =
        w.WriteStartObject()

        match verdict with
        | Some(Reusable ref) ->
            w.WriteString("kind", "reusable")
            w.WriteString("evidence", EvidenceReuse.referenceValue ref)
        | Some(MustRecompute cause) ->
            w.WriteString("kind", "mustRecompute")
            w.WritePropertyName "cause"
            writeCause w cause
        | None -> w.WriteString("kind", "notEvaluated")

        w.WriteEndObject()

    // ── F052: the embedded per-gate execution outcome (additive, default-empty ⇒ output unchanged) ──

    let dispositionToken (disposition: GateDisposition) : string =
        match disposition with
        | Executed -> "executed"
        | Reused -> "reused"
        | NotExecuted -> "notExecuted"

    /// First-by-list-order-wins lookup of the per-gate execution outcome, keyed on the gate-id string (the
    /// F045 `verdictByGate` precedent). Empty list ⇒ empty map ⇒ no `execution` object emitted anywhere.
    let outcomeByGate (execution: (GateId * GateOutcome) list) : Map<string, GateOutcome> =
        execution
        |> List.fold
            (fun m (gid, outcome) ->
                let k = gateIdValue gid
                if Map.containsKey k m then m else Map.add k outcome m)
            Map.empty

    /// The per-gate `execution` object — field order `disposition`, then (when present) `exitCode`, `passed`.
    /// `exitCode`/`passed` are OMITTED for `notExecuted` (no run, no exit — D6).
    let writeExecution (w: Utf8JsonWriter) (outcome: GateOutcome) =
        w.WriteStartObject()
        w.WriteString("disposition", dispositionToken outcome.Disposition)

        match outcome.ExitCode with
        | Some(ExitCode code) -> w.WriteNumber("exitCode", code)
        | None -> ()

        match outcome.Passed with
        | Some passed -> w.WriteBoolean("passed", passed)
        | None -> ()

        w.WriteEndObject()

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

        match item.Id with
        | GateItem g ->
            w.WriteString("kind", "gate")
            w.WriteString("id", gateIdValue g)
        | FindingItem(fid, GovernedPath path) ->
            w.WriteString("kind", "finding")
            w.WriteString("id", findingIdToken fid)
            w.WriteString("path", path)

        w.WritePropertyName "enforcement"
        writeEnforcement w item.Decision

        // F045: only GATE items carry the cache verdict (matched by GateId); finding items carry none.
        match item.Id with
        | GateItem g ->
            w.WritePropertyName "cacheEligibility"
            writeCacheEligibility w (lookup g)
        | FindingItem _ -> ()

        // F052: only GATE items carry the execution outcome (matched by GateId), beside cacheEligibility.
        // Written ONLY when an outcome is supplied; absent (default-empty) ⇒ byte-identical to F045 (D6).
        match item.Id with
        | GateItem g ->
            match execLookup g with
            | Some outcome ->
                w.WritePropertyName "execution"
                writeExecution w outcome
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

    // ── the public entry point ──

    let ofShipDecision
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        : string =
        // One linear walk of the already-ordered `ShipDecision`, writing the top-level object in the
        // FIXED order schemaVersion → verdict → exitCodeBasis → blockers → warnings → passing →
        // cacheEligibilityEvaluated. The verdict/basis are carried VERBATIM from the decision value —
        // never recomputed from the item sections (FR-002), never mapped to a numeric process exit code
        // (FR-003). Each section is emitted in its existing composite order, re-sorting NOTHING (FR-007).
        // PURE and TOTAL: the empty/clean decision yields three present, empty arrays with verdict:"pass"
        // / exitCodeBasis:"clean" and the cache section present — a valid success (FR-008/FR-009).
        //
        // F045: the cache verdict is matched per GATE item by `GateId`. `lookup` is built once — `None`
        // ⇒ every gate item `notEvaluated`; `Some report` ⇒ the first-by-report-order verdict map. The
        // top-level `cacheEligibilityEvaluated` flag is `false` for `None`, `true` for `Some _` (the
        // always-present section that survives the empty/clean decision — FR-012).
        let lookup: GateId -> CacheEligibilityVerdict option =
            match cache with
            | None -> fun _ -> None
            | Some report ->
                let byGate = verdictByGate report
                fun gateId -> Map.tryFind (gateIdValue gateId) byGate

        // F052: the per-gate execution lookup, built once from the supplied outcomes (empty ⇒ always None).
        let execByGate = outcomeByGate execution
        let execLookup: GateId -> GateOutcome option = fun gateId -> Map.tryFind (gateIdValue gateId) execByGate

        writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WriteString("verdict", verdictToken decision.Verdict)
            w.WriteString("exitCodeBasis", basisToken decision.ExitCodeBasis)
            writeSection w lookup execLookup "blockers" decision.Blockers
            writeSection w lookup execLookup "warnings" decision.Warnings
            writeSection w lookup execLookup "passing" decision.Passing
            w.WriteBoolean("cacheEligibilityEvaluated", Option.isSome cache)
            w.WriteEndObject())
