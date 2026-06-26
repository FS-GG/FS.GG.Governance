namespace FS.GG.Governance.VerifyJson

open System.IO
open System.Text
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
open FS.GG.Governance.PackEvidence.Model         // F26: PackEvidenceSet / PackVerdict / VersionVerdict
open FS.GG.Governance.Attestation.Model          // F26: AttestationSummary
open FS.GG.Governance.ReleaseReport.Model        // F26: VerifyReleasePreview

module SC = FS.GG.Governance.SurfaceChecks.Model // F24: the additive surfaceChecks section

// The F056 verify.json projection. Renders the F024 `ShipDecision` (rolled at `RunMode.Verify`) + the F041
// `CacheEligibilityReport` + the F052 per-gate execution outcomes into the deterministic, versioned
// `verify.json` WHOLE-CHANGE pre-PR verification document text via a hand-driven `System.Text.Json`
// `Utf8JsonWriter` walk — the net10.0 shared-framework mechanism the kernel's `Json.fs` and
// F020/F021/F025/F042/F055's projections use, so NO new dependency (FR-014). PURE and TOTAL (FR-008): no I/O,
// no git, no clock, never throws. Emit-only: it re-derives/re-classifies/re-partitions/re-sorts NOTHING (the
// `ShipDecision` fixed the verdict / basis / partition, the `CacheEligibilityReport` fixed each per-gate
// verdict, the execution list fixed each per-gate disposition). No visibility modifiers — the surface is
// VerifyJson.fsi (Principle II); every token helper and sub-object writer below is hidden by its absence from
// the .fsi, the `AuditJson.fs` precedent. Each closed-enum `match` is EXHAUSTIVE with NO wildcard, so a future
// verdict/basis/severity/maturity/profile/disposition/cause case is a compile error here, never a silently
// mis-tokened field. `mode` is the fixed literal `"verify"` (the command threads only `RunMode.Verify`).

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VerifyJson =

    let schemaVersion = "fsgg.verify/v1"

    // ── internal writer plumbing (hidden — absent from VerifyJson.fsi) ──

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string. Default
    /// `Utf8JsonWriter` options ⇒ no indentation ⇒ deterministic, compact output (the `Json.fs`
    /// `writeToString` precedent shared by F020/F021/F025/F042/F055).
    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    // ── closed-enum token helpers (hidden) — each EXHAUSTIVE with NO wildcard ──

    let verdictToken (verdict: Verdict) : string =
        match verdict with
        | Pass -> "pass"
        | Fail -> "blocked"

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

    let profileToken (profile: Profile) : string =
        match profile with
        | Light -> "light"
        | Standard -> "standard"
        | Strict -> "strict"
        | Profile.Release -> "release"

    let dispositionToken (disposition: GateDisposition) : string =
        match disposition with
        | Executed -> "executed"
        | Reused -> "reused"
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
        w.WriteString("baseSeverity", severityToken d.BaseSeverity)
        w.WriteString("maturity", maturityToken d.Maturity)
        w.WriteString("mode", "verify")
        w.WriteString("profile", profileToken d.Profile)
        w.WriteString("effectiveSeverity", severityToken d.EffectiveSeverity)
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

            match o.ExitCode with
            | Some(ExitCode code) -> w.WriteNumber("exitCode", code)
            | None -> w.WriteNull("exitCode")

            match o.Passed with
            | Some passed -> w.WriteBoolean("passed", passed)
            | None -> w.WriteNull("passed")

            w.WriteEndObject()
        | None -> w.WriteNullValue()

    // ── per-gate lookups, first-by-list-order-wins (the AuditJson `verdictByGate`/`outcomeByGate` precedent) ──

    let verdictByGate (report: CacheEligibilityReport) : Map<string, CacheEligibilityVerdict> =
        CacheEligibility.entries report
        |> List.fold
            (fun m e ->
                let k = gateIdValue e.Gate
                if Map.containsKey k m then m else Map.add k e.Verdict m)
            Map.empty

    let outcomeByGate (execution: (GateId * GateOutcome) list) : Map<string, GateOutcome> =
        execution
        |> List.fold
            (fun m (gid, outcome) ->
                let k = gateIdValue gid
                if Map.containsKey k m then m else Map.add k outcome m)
            Map.empty

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
        |> List.fold (fun acc g -> if List.contains g acc then acc else acc @ [ g ]) []

    let writeCurrency (w: Utf8JsonWriter) (decision: ShipDecision) (cache: CacheEligibilityReport option) =
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

        // unresolved ⇐ selected gate with no cache entry → { gate, missing: [] }. The missing-fact tokens
        // are surfaced richly in the command's TEXT render (which holds `Model.Sensed`); the cache report
        // alone cannot reconstruct them, so the projection emits a present, empty `missing` array.
        w.WritePropertyName "unresolved"
        w.WriteStartArray()

        for g in unresolved do
            w.WriteStartObject()
            w.WriteString("gate", g)
            w.WritePropertyName "missing"
            w.WriteStartArray()
            w.WriteEndArray()
            w.WriteEndObject()

        w.WriteEndArray()

        w.WriteEndObject()

    // ── F24: the additive `surfaceChecks` element — `{ domain, surface, code, file, detail, severity,
    //    inputState, evidenceTag?, message }`. `evidenceTag` is emitted ONLY when the surface declared one
    //    (FR-009); omitted otherwise. `severity` is the BASE severity (advisory entries appear but never
    //    change the exit code — FR-011). Deterministic: no abs-path/clock/username (FR-010). ──

    let writeSurfaceFinding (w: Utf8JsonWriter) (f: SC.SurfaceFinding) =
        w.WriteStartObject()
        w.WriteString("domain", SC.checkDomainToken f.Domain)
        let (SurfaceId sid) = f.Surface
        w.WriteString("surface", sid)
        w.WriteString("code", f.Code)
        // 068: the surface finding's `ruleId` (`surface:<domain>:<code>`), emitted after the leading
        // domain/surface/code identity triple and before the detail fields. Derived from the same domain +
        // code already written above; reads no severity / message, so it is profile/mode/message-invariant.
        w.WriteString("ruleId", RuleIdentity.ruleIdToken (RuleIdentity.surface (SC.checkDomainToken f.Domain) f.Code))
        let (GovernedPath file) = f.Location.File
        w.WriteString("file", file)
        w.WriteString("detail", f.Location.Detail)
        w.WriteString("severity", severityToken f.BaseSeverity)
        w.WriteBoolean("inputState", f.IsInputState)

        match f.EvidenceTag with
        | Some(EvidenceTag t) -> w.WriteString("evidenceTag", t)
        | None -> ()

        w.WriteString("message", f.Message)
        w.WriteEndObject()

    // ── F26: the additive `releaseReadiness` advisory preview block. Mirrors the release.json v2
    //    packageEvidence/versionPolicy/attestation shape (the cores own their own writers — the codebase
    //    convention each projection duplicates its token helpers). `advisory` is ALWAYS true; verify's exit
    //    code is decided WITHOUT it (FR-005). Written ONLY when a preview is present; absent ⇒ byte-identical
    //    to the pre-F26 projection (no schemaVersion bump — fsgg.verify/v1 stays). ──

    let private rrSurfaceValue (SurfaceId s) : string = s
    let private rrArtifactHashValue (ArtifactHash s) : string = s

    let private rrVerdictToken (verdict: Verdict) : string =
        match verdict with
        | Pass -> "pass"
        | Fail -> "fail"

    let private rrPackOutcomeToken (outcome: PackOutcome) : string =
        match outcome with
        | Packed _ -> "packed"
        | PackedNoArtifact _ -> "packedNoArtifact"
        | PackFailed _ -> "packFailed"

    let private rrVersionVerdictToken (v: VersionVerdict) : string =
        match v with
        | Bumped _ -> "bumped"
        | Unbumped _ -> "unbumped"
        | Downgraded _ -> "downgraded"
        | NoBaseline _ -> "noBaseline"
        | NotPackable -> "notPackable"

    let private rrNullableString (w: Utf8JsonWriter) (name: string) (value: string option) =
        match value with
        | Some s -> w.WriteString(name, s)
        | None -> w.WritePropertyName name; w.WriteNullValue()

    let private rrNullableInt (w: Utf8JsonWriter) (name: string) (value: int option) =
        match value with
        | Some i -> w.WriteNumber(name, i)
        | None -> w.WritePropertyName name; w.WriteNullValue()

    let private rrPathOf (v: PackVerdict) : string =
        match v.Outcome with
        | Packed(art, _) -> art.ArtifactPath
        | PackedNoArtifact _
        | PackFailed _ -> ""

    let private writePackProject (w: Utf8JsonWriter) (verdict: PackVerdict) =
        let artifactPath, packedVersion, digest =
            match verdict.Outcome with
            | Packed(art, _) -> Some art.ArtifactPath, Some art.PackedVersion, Some(rrArtifactHashValue art.Digest)
            | PackedNoArtifact _
            | PackFailed _ -> None, None, None

        let sentinel =
            match verdict.Outcome with
            | PackFailed(_, s, _) -> Some s
            | Packed _
            | PackedNoArtifact _ -> None

        w.WriteStartObject()
        w.WriteString("surface", rrSurfaceValue verdict.Surface)
        w.WriteString("outcome", rrPackOutcomeToken verdict.Outcome)
        rrNullableString w "artifactPath" artifactPath
        rrNullableString w "packedVersion" packedVersion
        rrNullableString w "digest" digest
        rrNullableInt w "sentinel" sentinel
        w.WriteString("reason", verdict.Reason)
        w.WriteEndObject()

    let private writePackageEvidence (w: Utf8JsonWriter) (pack: PackEvidenceSet) =
        let sorted =
            pack.Verdicts
            |> List.sortWith (fun a b ->
                let s = System.String.CompareOrdinal(rrSurfaceValue a.Surface, rrSurfaceValue b.Surface)
                if s <> 0 then s else System.String.CompareOrdinal(rrPathOf a, rrPathOf b))

        w.WritePropertyName "packageEvidence"
        w.WriteStartObject()
        w.WriteBoolean("noPackableProjects", pack.NoPackableProjects)
        w.WritePropertyName "projects"
        w.WriteStartArray()
        for v in sorted do
            writePackProject w v
        w.WriteEndArray()
        w.WriteEndObject()

    let private writeVersionPolicy (w: Utf8JsonWriter) (pack: PackEvidenceSet) =
        let baselineAndPacked (v: VersionVerdict) =
            match v with
            | Bumped(b, p) -> Some b, Some p
            | Downgraded(b, p) -> Some b, Some p
            | Unbumped p -> None, Some p
            | NoBaseline p -> None, Some p
            | NotPackable -> None, None

        let sorted =
            pack.Verdicts
            |> List.sortWith (fun a b -> System.String.CompareOrdinal(rrSurfaceValue a.Surface, rrSurfaceValue b.Surface))

        w.WritePropertyName "versionPolicy"
        w.WriteStartObject()
        w.WritePropertyName "projects"
        w.WriteStartArray()
        for v in sorted do
            let baseline, packed = baselineAndPacked v.Version
            w.WriteStartObject()
            w.WriteString("surface", rrSurfaceValue v.Surface)
            w.WriteString("verdict", rrVersionVerdictToken v.Version)
            rrNullableString w "baseline" baseline
            rrNullableString w "packed" packed
            w.WriteEndObject()
        w.WriteEndArray()
        w.WriteEndObject()

    let private writeAttestationRef (w: Utf8JsonWriter) (attestation: AttestationSummary) =
        w.WritePropertyName "attestation"
        w.WriteStartObject()
        w.WriteString("schemaVersion", "fsgg.attestation/v1")
        w.WriteString("identity", attestation.Identity)
        w.WriteString("compliance", "compatible-shape-not-formal-compliance")
        w.WriteNumber("subjectCount", List.length attestation.Subjects)
        w.WriteEndObject()

    let writeReleaseReadiness (w: Utf8JsonWriter) (preview: VerifyReleasePreview) =
        w.WritePropertyName "releaseReadiness"
        w.WriteStartObject()
        w.WriteBoolean("advisory", preview.Advisory)
        w.WriteString("verdict", rrVerdictToken preview.Verdict)
        writePackageEvidence w preview.Package
        writeVersionPolicy w preview.Package
        writeAttestationRef w preview.Attestation
        w.WriteEndObject()

    // ── the shared core (hidden) — schemaVersion → currency → optional surfaceChecks, no open/close ──

    let private writeCore
        (w: Utf8JsonWriter)
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (findings: SC.SurfaceFinding list)
        =
        let lookup: GateId -> CacheEligibilityVerdict option =
            match cache with
            | None -> fun _ -> None
            | Some report ->
                let byGate = verdictByGate report
                fun gateId -> Map.tryFind (gateIdValue gateId) byGate

        let execByGate = outcomeByGate execution
        let execLookup: GateId -> GateOutcome option = fun gateId -> Map.tryFind (gateIdValue gateId) execByGate

        w.WriteString("schemaVersion", schemaVersion)
        w.WriteString("verdict", verdictToken decision.Verdict)
        w.WriteString("exitCodeBasis", basisToken decision.ExitCodeBasis)
        writeSection w lookup execLookup "blockers" decision.Blockers
        writeSection w lookup execLookup "warnings" decision.Warnings
        writeSection w lookup execLookup "passing" decision.Passing
        writeCurrency w decision cache

        // F24: the additive product-surface findings. Written ONLY when non-empty; absent ⇒ byte-identical
        // to the pre-F24 projection (D8). Emitted in the Composition.run order the caller already fixed.
        match findings with
        | [] -> ()
        | _ ->
            w.WritePropertyName "surfaceChecks"
            w.WriteStartArray()
            for f in findings do
                writeSurfaceFinding w f
            w.WriteEndArray()

    // ── the public entry points ──

    let ofVerifyDecisionWithSurfaceChecks
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (findings: SC.SurfaceFinding list)
        : string =
        writeToString (fun w ->
            w.WriteStartObject()
            writeCore w decision cache execution findings
            w.WriteEndObject())

    /// The F056 contract, unchanged: no surface findings ⇒ NO `surfaceChecks` field, so this is
    /// byte-identical to the pre-F24 projection (existing goldens untouched).
    let ofVerifyDecision
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        : string =
        ofVerifyDecisionWithSurfaceChecks decision cache execution []

    /// F26 (additive, non-breaking): the same projection plus an advisory `releaseReadiness` block carrying
    /// the F26 VerifyReleasePreview. The block is emitted as the document's LAST top-level field ONLY when
    /// `preview` is `Some`; a `None` preview writes NO block, so the output is BYTE-IDENTICAL to
    /// `ofVerifyDecisionWithSurfaceChecks decision cache execution findings` (no `schemaVersion` bump —
    /// fsgg.verify/v1 stays). PURE and TOTAL.
    let ofVerifyDecisionWithPreview
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (findings: SC.SurfaceFinding list)
        (preview: VerifyReleasePreview option)
        : string =
        writeToString (fun w ->
            w.WriteStartObject()
            writeCore w decision cache execution findings

            match preview with
            | None -> ()
            | Some p -> writeReleaseReadiness w p

            w.WriteEndObject())
