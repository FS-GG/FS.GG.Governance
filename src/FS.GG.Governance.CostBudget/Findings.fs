namespace FS.GG.Governance.CostBudget

open System
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.CostBudget.Model

// The F25 cost/cache findings (US3). PURE and TOTAL: no I/O, no clock, never throws. A finding surfaces why a
// gate recomputed (stale / no-evidence) or that its evidence is synthetic, naming the gate and cause with no
// raw paths/clock/env (FR-011). Every finding is base-`Advisory` and enforced through the EXISTING
// `deriveEffectiveSeverity` — the truth table is reused verbatim, never re-opened (FR-013), so a cost/cache
// finding can never block (FR-010). No access modifiers — the surface is Findings.fsi (Principle II).

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Findings =

    type CostFindingKind =
        | Stale of InputCategory list
        | SyntheticTaint
        | NoEvidence

    type EvidenceTaint =
        | Real
        | Synthetic

    type CostFinding =
        { Gate: GateId
          Kind: CostFindingKind
          BaseSeverity: Severity
          Message: string }

    let kindToken (kind: CostFindingKind) : string =
        match kind with
        | Stale _ -> "stale"
        | SyntheticTaint -> "syntheticTaint"
        | NoEvidence -> "noEvidence"

    // ── hidden helpers (absent from Findings.fsi) ──

    /// The kind's tag rank, in declaration order, for the deterministic (GateId, kind) sort.
    let kindRank (kind: CostFindingKind) : int =
        match kind with
        | Stale _ -> 0
        | SyntheticTaint -> 1
        | NoEvidence -> 2

    /// Deterministic message for a kind, naming the gate and cause — no raw path/clock/env (FR-011).
    let message (gate: GateId) (kind: CostFindingKind) : string =
        let g = gateIdValue gate

        match kind with
        | Stale cats ->
            let named = cats |> List.map categoryToken |> String.concat ", "
            sprintf "gate %s: evidence stale — changed freshness dimension(s): %s" g named
        | SyntheticTaint -> sprintf "gate %s: evidence is synthetic, not from a real run" g
        | NoEvidence -> sprintf "gate %s: no prior evidence to reuse" g

    let advisory (gate: GateId) (kind: CostFindingKind) : CostFinding =
        { Gate = gate
          Kind = kind
          BaseSeverity = Advisory
          Message = message gate kind }

    /// The cause-based finding (if any) for one entry: a `Stale` for a changed freshness dimension (whether
    /// the gate recomputed or was over-budget), a `NoEvidence` for a recompute that had nothing to reuse, and
    /// nothing for a clean `Reuse` (synthetic taint is handled separately and is independent of the cause).
    let causeFinding (entry: CacheDecisionEntry) : CostFinding option =
        match entry.Decision with
        | Recompute(InputsChanged cats) -> Some(advisory entry.Gate (Stale cats))
        | Recompute NoPriorEvidence -> Some(advisory entry.Gate NoEvidence)
        | OverBudget reason ->
            match reason.Cause with
            | InputsChanged cats -> Some(advisory entry.Gate (Stale cats))
            | NoPriorEvidence -> None
        | Reuse _ -> None

    let cacheFindings (report: CacheDecisionReport) (taint: GateId -> EvidenceTaint) : CostFinding list =
        let (CacheDecisionReport entries) = report

        entries
        |> List.collect (fun entry ->
            let cause = causeFinding entry |> Option.toList

            let synthetic =
                match taint entry.Gate with
                | Synthetic -> [ advisory entry.Gate SyntheticTaint ]
                | Real -> []

            cause @ synthetic)
        |> List.sortWith (fun a b ->
            let c = String.CompareOrdinal(gateIdValue a.Gate, gateIdValue b.Gate)
            if c <> 0 then c else compare (kindRank a.Kind) (kindRank b.Kind))

    let enforce (mode: RunMode) (profile: Profile) (finding: CostFinding) : EnforcementDecision =
        deriveEffectiveSeverity
            { BaseSeverity = finding.BaseSeverity
              Maturity = Warn
              Mode = mode
              Profile = profile }
