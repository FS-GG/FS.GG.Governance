namespace FS.GG.Governance.AuditJson

open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model

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
    /// deterministic string literal — never derived from a clock, environment, or input value.
    let schemaVersion = "fsgg.audit/v1"

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

    /// One enforced item — a TAGGED object discriminated by `kind` (research D5). Matches the closed
    /// `EnforcedItemId` exhaustively (no wildcard):
    ///   • `GateItem g`                       → `kind:"gate"`, `id` (via `gateIdValue`, never re-parsed
    ///                                          even across a `:` separator — FR-010), `enforcement`.
    ///   • `FindingItem (fid, GovernedPath p)`→ `kind:"finding"`, `id` (via `findingIdToken`), `path`
    ///                                          (the unwrapped `GovernedPath` verbatim — FR-010),
    ///                                          `enforcement`.
    /// A gate item has NO `path` field (absent, not `null`; the `kind` tag disambiguates).
    let writeItem (w: Utf8JsonWriter) (item: EnforcedItem) =
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

        w.WriteEndObject()

    /// One section array — walks the carried item list in its existing F024 composite order, emitting
    /// each item via `writeItem`, re-sorting NOTHING (FR-007). An empty list emits a present, empty
    /// array (FR-005, FR-009).
    let writeSection (w: Utf8JsonWriter) (name: string) (items: EnforcedItem list) =
        w.WritePropertyName name
        w.WriteStartArray()
        for item in items do
            writeItem w item
        w.WriteEndArray()

    // ── the public entry point ──

    let ofShipDecision (decision: ShipDecision) : string =
        // One linear walk of the already-ordered `ShipDecision`, writing the top-level object in the
        // FIXED order schemaVersion → verdict → exitCodeBasis → blockers → warnings → passing. The
        // verdict/basis are carried VERBATIM from the decision value — never recomputed from the item
        // sections (FR-002), never mapped to a numeric process exit code (FR-003). Each section is
        // emitted in its existing composite order, re-sorting NOTHING (FR-007). PURE and TOTAL: the
        // empty/clean decision yields three present, empty arrays with verdict:"pass" /
        // exitCodeBasis:"clean" — a valid success (FR-008/FR-009).
        writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WriteString("verdict", verdictToken decision.Verdict)
            w.WriteString("exitCodeBasis", basisToken decision.ExitCodeBasis)
            writeSection w "blockers" decision.Blockers
            writeSection w "warnings" decision.Warnings
            writeSection w "passing" decision.Passing
            w.WriteEndObject())
