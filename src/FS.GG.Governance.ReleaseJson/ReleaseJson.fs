namespace FS.GG.Governance.ReleaseJson

open System
open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation.Model
open FS.GG.Governance.ReleaseReport.Model

// The F055 release.json projection. Renders the F053 `ReleaseDecision` + the F054 `SensedRelease`
// observed-evidence snapshot into the deterministic, versioned `release.json` WHOLE-RELEASE audit document
// text via a hand-driven `System.Text.Json` `Utf8JsonWriter` walk — the net10.0 shared-framework mechanism
// the kernel's `Json.fs` and F020/F021/F025/F042's projections use, so NO new dependency (FR-014). PURE and
// TOTAL (FR-008): no I/O, no git, no clock, never throws. Emit-only: it re-derives/re-classifies/
// re-evaluates NOTHING (the `ReleaseDecision` already fixed the verdict / basis / partition, and the
// `SensedRelease` already fixed each family's fact state and evidence ordering). No visibility modifiers —
// the surface is ReleaseJson.fsi (Principle II); every token helper and sub-object writer below is hidden by
// its absence from the .fsi, the `AuditJson.fs` precedent. Each closed-enum `match` is EXHAUSTIVE with NO
// wildcard, so a future verdict/basis/severity/factState/outcome/kind case is a compile error here, never a
// silently mis-tokened field.

