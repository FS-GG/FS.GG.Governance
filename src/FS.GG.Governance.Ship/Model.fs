// The result-vocabulary values returned by `Ship.rollup` — the closed ship verdict, the typed
// exit-code basis, the per-item identity + enforcement detail, and the three-way item partition.
// These REUSE the F023 `EnforcementDecision`, the F018 `GateId`, the F017 `FindingId`, and the F014
// `GovernedPath` verbatim rather than redefining them.
//
// Visibility lives in Model.fsi (Constitution Principle II); this file carries NO top-level
// `private`/`internal`/`public` modifiers. These are pure data — closed DUs and two records, no
// bodies.

namespace FS.GG.Governance.Ship

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type Verdict =
        | Pass
        | Fail

    type ExitCodeBasis =
        | Clean
        | Blocked

    type EnforcedItemId =
        | GateItem of GateId
        | FindingItem of FindingId * GovernedPath

    type EnforcedItem =
        { Id: EnforcedItemId
          Decision: EnforcementDecision }

    type ShipDecision =
        { Verdict: Verdict
          Blockers: EnforcedItem list
          Warnings: EnforcedItem list
          Passing: EnforcedItem list
          ExitCodeBasis: ExitCodeBasis }
