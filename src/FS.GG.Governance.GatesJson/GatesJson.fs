namespace FS.GG.Governance.GatesJson

open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model

// The F021 gates.json projection (US1–US4). Renders the F018 `GateRegistry` into the deterministic,
// versioned `gates.json` WHOLE-CATALOG document text via a hand-driven `System.Text.Json`
// `Utf8JsonWriter` walk — the net10.0 shared-framework mechanism the kernel's `Json.fs` and F020's
// `RouteJson.fs` use, so NO new dependency (FR-015). PURE and TOTAL (FR-008): no I/O, no git, no
// clock, never throws. Emit-only: re-derives/re-sorts/re-classifies NOTHING (the `GateRegistry`
// already fixed the gate order — `GateId` ordinal — and each gate's carried order). No visibility
// modifiers — the surface is GatesJson.fsi (Principle II); every token helper and sub-object writer
// below is hidden by its absence from the .fsi, the `Kernel/Json.fs` + `RouteJson.fs` precedent.
//
// The per-gate field set here is exactly F020's `selectedGates[*]` entry MINUS the route-specific
// `selectingPaths`; the shared gate fields render identically in both artifacts.

open FS.GG.Governance.JsonText // 073: the shared deterministic-emit helper JsonText.writeToString
open FS.GG.Governance.JsonTokens // 073: the shared closed-enum token helpers (module-qualified)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GatesJson =

    /// The declared schema-version token stamped into every emitted document (FR-013). A fixed,
    /// deterministic string literal — never derived from a clock, environment, or input value.
    let schemaVersion = "fsgg.gates/v1"

    // ── internal writer plumbing (hidden — absent from GatesJson.fsi) ──

    // ── closed-enum token helpers (hidden) ──
    // Each `match` is EXHAUSTIVE over the closed DU with NO wildcard (research D3), so a future
    // tier/maturity/environment case is a compile error here, never a silently mis-tokened field.

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
        w.WriteString("cost", JsonTokens.costToken key.Cost)
        w.WriteString("environment", JsonTokens.environmentToken key.Environment)

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

    /// One gate — the documented field order (contracts/gates-json-document.md). Carries the F018
    /// `Gate` VERBATIM (FR-002); `id` via `Gates.gateIdValue`, never re-parsed (FR-010); `cost`/
    /// `maturity` declared verbatim — NOT a weighted scalar, NOT translated to enforcement
    /// (FR-005/FR-011); `timeout` the carried int seconds — NOT re-derived (FR-006). Free-text
    /// `description` is written through the writer's string API so JSON-escaping is the writer's job
    /// (no manual escape, FR-002/FR-012).
    let writeGate (w: Utf8JsonWriter) (gate: Gate) =
        w.WriteStartObject()
        w.WriteString("id", gateIdValue gate.Id)
        let (DomainId domain) = gate.Domain
        w.WriteString("domain", domain)
        w.WriteString("description", gate.Description)
        w.WriteString("cost", JsonTokens.costToken gate.Cost)
        let (TimeoutLimit seconds) = gate.Timeout
        w.WriteNumber("timeout", seconds)
        let (Owner owner) = gate.Owner
        w.WriteString("owner", owner)
        w.WriteString("maturity", JsonTokens.maturityToken gate.Maturity)
        w.WriteBoolean("productCheck", gate.ProductCheck)

        w.WritePropertyName "prerequisites"
        w.WriteStartArray()
        for prereq in gate.Prerequisites do
            writePrerequisite w prereq
        w.WriteEndArray()

        w.WritePropertyName "freshnessKey"
        writeFreshnessKey w gate.FreshnessKey

        w.WriteEndObject()

    // ── the public entry point ──

    let ofGateRegistry (registry: GateRegistry) : string =
        // One linear walk of the already-ordered `GateRegistry`, writing the top-level object in the
        // FIXED order schemaVersion → gates. The gate collection is emitted in its existing GateId
        // ordinal order (fixed by F018 `buildRegistry`), each gate's prerequisites in their carried
        // order — re-sorting NOTHING (FR-007). PURE and TOTAL: the empty registry yields
        // { "schemaVersion": "fsgg.gates/v1", "gates": [] } — a valid success (FR-008/FR-009).
        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)

            w.WritePropertyName "gates"
            w.WriteStartArray()
            for gate in registry.Gates do
                writeGate w gate
            w.WriteEndArray()

            w.WriteEndObject())
