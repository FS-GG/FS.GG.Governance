namespace FS.GG.Governance.RouteJson

open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CommandRecord.Model       // F052: ExitCode (the execution embed's exit code)
open FS.GG.Governance.GateRun.Model             // F052: GateDisposition, GateOutcome
open FS.GG.Governance.ProductSurfaces.Model      // F23: ProductSurfaceReport/ProductClassification/TierAlternative

// The F020 route.json projection (US1–US4). Renders the F019 `RouteResult` into the deterministic,
// versioned `route.json` document text via a hand-driven `System.Text.Json` `Utf8JsonWriter` walk —
// the net10.0 shared-framework mechanism the kernel's `Json.fs` uses, so NO new dependency (FR-015).
// PURE and TOTAL (FR-008): no I/O, no git, no clock, never throws. Emit-only: re-derives/re-sorts/
// re-classifies NOTHING (the `RouteResult` already fixed every collection's order). No visibility
// modifiers — the surface is RouteJson.fsi (Principle II); every token helper and sub-object writer
// below is hidden by its absence from the .fsi, the `Kernel/Json.fs` precedent.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RouteJson =

    /// The declared schema-version token stamped into every emitted document (FR-013). A fixed,
    /// deterministic string literal — never derived from a clock, environment, or input value. Bumped
    /// to "fsgg.route/v2" for the F045 embedded cache-eligibility contract.
    let schemaVersion = "fsgg.route/v2"

    // ── internal writer plumbing (hidden — absent from RouteJson.fsi) ──

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string. Default
    /// `Utf8JsonWriter` options ⇒ no indentation ⇒ deterministic, compact output (the `Json.fs`
    /// `writeToString` precedent).
    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    // ── closed-enum token helpers (hidden) ──
    // Each `match` is EXHAUSTIVE over the closed DU with NO wildcard (research D3), so a future
    // tier/maturity/zone/environment case is a compile error here, never a silently mis-tokened field.

    let costToken (cost: Cost) : string =
        match cost with
        | Cheap -> "cheap"
        | Medium -> "medium"
        | High -> "high"
        | Exhaustive -> "exhaustive"

    let maturityToken (maturity: Maturity) : string =
        match maturity with
        | Observe -> "observe"
        | Warn -> "warn"
        | BlockOnPr -> "blockOnPr"
        | BlockOnShip -> "blockOnShip"
        | BlockOnRelease -> "blockOnRelease"

    let environmentToken (env: EnvironmentClass) : string =
        match env with
        | Local -> "local"
        | Ci -> "ci"
        | LocalOrCi -> "localOrCi"
        | Release -> "release"

    /// Write a finding `zone`: `GovernedRootUnknown` → the string `"governedRootUnknown"`;
    /// `ProtectedBoundaryUnknown sid` → the object `{ "protectedBoundary": "<surfaceId>" }`.
    let writeZone (w: Utf8JsonWriter) (zone: FindingZone) =
        match zone with
        | GovernedRootUnknown -> w.WriteStringValue "governedRootUnknown"
        | ProtectedBoundaryUnknown(SurfaceId sid) ->
            w.WriteStartObject()
            w.WriteString("protectedBoundary", sid)
            w.WriteEndObject()

    // ── sub-object writers (hidden) — each emits its documented field order verbatim ──

    /// `freshnessKey` — field order `check`, `domain`, `cost`, `environment`, `command`. Carried key
    /// INPUTS only — never a cache verdict (FR-014). `command` is the `CommandId` string when `Some`,
    /// JSON `null` when `None`.
    let writeFreshnessKey (w: Utf8JsonWriter) (key: FreshnessKey) =
        w.WriteStartObject()
        let (CheckId check) = key.Check
        w.WriteString("check", check)
        let (DomainId domain) = key.Domain
        w.WriteString("domain", domain)
        w.WriteString("cost", costToken key.Cost)
        w.WriteString("environment", environmentToken key.Environment)

        match key.Command with
        | Some(CommandId c) -> w.WriteString("command", c)
        | None -> w.WriteNull "command"

        w.WriteEndObject()

    /// One prerequisite: `RequiresCommand c` → `{ "requiresCommand": "<commandId>" }`.
    let writePrerequisite (w: Utf8JsonWriter) (prereq: GatePrerequisite) =
        w.WriteStartObject()
        let (RequiresCommand(CommandId c)) = prereq
        w.WriteString("requiresCommand", c)
        w.WriteEndObject()

    /// One selecting path: `{ "path", "matchedGlob" }`, both declared `GovernedPath`s verbatim.
    let writeSelectingPath (w: Utf8JsonWriter) (sp: SelectingPath) =
        w.WriteStartObject()
        let (GovernedPath path) = sp.Path
        w.WriteString("path", path)
        let (GovernedPath glob) = sp.MatchedGlob
        w.WriteString("matchedGlob", glob)
        w.WriteEndObject()

    /// One finding — field order `id`, `path`, `zone`, `message`. Carried UNCHANGED from F017
    /// (FR-005). `id` via `Findings.findingIdToken`; free-text `message` written through the writer's
    /// string API so JSON-escaping is the writer's job.
    let writeFinding (w: Utf8JsonWriter) (f: UnknownGovernedPathFinding) =
        w.WriteStartObject()
        w.WriteString("id", findingIdToken f.Id)
        let (GovernedPath path) = f.Path
        w.WriteString("path", path)
        w.WritePropertyName "zone"
        writeZone w f.Zone
        w.WriteString("message", f.Message)
        w.WriteEndObject()

    // ── F045: the embedded cache-eligibility verdict (the reused F042 vocabulary + one new case) ──
    // The IDENTITY/token renderers are REUSED VERBATIM from public upstream — `referenceValue` (F030),
    // `categoryToken` (F029), `gateIdValue` (F018) — exactly as F042's `CacheEligibilityJson.fs`. Each
    // `match` is EXHAUSTIVE over the closed DU with NO wildcard, so a future F041 verdict/cause case is a
    // compile error here, never a silently mis-tokened field. The render NEVER dereferences the opaque
    // evidence reference, computes no key/hash/decision, and resolves nothing (FR-010, FR-011).

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

    /// One selected gate — the documented field order (contracts/route-json-document.md). Carries the
    /// embedded F018 `Gate` VERBATIM (FR-002); `id` via `Gates.gateIdValue`, never re-parsed (FR-010);
    /// `maturity` declared verbatim — NOT translated to enforcement (FR-011). Free-text `description`
    /// is written through the writer's string API so escaping is the writer's job (no manual escape).
    /// (F045) `lookup` resolves the gate's cache verdict by `GateId` (`None` ⇒ `notEvaluated`), emitted
    /// as the entry's LAST field — additive, changing no existing field.
    let writeSelectedGate
        (w: Utf8JsonWriter)
        (lookup: GateId -> CacheEligibilityVerdict option)
        (execLookup: GateId -> GateOutcome option)
        (sg: SelectedGate)
        =
        let gate = sg.Gate
        w.WriteStartObject()
        w.WriteString("id", gateIdValue gate.Id)
        let (DomainId domain) = gate.Domain
        w.WriteString("domain", domain)
        w.WriteString("description", gate.Description)
        w.WriteString("cost", costToken gate.Cost)
        let (TimeoutLimit seconds) = gate.Timeout
        w.WriteNumber("timeout", seconds)
        let (Owner owner) = gate.Owner
        w.WriteString("owner", owner)
        w.WriteString("maturity", maturityToken gate.Maturity)
        w.WriteBoolean("productCheck", gate.ProductCheck)

        w.WritePropertyName "prerequisites"
        w.WriteStartArray()
        for prereq in gate.Prerequisites do
            writePrerequisite w prereq
        w.WriteEndArray()

        w.WritePropertyName "freshnessKey"
        writeFreshnessKey w gate.FreshnessKey

        w.WritePropertyName "selectingPaths"
        w.WriteStartArray()
        for sp in sg.SelectingPaths do
            writeSelectingPath w sp
        w.WriteEndArray()

        // F045: the per-gate cache-eligibility verdict, matched by GateId, as the entry's last field.
        w.WritePropertyName "cacheEligibility"
        writeCacheEligibility w (lookup gate.Id)

        // F052: the per-gate execution outcome, matched by GateId, beside cacheEligibility. Written ONLY
        // when an outcome is supplied for this gate; absent (default-empty) ⇒ byte-identical to F045 (D6).
        match execLookup gate.Id with
        | Some outcome ->
            w.WritePropertyName "execution"
            writeExecution w outcome
        | None -> ()

        w.WriteEndObject()

    /// The per-tier `cost` object — field order `cheap`, `medium`, `high`, `exhaustive`. Every
    /// declared tier present with its integer count including zero; never a summed scalar (FR-006).
    let writeCost (w: Utf8JsonWriter) (cost: CostRollup) =
        w.WriteStartObject()
        w.WriteNumber("cheap", cost.Cheap)
        w.WriteNumber("medium", cost.Medium)
        w.WriteNumber("high", cost.High)
        w.WriteNumber("exhaustive", cost.Exhaustive)
        w.WriteEndObject()

    // ── F23: the additive product-surface classification section (default-empty ⇒ output unchanged) ──
    // The class/tier/reason tokens are REUSED VERBATIM from public upstream — `surfaceClassToken` and
    // `generatedProductTierToken` (F23 Config.Model), `classificationReasonToken` (ProductSurfaces.Model).
    // Each `match` is EXHAUSTIVE with NO wildcard, so a future SurfaceClass/tier/reason case is a compile
    // error here, never a silently mis-tokened field.

    /// The `alternative` value: a strictly-cheaper, locally-runnable declared tier token, or `"none"` (the
    /// explicit no-cheaper-local outcome — never null, the no-hide rule).
    let alternativeToken (alt: TierAlternative) : string =
        match alt with
        | CheaperLocalTier t -> generatedProductTierToken t
        | NoCheaperLocalTier -> "none"

    /// One product-surface classification — field order `path`, `capability`, `surface`, `class`, `tier`,
    /// `tierDeclared`, `alternative`. Only declared ids; no raw YAML/host path/timestamp.
    let writeProductClassification (w: Utf8JsonWriter) (c: ProductClassification) =
        w.WriteStartObject()
        let (GovernedPath path) = c.Path
        w.WriteString("path", path)
        let (DomainId capability) = c.Capability
        w.WriteString("capability", capability)
        let (SurfaceId surface) = c.Surface
        w.WriteString("surface", surface)
        w.WriteString("class", surfaceClassToken c.Class)
        w.WriteString("tier", generatedProductTierToken c.SelectedTier)
        w.WriteBoolean("tierDeclared", c.TierIsDeclared)
        w.WriteString("alternative", alternativeToken c.Alternative)
        w.WriteEndObject()

    // ── the public entry points ──

    let ofRouteResultWithProductSurfaces
        (result: RouteResult)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (productSurfaces: ProductSurfaceReport)
        : string =
        // One linear walk of the already-ordered `RouteResult`, writing the top-level object in the
        // FIXED order schemaVersion → selectedGates → findings → cost → cacheEligibilityEvaluated. Every
        // collection is emitted in its existing order (gates by GateId, selecting paths by normalized
        // path, findings in F017 order) — re-sorting NOTHING (FR-005/FR-007). PURE and TOTAL: the empty
        // route yields { schemaVersion, "selectedGates": [], "findings": [], "cost": {0,0,0,0},
        // "cacheEligibilityEvaluated": <bool> } — a valid success.
        //
        // F045: the cache verdict is matched per selected gate by `GateId`. `lookup` is built once —
        // `None` ⇒ every gate `notEvaluated`; `Some report` ⇒ the first-by-report-order verdict map.
        // The top-level `cacheEligibilityEvaluated` flag is `false` for `None`, `true` for `Some _`
        // (the always-present section that survives the empty-gate-list edge — FR-012).
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

            w.WritePropertyName "selectedGates"
            w.WriteStartArray()
            for sg in result.SelectedGates do
                writeSelectedGate w lookup execLookup sg
            w.WriteEndArray()

            w.WritePropertyName "findings"
            w.WriteStartArray()
            for f in result.Findings.Findings do
                writeFinding w f
            w.WriteEndArray()

            w.WritePropertyName "cost"
            writeCost w result.Cost

            w.WriteBoolean("cacheEligibilityEvaluated", Option.isSome cache)

            // F23: the additive product-surface classification, as the document's LAST field. Written ONLY
            // when non-empty; absent (default-empty) ⇒ byte-identical to the F052-era projection (D6).
            match productSurfaces.Classifications with
            | [] -> ()
            | classifications ->
                w.WritePropertyName "productSurfaces"
                w.WriteStartArray()
                for c in classifications do
                    writeProductClassification w c
                w.WriteEndArray()

            w.WriteEndObject())

    let ofRouteResult
        (result: RouteResult)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        : string =
        // The F020/F045/F052 contract, unchanged: an empty product-surface report writes NO productSurfaces
        // field, so this is byte-identical to the pre-F23 projection (existing goldens untouched).
        ofRouteResultWithProductSurfaces result cache execution { Classifications = [] }
