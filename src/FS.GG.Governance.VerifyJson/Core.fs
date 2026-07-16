// The verify.json verdict core (076 Phase C seam). Visibility lives in Core.fsi (Principle II) — this file
// carries NO top-level access modifiers; every token helper and sub-object writer below is hidden by its
// absence from Core.fsi. PURE and TOTAL: it walks a caller-owned `Utf8JsonWriter`, re-deriving/re-sorting
// NOTHING. Bindings lifted verbatim from VerifyJson.fs (the verdict writers + writeCore); `writeCore` drops
// the old `findings`/`surfaceChecks` tail — the composing `VerifyJson` entry now appends `surfaceChecks`
// (via SurfaceChecks.writeSurfaceFinding) in the identical wire position, so the emitted bytes are unchanged.

namespace FS.GG.Governance.VerifyJson

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
open FS.GG.Governance.JsonTokens // 073: the shared closed-enum token helpers (module-qualified)
open FS.GG.Governance.JsonWriters // 073: the shared sub-object/map writers (module-qualified)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Core =

    let schemaVersion = "fsgg.verify/v1"

    // ── closed-enum token helpers (hidden) — each EXHAUSTIVE with NO wildcard ──

    let verdictToken (verdict: Verdict) : string =
        match verdict with
        | Pass -> "pass"
        | Fail -> "blocked"

    // 073: `dispositionToken` STAYS LOCAL — VerifyJson emits `not-executed` (hyphen) where the shared
    // `JsonTokens.dispositionToken` emits `notExecuted` (camelCase). The strings DIVERGE, so unifying
    // would change bytes; like `verdictToken` (`Fail` → `blocked`), this copy is out of scope (research
    // D4 flagged this Disposition divergence — caught by the byte-identity gate; FR-009).
    let dispositionToken (disposition: GateDisposition) : string =
        match disposition with
        | Executed _ -> "executed"
        | Reused _ -> "reused"
        | NotExecuted -> "not-executed"

    // ── the `cause` VALUE — a bare string for `NoPriorEvidence`, a tagged object for `InputsChanged`
    //    (contracts/verify.schema.md). `noPriorEvidence` (no `categories`) is distinct from `inputsChanged`
    //    with `categories: []`. The categories are named via `categoryToken` in the report's order. ──

    let writeCauseValue (w: Utf8JsonWriter) (cause: RecomputeCause) =
        match cause with
        | NoPriorEvidence -> w.WriteStringValue "noPriorEvidence"
        | InputsChanged cats ->
            w.WriteStartObject()
            w.WriteString("kind", "inputsChanged")
            w.WritePropertyName "categories"
            w.WriteStartArray()
            for c in cats do
                w.WriteStringValue(categoryToken c)
            w.WriteEndArray()
            w.WriteEndObject()

    // ── the nested `enforcement` object — field order `baseSeverity`, `maturity`, `mode`, `profile`,
    //    `effectiveSeverity`, `reason`. `mode` is the fixed literal `"verify"` (contracts/verify.schema.md);
    //    the free-text `reason` is written through the writer's string API so JSON-escaping is the writer's
    //    job, never manual. ──

    let writeEnforcement (w: Utf8JsonWriter) (d: EnforcementDecision) =
        w.WriteStartObject()
        w.WriteString("baseSeverity", JsonTokens.severityToken d.BaseSeverity)
        w.WriteString("maturity", JsonTokens.maturityToken d.Maturity)
        w.WriteString("mode", "verify")
        w.WriteString("profile", JsonTokens.profileToken d.Profile)
        w.WriteString("effectiveSeverity", JsonTokens.severityToken d.EffectiveSeverity)
        w.WriteString("reason", d.Reason)
        w.WriteEndObject()

    // ── the per-gate `cache` verdict object (or `null`) — `Some (Reusable ref)` ⇒ `{ kind:"reusable",
    //    evidence:<referenceValue ref> }` (only the opaque reference verbatim, never dereferenced);
    //    `Some (MustRecompute cause)` ⇒ `{ kind:"mustRecompute", cause:<cause> }`; `None` ⇒ JSON `null`. ──

    let writeCache (w: Utf8JsonWriter) (verdict: CacheEligibilityVerdict option) =
        match verdict with
        | Some(Reusable ref) ->
            w.WriteStartObject()
            w.WriteString("kind", "reusable")
            w.WriteString("evidence", EvidenceReuse.referenceValue ref)
            w.WriteEndObject()
        | Some(MustRecompute cause) ->
            w.WriteStartObject()
            w.WriteString("kind", "mustRecompute")
            w.WritePropertyName "cause"
            writeCauseValue w cause
            w.WriteEndObject()
        | None -> w.WriteNullValue()

    // ── the per-gate `execution` object (or `null`) — field order `disposition`, `exitCode`, `passed`.
    //    `exitCode`/`passed` are ALWAYS present (as `null` when absent — distinct from AuditJson which omits
    //    them) per contracts/verify.schema.md. `None` outcome ⇒ JSON `null`. ──

    let writeExecution (w: Utf8JsonWriter) (outcome: GateOutcome option) =
        match outcome with
        | Some o ->
            w.WriteStartObject()
            w.WriteString("disposition", dispositionToken o.Disposition)

            match o.Disposition with
            | Executed(ExitCode code, passed)
            | Reused(ExitCode code, passed) ->
                w.WriteNumber("exitCode", code)
                w.WriteBoolean("passed", passed)
            | NotExecuted ->
                w.WriteNull("exitCode")
                w.WriteNull("passed")

            w.WriteEndObject()
        | None -> w.WriteNullValue()

    // ── per-gate lookups, first-by-list-order-wins (the AuditJson `JsonWriters.verdictByGate`/`JsonWriters.outcomeByGate` precedent) ──

    // ── one enforced item — a tagged `id` object (`gate`/`finding`), `enforcement`, `cache`, `execution`.
    //    A finding carries `cache:null`/`execution:null` (cache + execution are gate-scoped). ──

    let writeItem
        (w: Utf8JsonWriter)
        (lookup: GateId -> CacheEligibilityVerdict option)
        (execLookup: GateId -> GateOutcome option)
        (item: EnforcedItem)
        =
        w.WriteStartObject()

        w.WritePropertyName "id"
        w.WriteStartObject()

        match item.Id with
        | GateItem g ->
            w.WriteString("kind", "gate")
            w.WriteString("gate", gateIdValue g)
        | FindingItem(fid, GovernedPath path) ->
            w.WriteString("kind", "finding")
            w.WriteString("finding", findingIdToken fid)
            w.WriteString("path", path)

        w.WriteEndObject()

        // 068: the stable, source-prefixed `ruleId`, emitted right after the `id` object — `gate
        // (gateIdValue g)` for a typed gate, `boundary (findingIdToken fid)` for a kernel boundary finding.
        // Derived at emit time from the identity the item already holds; reads no profile / mode / effective
        // severity / message, so it matches the audit.json id for the same finding (FR-006) and is invariant
        // across every profile and run mode (FR-003). Exhaustive over the closed `EnforcedItemId`.
        let ruleId =
            match item.Id with
            | GateItem g -> RuleIdentity.gate (gateIdValue g)
            | FindingItem(fid, _) -> RuleIdentity.boundary (findingIdToken fid)

        w.WriteString("ruleId", RuleIdentity.ruleIdToken ruleId)

        w.WritePropertyName "enforcement"
        writeEnforcement w item.Decision

        w.WritePropertyName "cache"

        match item.Id with
        | GateItem g -> writeCache w (lookup g)
        | FindingItem _ -> w.WriteNullValue()

        w.WritePropertyName "execution"

        match item.Id with
        | GateItem g -> writeExecution w (execLookup g)
        | FindingItem _ -> w.WriteNullValue()

        w.WriteEndObject()

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

    // ── the `currency` section — derived from the cache report (fresh/recomputed) and the decision's gate
    //    items (unresolved = selected gates with no cache entry). No new sensing, no new severity path. ──

    let gateItemIds (decision: ShipDecision) : string list =
        [ decision.Blockers; decision.Warnings; decision.Passing ]
        |> List.concat
        |> List.choose (fun item ->
            match item.Id with
            | GateItem g -> Some(gateIdValue g)
            | FindingItem _ -> None)
        // First-occurrence-preserving dedup in O(n) (#56/C1f) — List.distinct keeps input order, so this is
        // byte-identical to the previous O(n²) `List.contains`/append fold.
        |> List.distinct

    let writeCurrency
        (w: Utf8JsonWriter)
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (missingByGate: Map<string, string list>)
        =
        let entries =
            match cache with
            | Some report -> CacheEligibility.entries report
            | None -> []

        let resolvedGates = entries |> List.map (fun e -> gateIdValue e.Gate) |> Set.ofList

        let unresolved = gateItemIds decision |> List.filter (fun g -> not (Set.contains g resolvedGates))

        w.WritePropertyName "currency"
        w.WriteStartObject()

        // fresh ⇐ Reusable ref → { gate, evidence }
        w.WritePropertyName "fresh"
        w.WriteStartArray()

        for e in entries do
            match e.Verdict with
            | Reusable ref ->
                w.WriteStartObject()
                w.WriteString("gate", gateIdValue e.Gate)
                w.WriteString("evidence", EvidenceReuse.referenceValue ref)
                w.WriteEndObject()
            | MustRecompute _ -> ()

        w.WriteEndArray()

        // recomputed ⇐ MustRecompute cause → { gate, cause }
        w.WritePropertyName "recomputed"
        w.WriteStartArray()

        for e in entries do
            match e.Verdict with
            | MustRecompute cause ->
                w.WriteStartObject()
                w.WriteString("gate", gateIdValue e.Gate)
                w.WritePropertyName "cause"
                writeCauseValue w cause
                w.WriteEndObject()
            | Reusable _ -> ()

        w.WriteEndArray()

        // unresolved ⇐ selected gate with no cache entry → { gate, missing: [<missing-fact wire tokens>] }.
        // The missing-fact tokens (`ruleHash`/`coveredArtifacts`/… — the F029 enum-order, injective
        // `missingFactToken` set) are carried in by the caller keyed on the gate id value: verify's command
        // host resolves them from `FreshnessResolution` (which holds `Model.Sensed`) and hands them here
        // already tokenized, so this stays emit-only — it re-derives and re-classifies nothing. A gate absent
        // from `missingByGate` (or a caller that has no resolution, e.g. the pinning unit projections) writes
        // an empty array, so those paths are byte-identical to the pre-threading output.
        w.WritePropertyName "unresolved"
        w.WriteStartArray()

        for g in unresolved do
            w.WriteStartObject()
            w.WriteString("gate", g)
            w.WritePropertyName "missing"
            w.WriteStartArray()
            for token in (Map.tryFind g missingByGate |> Option.defaultValue []) do
                w.WriteStringValue token
            w.WriteEndArray()
            w.WriteEndObject()

        w.WriteEndArray()

        w.WriteEndObject()

    // ── the shared verdict body (hidden) — schemaVersion → verdict → exitCodeBasis → sections → currency.
    //    The optional surfaceChecks/releaseReadiness/generatedViews tail is appended by the entry AFTER this,
    //    in the identical wire position, so the byte stream matches the pre-split projection. ──

    let writeCore
        (w: Utf8JsonWriter)
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (missingByGate: Map<string, string list>)
        =
        let lookup: GateId -> CacheEligibilityVerdict option =
            match cache with
            | None -> fun _ -> None
            | Some report ->
                let byGate = JsonWriters.verdictByGate report
                fun gateId -> Map.tryFind (gateIdValue gateId) byGate

        let execByGate = JsonWriters.outcomeByGate execution
        let execLookup: GateId -> GateOutcome option = fun gateId -> Map.tryFind (gateIdValue gateId) execByGate

        w.WriteString("schemaVersion", schemaVersion)
        w.WriteString("verdict", verdictToken decision.Verdict)
        w.WriteString("exitCodeBasis", JsonTokens.basisToken decision.ExitCodeBasis)
        writeSection w lookup execLookup "blockers" decision.Blockers
        writeSection w lookup execLookup "warnings" decision.Warnings
        writeSection w lookup execLookup "passing" decision.Passing
        writeCurrency w decision cache missingByGate