open FS.GG.Governance.JsonText // 073: the shared deterministic-emit helper JsonText.writeToString
open FS.GG.Governance.JsonTokens // 073: the shared closed-enum token helpers (module-qualified)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReleaseJson =

    let schemaVersion = "fsgg.release/v2"

    // ── internal writer plumbing (hidden — absent from ReleaseJson.fsi) ──

    // ── closed-enum token helpers (hidden) — each EXHAUSTIVE with NO wildcard ──

    let surfaceValue (SurfaceId s) : string = s

    let verdictToken (verdict: Verdict) : string =
        match verdict with
        | Pass -> "pass"
        | Fail -> "fail"

    let factStateToken (state: FactState) : string =
        match state with
        | Met -> "met"
        | Unmet -> "unmet"
        | Unrecoverable -> "unrecoverable"

    let outcomeToken (outcome: RuleOutcome) : string =
        match outcome with
        | Satisfied -> "satisfied"
        | Violated -> "violated"

    // ── rules[] (hidden) — one finding per declared rule, recovered into F053 composite order ──

    /// The decision's three partition lists carry every enforced finding; the projection emits one
    /// `rules[]` entry per finding, recovered into the F053 STABLE COMPOSITE order (`releaseRuleKindOrdinal`
    /// then surface id) — the same order `Release.evaluate` fixed before `rollup` partitioned them. This is
    /// a TOTAL value-keyed re-ordering (never a re-derivation): reordering the input partition lists never
    /// changes the output (SC-003).
    let writeRule (w: Utf8JsonWriter) (facts: ReleaseFacts) (enforced: EnforcedReleaseFinding) =
        let f = enforced.Finding
        w.WriteStartObject()
        w.WriteString("kind", Release.releaseRuleKindToken f.Kind)
        w.WriteString("surface", surfaceValue f.Surface)
        w.WriteString("factState", factStateToken (Release.factFor facts f.Kind))
        w.WriteString("outcome", outcomeToken f.Outcome)
        w.WriteString("baseSeverity", JsonTokens.severityToken f.BaseSeverity)
        w.WriteString("effectiveSeverity", JsonTokens.severityToken enforced.Decision.EffectiveSeverity)
        w.WriteString("reason", f.Reason)
        w.WriteEndObject()

    // ── evidence (hidden) — the F054 ReleaseSnapshot; per-family object or `null` on unrecoverable ──

    let writeStringArray (w: Utf8JsonWriter) (name: string) (items: string list) =
        w.WritePropertyName name
        w.WriteStartArray()
        for s in items do
            w.WriteStringValue s
        w.WriteEndArray()

    /// A pin association list as an array of two-element `[name, version]` arrays (key order is fixed
    /// upstream by F054 — never re-sorted here).
    let writePinPairs (w: Utf8JsonWriter) (name: string) (pairs: (string * string) list) =
        w.WritePropertyName name
        w.WriteStartArray()
        for (k, v) in pairs do
            w.WriteStartArray()
            w.WriteStringValue k
            w.WriteStringValue v
            w.WriteEndArray()
        w.WriteEndArray()

    /// `version` — `{ observed, baseline }` or `null` (unrecoverable).
    let writeVersion (w: Utf8JsonWriter) (fact: VersionFact option) =
        w.WritePropertyName "version"
        match fact with
        | None -> w.WriteNullValue()
        | Some v ->
            w.WriteStartObject()
            w.WriteString("observed", v.Observed)
            w.WriteString("baseline", v.Baseline)
            w.WriteEndObject()

    /// `metadata` — `{ present, missing }` or `null`.
    let writeMetadata (w: Utf8JsonWriter) (fact: MetadataFact option) =
        w.WritePropertyName "metadata"
        match fact with
        | None -> w.WriteNullValue()
        | Some m ->
            w.WriteStartObject()
            writeStringArray w "present" m.Present
            writeStringArray w "missing" m.Missing
            w.WriteEndObject()

    /// `pins` — `{ resolved, expected, drifted }` or `null`.
    let writePins (w: Utf8JsonWriter) (fact: PinsFact option) =
        w.WritePropertyName "pins"
        match fact with
        | None -> w.WriteNullValue()
        | Some p ->
            w.WriteStartObject()
            writePinPairs w "resolved" p.Resolved
            writePinPairs w "expected" p.Expected
            writeStringArray w "drifted" p.Drifted
            w.WriteEndObject()

    /// A posture family (`publishPlan`/`trustedPublishing`/`provenance`) — `{ observed, required, missing }`
    /// or `null`.
    let writePosture (w: Utf8JsonWriter) (name: string) (fact: PostureFact option) =
        w.WritePropertyName name
        match fact with
        | None -> w.WriteNullValue()
        | Some p ->
            w.WriteStartObject()
            writeStringArray w "observed" p.Observed
            writeStringArray w "required" p.Required
            writeStringArray w "missing" p.Missing
            w.WriteEndObject()

    /// One sensing diagnostic — `{ family, reason }`; `family` is the F053 kind token.
    let writeDiagnostic (w: Utf8JsonWriter) (d: SensingDiagnostic) =
        w.WriteStartObject()
        w.WriteString("family", Release.releaseRuleKindToken d.Family)
        w.WriteString("reason", d.Reason)
        w.WriteEndObject()

    let writeEvidence (w: Utf8JsonWriter) (snapshot: ReleaseSnapshot) =
        w.WriteStartObject()
        w.WriteString("surface", surfaceValue snapshot.Surface)
        writeVersion w snapshot.Version
        writeMetadata w snapshot.Metadata
        writePins w snapshot.Pins
        writePosture w "publishPlan" snapshot.PublishPlan
        writePosture w "trustedPublishing" snapshot.TrustedPublishing
        writePosture w "provenance" snapshot.Provenance
        w.WritePropertyName "diagnostics"
        w.WriteStartArray()
        for d in snapshot.Diagnostics do
            writeDiagnostic w d
        w.WriteEndArray()
        w.WriteEndObject()

    // ── F26 additive v2 fields (hidden): packageEvidence, versionPolicy, attestation ──

    let private artifactHashValue (ArtifactHash s) : string = s

    /// `outcome` token — packed | packedNoArtifact | packFailed. Exhaustive, no wildcard.
    let private packOutcomeToken (outcome: PackOutcome) : string =
        match outcome with
        | Packed _ -> "packed"
        | PackedNoArtifact _ -> "packedNoArtifact"
        | PackFailed _ -> "packFailed"

    /// `versionPolicy[].verdict` token — bumped | unbumped | downgraded | noBaseline | notPackable.
    let private versionVerdictToken (v: VersionVerdict) : string =
        match v with
        | Bumped _ -> "bumped"
        | Unbumped _ -> "unbumped"
        | Downgraded _ -> "downgraded"
        | NoBaseline _ -> "noBaseline"
        | NotPackable -> "notPackable"

    let private writeNullableString (w: Utf8JsonWriter) (name: string) (value: string option) =
        match value with
        | Some s -> w.WriteString(name, s)
        | None -> w.WritePropertyName name; w.WriteNullValue()

    let private writeNullableInt (w: Utf8JsonWriter) (name: string) (value: int option) =
        match value with
        | Some i -> w.WriteNumber(name, i)
        | None -> w.WritePropertyName name; w.WriteNullValue()

    /// One package-evidence project — surface/outcome/artifactPath/packedVersion/digest/sentinel/reason.
    /// artifactPath/packedVersion/digest are null when no artifact; sentinel is the pack exit when failed.
    let private writePackProject (w: Utf8JsonWriter) (verdict: PackVerdict) =
        let artifactPath, packedVersion, digest =
            match verdict.Outcome with
            | Packed(art, _) -> Some art.ArtifactPath, Some art.PackedVersion, Some(artifactHashValue art.Digest)
            | PackedNoArtifact _
            | PackFailed _ -> None, None, None

        let sentinel =
            match verdict.Outcome with
            | PackFailed(_, s, _) -> Some s
            | Packed _
            | PackedNoArtifact _ -> None

        w.WriteStartObject()
        w.WriteString("surface", surfaceValue verdict.Surface)
        w.WriteString("outcome", packOutcomeToken verdict.Outcome)
        writeNullableString w "artifactPath" artifactPath
        writeNullableString w "packedVersion" packedVersion
        writeNullableString w "digest" digest
        writeNullableInt w "sentinel" sentinel
        w.WriteString("reason", verdict.Reason)
        w.WriteEndObject()

    /// `packageEvidence` — `{ noPackableProjects, projects[] }`; projects sorted by (surface, artifactPath).
    let private writePackageEvidence (w: Utf8JsonWriter) (pack: PackEvidenceSet) =
        let pathOf (v: PackVerdict) =
            match v.Outcome with
            | Packed(art, _) -> art.ArtifactPath
            | PackedNoArtifact _
            | PackFailed _ -> ""

        let sorted =
            pack.Verdicts
            |> List.sortWith (fun a b ->
                let s = String.CompareOrdinal(surfaceValue a.Surface, surfaceValue b.Surface)
                if s <> 0 then s else String.CompareOrdinal(pathOf a, pathOf b))

        w.WritePropertyName "packageEvidence"
        w.WriteStartObject()
        w.WriteBoolean("noPackableProjects", pack.NoPackableProjects)
        w.WritePropertyName "projects"
        w.WriteStartArray()
        for v in sorted do
            writePackProject w v
        w.WriteEndArray()
        w.WriteEndObject()

    /// `versionPolicy` — `{ projects[] }`; one `{ surface, verdict, baseline, packed }` per project.
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
            |> List.sortWith (fun a b -> String.CompareOrdinal(surfaceValue a.Surface, surfaceValue b.Surface))

        w.WritePropertyName "versionPolicy"
        w.WriteStartObject()
        w.WritePropertyName "projects"
        w.WriteStartArray()
        for v in sorted do
            let baseline, packed = baselineAndPacked v.Version
            w.WriteStartObject()
            w.WriteString("surface", surfaceValue v.Surface)
            w.WriteString("verdict", versionVerdictToken v.Version)
            writeNullableString w "baseline" baseline
            writeNullableString w "packed" packed
            w.WriteEndObject()
        w.WriteEndArray()
        w.WriteEndObject()

    /// `attestation` — a self-contained identity reference, or `null` when there is no attestation input.
    let private writeAttestationRef (w: Utf8JsonWriter) (attestation: AttestationSummary option) =
        w.WritePropertyName "attestation"
        match attestation with
        | None -> w.WriteNullValue()
        | Some a ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", "fsgg.attestation/v1")
            w.WriteString("identity", a.Identity)
            w.WriteString("compliance", "compatible-shape-not-formal-compliance")
            w.WriteNumber("subjectCount", List.length a.Subjects)
            w.WriteEndObject()

    /// Empty package evidence — the retained `ofRelease` path emits an empty package/version block.
    let private emptyPackEvidence: PackEvidenceSet =
        { Verdicts = []; Runs = []; NoPackableProjects = true }

    // ── the v1 body (shared by both entry points) ──

    /// Writes the v1 fields VERBATIM (verdict, exitCodeBasis, rules, evidence) inside an already-open object.
    let private writeV1Body (w: Utf8JsonWriter) (decision: ReleaseDecision) (sensed: SensedRelease) =
        let rules =
            decision.Blockers @ decision.Warnings @ decision.Passing
            |> List.sortBy (fun e -> Release.releaseRuleKindOrdinal e.Finding.Kind, surfaceValue e.Finding.Surface)

        w.WriteString("verdict", verdictToken decision.Verdict)
        w.WriteString("exitCodeBasis", JsonTokens.basisToken decision.ExitCodeBasis)
        w.WritePropertyName "rules"
        w.WriteStartArray()
        for r in rules do
            writeRule w sensed.Facts r
        w.WriteEndArray()
        w.WritePropertyName "evidence"
        writeEvidence w sensed.Snapshot

    // ── the public entry points ──

    let ofRelease (decision: ReleaseDecision) (sensed: SensedRelease) : string =
        // Retained for callers that have only a decision+snapshot: emits a well-formed v2 document with an
        // empty packageEvidence/versionPolicy and a null attestation (no fabricated attestation). The v1
        // fields are byte-identical to v1; the schemaVersion token is the only v1-field change (v1 → v2).
        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            writeV1Body w decision sensed
            writePackageEvidence w emptyPackEvidence
            writeVersionPolicy w emptyPackEvidence
            writeAttestationRef w None
            w.WriteEndObject())

    let ofReleaseReport (report: ReleaseReport) : string =
        // The single-source-of-truth projection: the v1 fields render from report.Decision + report.Sensed,
        // then the three additive fields render from report.Package + report.Attestation. The verify
        // preview and the F27 human projections render from the SAME ReleaseReport (FR-012, D3).
        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            writeV1Body w report.Decision report.Sensed
            writePackageEvidence w report.Package
            writeVersionPolicy w report.Package
            writeAttestationRef w (Some report.Attestation)
            w.WriteEndObject())
