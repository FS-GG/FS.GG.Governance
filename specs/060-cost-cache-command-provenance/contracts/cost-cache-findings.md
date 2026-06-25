# Contract: Cost/Cache Findings (`FS.GG.Governance.CostBudget.Findings`)

Pure. Surfaces *why* a gate recomputed or why its evidence was rejected, as deterministic findings — never a
silent recompute, never a silently reused stale/synthetic result. FR-007, FR-012, FR-013, SC-004. Enforced
through the **existing** `Enforcement.deriveEffectiveSeverity`; the truth table is **not** re-opened.

## `Findings.fsi`

```fsharp
namespace FS.GG.Governance.CostBudget

open FS.GG.Governance.Gates.Model               // GateId
open FS.GG.Governance.FreshnessKey.Model          // InputCategory
open FS.GG.Governance.Enforcement.Enforcement     // Severity, Profile, RunMode, EnforcementDecision
open FS.GG.Governance.CostBudget.Model             // CacheDecisionReport

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
    val cacheFindings:
        report: CacheDecisionReport ->
        taint: (GateId -> EvidenceTaint) ->
            CostFinding list

    /// Stable wire token for a kind: "stale" | "syntheticTaint" | "noEvidence". Exhaustive; no wildcard.
    val kindToken: kind: CostFindingKind -> string

    /// Enforce one finding through the F018/F023 truth table VERBATIM (FR-013). Maps the finding's
    /// `BaseSeverity` + a fixed warn-equivalent maturity + the run `mode`/`profile` to an
    /// `EnforcementDecision`. A base-`Advisory` finding ALWAYS derives `Advisory` (never blocks).
    val enforce:
        mode: RunMode ->
        profile: Profile ->
        finding: CostFinding ->
            EnforcementDecision
```

## Rules

- **Stale ⇔ a freshness dimension changed.** Emitted iff the gate's decision (`Recompute` or `OverBudget`)
  derives from `InputsChanged cats`; the finding names exactly those `cats` — no new dimension, no second
  opinion (it reuses the F041/F030 cause).
- **SyntheticTaint is independent of freshness.** Emitted for any gate whose supplied taint is `Synthetic`,
  including a gate whose decision is `Reuse` (spec edge "synthetic evidence reused") — a synthetic result is
  never silently reused as if real. Distinguishable from `Stale` by kind.
- **NoEvidence is not an error.** It records that the first run for a gate had nothing to reuse — a clear input
  state, not a tool defect (FR-012). The gate still recomputes (subject to the budget).
- **Clean reuse is silent.** A `Reuse` decision with `Real` taint emits nothing — no fabricated "all clear"
  finding (SC-004).
- **Never blocks.** Because every kind carries `BaseSeverity = Advisory` and `deriveEffectiveSeverity` never
  escalates advisory, no cost/cache finding can block under any (`Profile`, `RunMode`) (SC-007 family). F25
  does **not** invoke `AdvisoryPromotion` (F039).
