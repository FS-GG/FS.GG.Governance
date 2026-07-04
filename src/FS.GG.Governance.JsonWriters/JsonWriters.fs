namespace FS.GG.Governance.JsonWriters

// The 073 pure sub-object/map writer leaf. Each member is the byte-identical body the *Json projections
// used to hand-copy: the tagged `cause` object, the two first-by-list-order-wins gate-id maps, and the
// Audit/Route per-gate `execution` object. Token rendering is delegated to JsonTokens. No clock/host/
// filesystem/git/environment/network; output byte-identical to today's projections. No visibility
// modifiers — the surface is JsonWriters.fsi (Principle II).

open System.Text.Json
open FS.GG.Governance.Config.Model             // CheckId, DomainId, CommandId (freshness-key / prerequisite)
open FS.GG.Governance.Gates.Model              // gateIdValue, GateId, GatePrerequisite (RequiresCommand)
open FS.GG.Governance.GateRun.Model            // GateOutcome
open FS.GG.Governance.CommandRecord.Model      // ExitCode
open FS.GG.Governance.EvidenceReuse            // referenceValue
open FS.GG.Governance.EvidenceReuse.Model      // RecomputeCause (NoPriorEvidence, InputsChanged)
open FS.GG.Governance.FreshnessKey.Model       // categoryToken, FreshnessKey
open FS.GG.Governance.CacheEligibility         // entries
open FS.GG.Governance.CacheEligibility.Model   // CacheEligibilityReport, CacheEligibilityVerdict, entry fields
open FS.GG.Governance.JsonTokens               // dispositionToken (module-qualified)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JsonWriters =

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

    let writeExecution (w: Utf8JsonWriter) (outcome: GateOutcome) =
        w.WriteStartObject()
        w.WriteString("disposition", JsonTokens.dispositionToken outcome.Disposition)

        match outcome.Disposition with
        | Executed(ExitCode code, passed)
        | Reused(ExitCode code, passed) ->
            w.WriteNumber("exitCode", code)
            w.WriteBoolean("passed", passed)
        | NotExecuted -> ()

        w.WriteEndObject()

    // 111/A4: the freshness-key / prerequisite / cache-eligibility sub-objects — byte-identical copies the
    // Gates/Route/Audit projections used to hand-hold. (The generated-view and attestation-ref writers are
    // NOT hoisted here: they need RefreshJson / AttestationJson — higher projection layers — which would
    // invert the writer→projection layering; left local, re-deferred on #83.)

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

    let writePrerequisite (w: Utf8JsonWriter) (prereq: GatePrerequisite) =
        w.WriteStartObject()
        let (RequiresCommand(CommandId c)) = prereq
        w.WriteString("requiresCommand", c)
        w.WriteEndObject()

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
