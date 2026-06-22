// Curated public signature contract for the per-gate cache-eligibility types (F041).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body exists
// (Principle I). These are the supplied per-gate inputs and the named outcomes that `CacheEligibility.evaluate`
// / `evaluateGate` work over. They REUSE the F018 `GateId` (opened from `FS.GG.Governance.Gates.Model`), the
// F029 `FreshnessInputs` / `InputCategory` (opened from `FS.GG.Governance.FreshnessKey.Model`), and the F030
// `ReuseStore` / `ReuseDecision` / `RecomputeCause` / `EvidenceRef` (opened from
// `FS.GG.Governance.EvidenceReuse.Model`) VERBATIM, never redefined (FR-012). The only new vocabulary is the
// candidate pairing, the two-outcome verdict shell, the per-gate entry, and the report — exactly the minimal
// set FR-012 names. The supplied `GateId` / `FreshnessInputs` / `EvidenceRef` / `RecomputeCause` are OPAQUE
// facts produced elsewhere: this core never resolves, fabricates, re-hashes, parses, or dereferences them
// (FR-009).

namespace FS.GG.Governance.CacheEligibility

open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// One selected gate's stable identity (F018 `GateId`, verbatim) paired with the freshness inputs ALREADY
    /// resolved for it (F029 `FreshnessInputs`, verbatim). Both fields are SUPPLIED facts: this core does not
    /// resolve, fabricate, or re-hash the inputs, and does not derive, parse, or cross-check the `GateId`
    /// against `Inputs.Check` / `Inputs.Domain` (FR-009). The unit of input to the roll-up.
    type CandidateGate =
        { Gate: GateId
          Inputs: FreshnessInputs }

    /// The CLOSED two-outcome per-gate verdict (FR-001/FR-002/FR-010). Exactly one of two outcomes, so a
    /// threshold-unmet or opaque yes/no verdict is UNREPRESENTABLE (FR-001). `Reusable` carries the F030
    /// `EvidenceRef` verbatim — prior evidence MAY be reused for this gate — and is necessary-not-sufficient:
    /// it holds no skip action, severity, ship verdict, or exit-code basis (FR-010). `MustRecompute` always
    /// names its F030 `RecomputeCause` (the no-hide rule, FR-002): `NoPriorEvidence`, or `InputsChanged`
    /// naming exactly the changed freshness-input categories. The relabel of F030's `ReuseDecision` introduces
    /// NO new reuse policy (FR-004). The payloads are reused verbatim from F030; this shell is the only new
    /// union.
    type CacheEligibilityVerdict =
        | Reusable of EvidenceRef
        | MustRecompute of RecomputeCause

    /// One candidate gate's verdict attributed to its originating `GateId`, so a later projection can place it
    /// under the correct gate (FR-005). The entry carries no `FreshnessInputs` (only the gate id and verdict).
    type CacheEligibilityEntry =
        { Gate: GateId
          Verdict: CacheEligibilityVerdict }

    /// The per-change roll-up: one entry per candidate gate, every gate preserved (none dropped, merged, or
    /// duplicated), in deterministic `GateId`-ordinal order independent of supply order (FR-006). Single-case
    /// wrapper (the F030 `ReuseStore` precedent); the `entries` accessor unwraps it. `evaluate [] store`
    /// yields the empty report — a total, valid result, never an error (Edge Cases).
    type CacheEligibilityReport = CacheEligibilityReport of CacheEligibilityEntry list
