// Per-gate cache-eligibility types for the cache-eligibility roll-up core (F041). The public surface is fixed
// by Model.fsi (Principle II); no top-level binding here carries an access modifier. These are the supplied
// per-gate inputs and the named outcomes that `CacheEligibility.evaluate` / `evaluateGate` work over; they
// reuse the F018 `GateId`, the F029 `FreshnessInputs` / `InputCategory`, and the F030 `ReuseStore` /
// `ReuseDecision` / `RecomputeCause` / `EvidenceRef` verbatim rather than redefining them (FR-012).

namespace FS.GG.Governance.CacheEligibility

open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type CandidateGate =
        { Gate: GateId
          Inputs: FreshnessInputs }

    type CacheEligibilityVerdict =
        | Reusable of EvidenceRef
        | MustRecompute of RecomputeCause

    type CacheEligibilityEntry =
        { Gate: GateId
          Verdict: CacheEligibilityVerdict }

    type CacheEligibilityReport = CacheEligibilityReport of CacheEligibilityEntry list
