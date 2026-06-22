// Per-gate freshness-inputs resolution operations for the resolution (join) core (F043). The public surface is
// fixed by FreshnessResolution.fsi (Principle II); no top-level binding here carries an access modifier (the
// per-gate join helper, the ordinal sort comparator, and the token map are private by their absence from the
// .fsi). The operations are pure, total, and deterministic (FR-008, FR-009): no clock, filesystem, git,
// environment, or network; no command run; no hash/freshness key/digest computed; no cache eligibility
// evaluated; none of the supplied newtypes parsed, re-hashed, or fabricated; identical inputs always yield the
// identical report. The join is fixed by contracts/freshness-resolution-api.md and data-model.md.

namespace FS.GG.Governance.FreshnessResolution

open System
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.FreshnessResolution.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FreshnessResolution =

    // The pure per-gate join. For a gate's carried `FreshnessKey` against the supplied `SensedFacts`, collect the
    // missing facts in FR-002 field order (no-hide), and if NONE are missing build the complete `FreshnessInputs`
    // (the four identity fields from the key — dropping `Cost` — and the six sensed fields verbatim), else name
    // every gap in an `Unresolved`. Private by its absence from the .fsi. Fabricates/defaults/zero-fills nothing.
    let private resolveGate (gate: Gate) (sensed: SensedFacts) : FreshnessResolutionEntry =
        let fk = gate.FreshnessKey

        // The command version is sensed ONLY for a command-bearing gate; a command-less gate has no version and
        // is never unresolved on that basis (FR-005).
        let commandVersion =
            fk.Command |> Option.bind (fun c -> Map.tryFind c sensed.CommandVersions)

        let missing =
            [ if Option.isNone sensed.RuleHash then
                  MissingRuleHash
              if not (Map.containsKey gate.Id sensed.CoveredArtifacts) then
                  MissingCoveredArtifacts
              match fk.Command with
              | Some c when not (Map.containsKey c sensed.CommandVersions) -> MissingCommandVersion
              | _ -> ()
              if Option.isNone sensed.GeneratorVersion then
                  MissingGeneratorVersion
              if Option.isNone sensed.Base then
                  MissingBaseRevision
              if Option.isNone sensed.Head then
                  MissingHeadRevision ]

        let outcome =
            match missing with
            | [] ->
                Resolved
                    { Check = fk.Check
                      Domain = fk.Domain
                      Command = fk.Command
                      Environment = fk.Environment
                      RuleHash = Option.get sensed.RuleHash
                      CoveredArtifacts = Map.find gate.Id sensed.CoveredArtifacts
                      CommandVersion = commandVersion
                      GeneratorVersion = Option.get sensed.GeneratorVersion
                      Base = Option.get sensed.Base
                      Head = Option.get sensed.Head }
            | facts -> Unresolved facts

        { Gate = gate.Id; Outcome = outcome }

    // The total, input-order-independent sort comparator: ordinal on the `GateId` wire string first, then a
    // structural comparison of the whole entry as the duplicate-`GateId` tiebreak. Private by its absence from
    // the .fsi. Computes no freshness key or hash — only string/structural comparison.
    let private compareEntries (a: FreshnessResolutionEntry) (b: FreshnessResolutionEntry) : int =
        match String.CompareOrdinal(gateIdValue a.Gate, gateIdValue b.Gate) with
        | 0 -> compare a b
        | n -> n

    let resolve (gates: Gate list) (sensed: SensedFacts) : FreshnessResolutionReport =
        // One attributed outcome per gate, then a stable ordinal sort with a total structural tiebreak; every
        // gate preserved, duplicates kept; empty input ⇒ empty report.
        gates
        |> List.map (fun g -> resolveGate g sensed)
        |> List.sortWith compareEntries
        |> FreshnessResolutionReport

    let entries (report: FreshnessResolutionReport) : FreshnessResolutionEntry list =
        let (FreshnessResolutionReport xs) = report
        xs

    let candidate (entry: FreshnessResolutionEntry) : CandidateGate option =
        // The F041 bridge — the ONLY function producing a `CandidateGate`. Recompute-safe by construction: an
        // `Unresolved` entry carries no `FreshnessInputs`, so it can never become a candidate (FR-004/FR-010).
        match entry.Outcome with
        | Resolved inputs -> Some { Gate = entry.Gate; Inputs = inputs }
        | Unresolved _ -> None

    let isResolved (outcome: ResolutionOutcome) : bool =
        match outcome with
        | Resolved _ -> true
        | Unresolved _ -> false

    let missingFacts (outcome: ResolutionOutcome) : MissingFact list =
        match outcome with
        | Resolved _ -> []
        | Unresolved facts -> facts

    // The stable, injective wire-token map — wildcard-free so a future `MissingFact` case is a compile error
    // here (Principle III). Mirrors F029 `categoryToken` and the F042 token precedent.
    let missingFactToken (fact: MissingFact) : string =
        match fact with
        | MissingRuleHash -> "ruleHash"
        | MissingCoveredArtifacts -> "coveredArtifacts"
        | MissingCommandVersion -> "commandVersion"
        | MissingGeneratorVersion -> "generatorVersion"
        | MissingBaseRevision -> "baseRevision"
        | MissingHeadRevision -> "headRevision"
