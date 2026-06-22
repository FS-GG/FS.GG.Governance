// Curated public signature contract for the freshness-inputs resolution vocabulary (F043).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body exists
// (Principle I). These are the new vocabulary the `FreshnessResolution.resolve` join works over: the supplied
// already-sensed repository facts (`SensedFacts`), the closed no-hide missing-fact union (`MissingFact`), the
// closed two-outcome per-gate result (`ResolutionOutcome`), the gate-attributed entry, and the report. They
// REUSE the F018 `GateId` (opened from `FS.GG.Governance.Gates.Model`), the F029 `FreshnessInputs` + its
// newtypes `RuleHash`/`ArtifactHash`/`CommandVersion`/`GeneratorVersion`/`Revision` (opened from
// `FS.GG.Governance.FreshnessKey.Model`), and the F014 `CommandId` (opened from `FS.GG.Governance.Config.Model`)
// VERBATIM, never redefined (FR-012) — they arrive through the single F041 `CacheEligibility` reference.

namespace FS.GG.Governance.FreshnessResolution

open FS.GG.Governance.Gates.Model // GateId
open FS.GG.Governance.FreshnessKey.Model // RuleHash, ArtifactHash, CommandVersion, GeneratorVersion, Revision, FreshnessInputs
open FS.GG.Governance.Config.Model // CommandId

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The already-sensed repository facts SUPPLIED to the join (research D4); the core senses NONE of this. The
    /// repo-wide facts (`RuleHash`, `GeneratorVersion`, `Base`, `Head`) are `option`: `None` ⇒ NOT SENSED ⇒
    /// every gate needing it is unresolved on that fact. The per-key facts are `Map`s where a PRESENT key is
    /// SENSED (its value — including an empty `ArtifactHash list` — is a legitimate resolved value) and an
    /// ABSENT key is NOT SENSED (unresolved). This is the "sensed-empty vs unsensed" distinction (FR-003):
    /// `CoveredArtifacts = map [g, []]` resolves to `CoveredArtifacts = []`; `CoveredArtifacts` with no `g` key
    /// is unresolved on covered artifacts. The supplied newtypes are consumed opaquely — never parsed, re-hashed,
    /// or fabricated.
    type SensedFacts =
        { RuleHash: RuleHash option
          GeneratorVersion: GeneratorVersion option
          Base: Revision option
          Head: Revision option
          CoveredArtifacts: Map<GateId, ArtifactHash list>
          CommandVersions: Map<CommandId, CommandVersion> }

    /// The CLOSED no-hide vocabulary (research D5/D6), in FR-002 field order. One case per required sensed fact
    /// that can be missing. `MissingCommandVersion` is ONLY possible for a gate that declares a command (FR-005).
    /// Each case has a stable, injective wire token via `FreshnessResolution.missingFactToken` (research D8).
    type MissingFact =
        | MissingRuleHash
        | MissingCoveredArtifacts
        | MissingCommandVersion
        | MissingGeneratorVersion
        | MissingBaseRevision
        | MissingHeadRevision

    /// The CLOSED two-outcome per-gate result (research D5). `Resolved` carries the complete F029
    /// `FreshnessInputs`, shaped to feed F041 verbatim. It is RECOMPUTE-SAFE BY CONSTRUCTION: the alternative
    /// `Unresolved` carries NO `FreshnessInputs`, so no consumer can convert an unresolved gate into a resolved
    /// input set (FR-004). `Unresolved` carries a NON-EMPTY `MissingFact list`, in `MissingFact` enum order,
    /// naming EXACTLY the gaps and no others (the no-hide rule, FR-003).
    type ResolutionOutcome =
        | Resolved of FreshnessInputs
        | Unresolved of MissingFact list

    /// One gate-attributed outcome (FR-006). Every outcome — resolved or unresolved — is attributed to its
    /// originating `GateId` so the host can run F041 and a later projection can place each result under the
    /// correct gate. The entry carries ONLY the gate id and outcome (necessary-not-sufficient, FR-011).
    type FreshnessResolutionEntry =
        { Gate: GateId
          Outcome: ResolutionOutcome }

    /// The per-change roll-up (FR-007): one entry per input gate, in deterministic `GateId`-ordinal order with a
    /// structural tiebreak (duplicates preserved as adjacent entries); every gate preserved, none dropped,
    /// merged, or deduplicated; the empty report is a valid success. Single-case wrapper (the F030 `ReuseStore` /
    /// F041 `CacheEligibilityReport` precedent), unwrapped by `FreshnessResolution.entries`.
    type FreshnessResolutionReport = FreshnessResolutionReport of FreshnessResolutionEntry list
