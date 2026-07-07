// The PURE dry-run projections (112). Visibility lives in SimulateProjection.fsi (Principle II) — this file
// carries NO top-level access modifiers. No I/O, no clock: `toJson`/`toText` are deterministic string
// projections of a `Simulate.SimulatedResult`. The JSON is emitted through the shared 073 `JsonText.writeToString`
// leaf (the repo's canonical COMPACT deterministic writer — the same one `AuditJson` uses), under a DISTINCT
// schema id so the real `audit.json` contract stays byte-identical AND the dry-run doc is compact like every
// other repo JSON contract.

namespace FS.GG.Governance.ShipCommand

open System.Text.Json                           // Utf8JsonWriter (the callback param type)
open FS.GG.Governance.Config.Model             // GovernedPath
open FS.GG.Governance.Gates.Model              // gateIdValue
open FS.GG.Governance.Findings.Model           // findingIdToken
open FS.GG.Governance.Ship.Model               // ShipDecision, Verdict, ExitCodeBasis, EnforcedItem, EnforcedItemId
open FS.GG.Governance.Adapters.SddHandoff.Model // Diagnostic, DiagnosticCause
open FS.GG.Governance.JsonText                  // JsonText.writeToString — the shared compact-emit leaf
open FS.GG.Governance.HumanText                 // HumanText.ofShipDecision (the reused verdict projection)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SimulateProjection =

    let schemaVersion = "fsgg.audit.dryrun/v1"

    // ── token maps (total over each closed union) ──

    let verdictToken (v: Verdict) : string =
        match v with
        | Pass -> "pass"
        | Fail -> "fail"

    let basisToken (b: ExitCodeBasis) : string =
        match b with
        | Clean -> "clean"
        | ExitCodeBasis.Blocked -> "blocked"

    let classToken (c: Simulate.SignalClass) : string =
        match c with
        | Simulate.RequiredSatisfied -> "requiredSatisfied"
        | Simulate.RequiredAbsent -> "requiredAbsent"
        | Simulate.NotRequired -> "notRequired"

    let causeToken (c: DiagnosticCause) : string =
        match c with
        | VersionMismatch -> "versionMismatch"
        | Malformed -> "malformed"
        | AutoSyntheticDeclared -> "autoSyntheticDeclared"
        | StaleEvidence -> "staleEvidence"

    // The stable identity token for an enforced item (gate id, or finding token + governed path).
    let idToken (id: EnforcedItemId) : string =
        match id with
        | GateItem g -> gateIdValue g
        | FindingItem(f, GovernedPath p) -> findingIdToken f + ":" + p

    // ── JSON (US3) — distinct schema id + simulated marker; fixed key order; deterministic ──

    let toJson (result: Simulate.SimulatedResult) : string =
        let d = result.Decision
        let suf = result.Sufficiency

        JsonText.writeToString (fun (writer: Utf8JsonWriter) ->
            let writeIdArray (name: string) (items: EnforcedItem list) =
                writer.WriteStartArray(name)
                items |> List.iter (fun (it: EnforcedItem) -> writer.WriteStringValue(idToken it.Id))
                writer.WriteEndArray()

            writer.WriteStartObject()
            writer.WriteString("schemaVersion", schemaVersion)
            writer.WriteBoolean("simulated", true)
            writer.WriteString("verdict", verdictToken d.Verdict)
            writer.WriteString("exitCodeBasis", basisToken d.ExitCodeBasis)
            writeIdArray "blockers" d.Blockers
            writeIdArray "warnings" d.Warnings
            writeIdArray "passing" d.Passing

            writer.WriteStartObject("sufficiency")
            writer.WriteNumber("requiredAbsentCount", suf.RequiredAbsentCount)
            writer.WriteBoolean("allNotEvaluated", suf.AllNotEvaluated)
            writer.WriteStartArray("signals")
            suf.Signals
            |> List.iter (fun (s: Simulate.SignalSufficiency) ->
                writer.WriteStartObject()
                writer.WriteString("signal", s.Signal)
                writer.WriteString("class", classToken s.Class)
                writer.WriteEndObject())
            writer.WriteEndArray()
            writer.WriteEndObject()

            writer.WriteStartArray("handoffDiagnostics")
            result.HandoffDiagnostics
            |> List.iter (fun (dg: Diagnostic) ->
                writer.WriteStartObject()
                writer.WriteString("cause", causeToken dg.Cause)
                writer.WriteString("source", dg.Source)
                writer.WriteString("message", dg.Message)
                writer.WriteEndObject())
            writer.WriteEndArray()

            writer.WriteEndObject())

    // ── human text (US1/US2) — banner + reused verdict view + sufficiency + diagnostics ──

    let toText (result: Simulate.SimulatedResult) : string =
        let d = result.Decision
        let suf = result.Sufficiency

        let signalsOf (c: Simulate.SignalClass) =
            suf.Signals |> List.filter (fun s -> s.Class = c) |> List.map (fun s -> s.Signal)

        let section (label: string) (ids: string list) : string list =
            match ids with
            | [] -> []
            | _ -> (sprintf "  %s:" label) :: (ids |> List.map (fun i -> "    - " + i))

        let sufficiencyLines =
            [ ""
              "Sufficiency (handoff evidence signals):" ]
            @ section (sprintf "required-absent (%d)" suf.RequiredAbsentCount) (signalsOf Simulate.RequiredAbsent)
            @ section "required-satisfied" (signalsOf Simulate.RequiredSatisfied)
            @ section "not-required" (signalsOf Simulate.NotRequired)
            @ (if suf.AllNotEvaluated then
                   [ "  all-not-evaluated: nothing real was carried — this is the notEvaluated failure mode (absence, not a pass)." ]
               else
                   [])

        let diagnosticLines =
            match result.HandoffDiagnostics with
            | [] -> []
            | diags ->
                ""
                :: "Handoff diagnostics:"
                :: (diags
                    |> List.map (fun dg -> sprintf "  - %s: %s (%s)" (causeToken dg.Cause) dg.Message dg.Source))

        [ "SIMULATED (dry-run) — not a real gate result"
          sprintf "verdict: %s (%s)" (verdictToken d.Verdict) (basisToken d.ExitCodeBasis)
          "gates not executed (dry-run); the verdict reflects the pre-execution state."
          ""
          HumanText.ofShipDecision d None [] ]
        @ sufficiencyLines
        @ diagnosticLines
        |> String.concat "\n"
