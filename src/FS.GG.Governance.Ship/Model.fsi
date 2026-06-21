// CONTRACT DRAFT (Phase 1) for specs/024-ship-verdict-rollup.
// The shipped Model.fsi (in src/FS.GG.Governance.Ship/) is authored from this draft and is the SOLE
// declaration of the module's public surface (Constitution Principle II). The matching Model.fs
// carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// These are the result-vocabulary values the `Ship.rollup` whole-change rollup returns: the closed
// ship verdict, the typed exit-code basis, the per-item identity + enforcement detail, and the
// three-way item partition. They REUSE the F023 `EnforcementDecision`, the F018 `GateId`, the F017
// `FindingId`, and the F014 `GovernedPath` rather than redefining them. No field carries raw YAML,
// host paths, timestamps, a serialized audit document, a process exit code, a cache/freshness verdict,
// or any policy.yml-derived dial (FR-012, SC-007).

namespace FS.GG.Governance.Ship

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The whole-change outcome (FR-002). `Fail` iff at least one enforced item is effective-`Blocking`
    /// (i.e. `Blockers` is non-empty); `Pass` otherwise. Closed.
    type Verdict =
        | Pass
        | Fail

    /// The typed exit-code BASIS, not a number (FR-007). `Clean` when the verdict is `Pass`, `Blocked`
    /// when `Fail`. The later `fsgg ship` host edge maps this to a numeric process exit; this pure core
    /// sets no exit code and never exits. Closed.
    type ExitCodeBasis =
        | Clean
        | Blocked

    /// The identity of one enforced item. A gate is identified by its `GateId`; a finding by its
    /// `FindingId` paired with its normalized `Path` (the same id may recur on several paths). Closed.
    type EnforcedItemId =
        | GateItem of GateId
        | FindingItem of FindingId * GovernedPath

    /// One selected gate or one finding after enforcement: its identity plus the F023
    /// `EnforcementDecision` returned VERBATIM — carrying all six no-hide fields (base severity echoed
    /// unchanged, maturity, run mode, profile, effective severity, reason — FR-005, FR-006).
    type EnforcedItem =
        { Id: EnforcedItemId
          Decision: EnforcementDecision }

    /// The whole-change ship decision. `Blockers`/`Warnings`/`Passing` are the mutually-exclusive,
    /// jointly-exhaustive partition of every enforced item (FR-004, FR-010, SC-006):
    ///   • `Blockers` — effective severity is `Blocking`.
    ///   • `Warnings` — base `Blocking` relaxed to effective `Advisory` by mode/maturity/profile.
    ///   • `Passing`  — base `Advisory` (never escalated — FR-011).
    /// Each list is sorted by a stable composite per-item key (gates before findings, then by id/path —
    /// FR-009). `Verdict`/`ExitCodeBasis` are total functions of the partition. No serialized document,
    /// exit code, or freshness/cache verdict (FR-012, SC-007).
    type ShipDecision =
        { Verdict: Verdict
          Blockers: EnforcedItem list
          Warnings: EnforcedItem list
          Passing: EnforcedItem list
          ExitCodeBasis: ExitCodeBasis }
