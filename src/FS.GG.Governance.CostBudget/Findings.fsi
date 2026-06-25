// Curated public signature contract for the cost/cache findings (F25).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Findings.fs carries NO access modifiers. Findings surface *why* a gate recomputed or why its evidence was
// rejected — never a silent recompute, never a silently reused stale/synthetic result (FR-007, FR-012,
// SC-004). They are enforced through the EXISTING `Enforcement.deriveEffectiveSeverity`; the F018/F023 truth
// table is NOT re-opened (FR-013).

namespace FS.GG.Governance.CostBudget

open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.CostBudget.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Findings =

    /// Why a gate's evidence could not be cleanly reused.
    ///   • `Stale cats`     — a freshness dimension changed (cache-invalidated); `cats` are the changed F029
    ///                        `InputCategory`s, named verbatim via `FreshnessKey.categoryToken`.
    ///   • `SyntheticTaint` — evidence was produced synthetically rather than by a real run (distinct kind).
    ///   • `NoEvidence`     — no prior evidence existed (the `NoPriorEvidence` cause; never a fabricated reuse).
    type CostFindingKind =
        | Stale of InputCategory list
        | SyntheticTaint
        | NoEvidence

    /// Supplied SENSED taint per gate (research D5) — NOT a field on F030 `RecordedEvidence`, which is
    /// unchanged. `Synthetic` surfaces a `SyntheticTaint` finding even when the freshness key matches.
    type EvidenceTaint =
        | Real
        | Synthetic

    /// One cost/cache finding. `BaseSeverity` is `Advisory` for all kinds — `deriveEffectiveSeverity` never
    /// escalates it (FR-010, FR-013). `Message` names the gate and cause; no raw paths/clock/env (FR-011).
    type CostFinding =
        { Gate: GateId
          Kind: CostFindingKind
          BaseSeverity: Severity
          Message: string }

    /// Derive the findings from the budgeted report + the per-gate taint. PURE, TOTAL, DETERMINISTIC:
    /// findings sorted by (GateId ordinal, kind tag); identical input -> byte-identical list (SC-004).
    /// A clean `Reuse` with `Real` taint yields NO finding for that gate.
    val cacheFindings: report: CacheDecisionReport -> taint: (GateId -> EvidenceTaint) -> CostFinding list

    /// Stable wire token for a kind: "stale" | "syntheticTaint" | "noEvidence". Exhaustive; no wildcard.
    val kindToken: kind: CostFindingKind -> string

    /// Enforce one finding through the F018/F023 truth table VERBATIM (FR-013). Maps the finding's
    /// `BaseSeverity` + a fixed warn-equivalent maturity + the run `mode`/`profile` to an
    /// `EnforcementDecision`. A base-`Advisory` finding ALWAYS derives `Advisory` (never blocks).
    val enforce: mode: RunMode -> profile: Profile -> finding: CostFinding -> EnforcementDecision
