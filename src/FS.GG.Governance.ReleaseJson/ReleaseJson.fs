namespace FS.GG.Governance.ReleaseJson

open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseFactsSensing.Model

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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReleaseJson =

    let schemaVersion = "fsgg.release/v1"

    // ── internal writer plumbing (hidden — absent from ReleaseJson.fsi) ──

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string. Default
    /// `Utf8JsonWriter` options ⇒ no indentation ⇒ deterministic, compact output (the `Json.fs`
    /// `writeToString` precedent shared by F020/F021/F025/F042).
    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    // ── closed-enum token helpers (hidden) — each EXHAUSTIVE with NO wildcard ──

    let surfaceValue (SurfaceId s) : string = s

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
        w.WriteString("baseSeverity", severityToken f.BaseSeverity)
        w.WriteString("effectiveSeverity", severityToken enforced.Decision.EffectiveSeverity)
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

    // ── the public entry point ──

    let ofRelease (decision: ReleaseDecision) (sensed: SensedRelease) : string =
        // One linear walk, writing the top-level object in the FIXED order schemaVersion → verdict →
        // exitCodeBasis → rules → evidence. The verdict/basis are carried VERBATIM from the decision value
        // (never recomputed, never mapped to a numeric exit code). The `rules` are the three partition
        // lists' findings, recovered into the F053 composite order; each `factState` is read from
        // `sensed.Facts` via `Release.factFor`. The `evidence` is the F054 snapshot, re-sorting NOTHING.
        let rules =
            decision.Blockers @ decision.Warnings @ decision.Passing
            |> List.sortBy (fun e -> Release.releaseRuleKindOrdinal e.Finding.Kind, surfaceValue e.Finding.Surface)

        writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WriteString("verdict", verdictToken decision.Verdict)
            w.WriteString("exitCodeBasis", basisToken decision.ExitCodeBasis)
            w.WritePropertyName "rules"
            w.WriteStartArray()
            for r in rules do
                writeRule w sensed.Facts r
            w.WriteEndArray()
            w.WritePropertyName "evidence"
            writeEvidence w sensed.Snapshot
            w.WriteEndObject())
