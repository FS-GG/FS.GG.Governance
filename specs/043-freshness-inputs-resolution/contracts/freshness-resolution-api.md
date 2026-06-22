# Contract: `FreshnessResolution` public API

**Feature**: `043-freshness-inputs-resolution`

The committed public surface of `FS.GG.Governance.FreshnessResolution`. This is the Tier-1 contract guarded by
`surface/FS.GG.Governance.FreshnessResolution.surface.txt`. All operations are **pure, total, and
deterministic** (FR-008/FR-009): defined for every well-typed input, never throwing, reading no clock /
filesystem / git / environment / network, running no command, computing no hash / freshness key / digest, and
byte-identical for identical input regardless of evaluation time, machine, process, or working directory.

## `module Model`

```fsharp
namespace FS.GG.Governance.FreshnessResolution

open FS.GG.Governance.Gates.Model            // GateId
open FS.GG.Governance.FreshnessKey.Model      // RuleHash, ArtifactHash, CommandVersion, GeneratorVersion, Revision, FreshnessInputs
open FS.GG.Governance.Config.Model            // CommandId

module Model =

    type SensedFacts =
        { RuleHash: RuleHash option
          GeneratorVersion: GeneratorVersion option
          Base: Revision option
          Head: Revision option
          CoveredArtifacts: Map<GateId, ArtifactHash list>
          CommandVersions: Map<CommandId, CommandVersion> }

    type MissingFact =
        | MissingRuleHash
        | MissingCoveredArtifacts
        | MissingCommandVersion
        | MissingGeneratorVersion
        | MissingBaseRevision
        | MissingHeadRevision

    type ResolutionOutcome =
        | Resolved of FreshnessInputs
        | Unresolved of MissingFact list

    type FreshnessResolutionEntry = { Gate: GateId; Outcome: ResolutionOutcome }

    type FreshnessResolutionReport = FreshnessResolutionReport of FreshnessResolutionEntry list
```

## `module FreshnessResolution`

```fsharp
namespace FS.GG.Governance.FreshnessResolution

open FS.GG.Governance.Gates.Model                       // Gate
open FS.GG.Governance.CacheEligibility.Model            // CandidateGate
open FS.GG.Governance.FreshnessResolution.Model

module FreshnessResolution =

    /// The join: one attributed outcome per supplied gate, ordered by the total order — `gateIdValue` ordinal
    /// (`String.CompareOrdinal`), then, for entries sharing a GateId, structural `compare` of the whole
    /// `FreshnessResolutionEntry` (duplicates preserved as adjacent entries). For each gate, sources
    /// Check/Domain/Environment/Command from the
    /// gate's carried FreshnessKey (dropping Cost) and the six remaining fields from `sensed`; a gate missing
    /// any required sensed fact is `Unresolved` naming every gap (no-hide), else `Resolved` with the complete
    /// FreshnessInputs. PURE and TOTAL. `resolve [] sensed = FreshnessResolutionReport []`.
    val resolve: gates: Gate list -> sensed: SensedFacts -> FreshnessResolutionReport

    /// Unwrap a report to its attributed entries. `entries (FreshnessResolutionReport xs) = xs`.
    val entries: report: FreshnessResolutionReport -> FreshnessResolutionEntry list

    /// The F041 bridge — recompute-safe by construction. `Some { Gate = e.Gate; Inputs = inputs }` for a
    /// `Resolved inputs` entry; `None` for `Unresolved _`. The only function producing a `CandidateGate`, so an
    /// unresolved gate can never become a cache-eligibility candidate (FR-004/FR-010).
    val candidate: entry: FreshnessResolutionEntry -> CandidateGate option

    /// `true` for `Resolved _`; `false` for `Unresolved _`.
    val isResolved: outcome: ResolutionOutcome -> bool

    /// The named missing facts of an outcome, in enum order. `[]` for `Resolved _`; the non-empty no-hide list
    /// for `Unresolved facts`.
    val missingFacts: outcome: ResolutionOutcome -> MissingFact list

    /// Stable, injective wire token for a missing fact (for messages, tests, and the later projection), e.g.
    /// `MissingRuleHash -> "ruleHash"`, `MissingCoveredArtifacts -> "coveredArtifacts"`,
    /// `MissingCommandVersion -> "commandVersion"`, `MissingGeneratorVersion -> "generatorVersion"`,
    /// `MissingBaseRevision -> "baseRevision"`, `MissingHeadRevision -> "headRevision"`.
    val missingFactToken: fact: MissingFact -> string
```

## Laws (asserted by the semantic tests; full list in [data-model.md](../data-model.md))

| Law | Statement |
|---|---|
| L-carry | `Resolved` fields = carried identity (cost dropped) + sensed facts, verbatim (FR-001/FR-002) |
| L-no-fabricate | a gate missing facts is `Unresolved`; no `FreshnessInputs`, no placeholder produced (FR-003) |
| L-no-hide | `Unresolved` names *every* missing fact, never truncated, in enum order (FR-003) |
| L-recompute-safe | `candidate (Unresolved …)` = `None`; no path from `Unresolved` to `FreshnessInputs` (FR-004) |
| L-command-absent | `Command = None` ⇒ `CommandVersion = None`, never `MissingCommandVersion` (FR-005) |
| L-attribute | every entry carries its originating `GateId` (FR-006) |
| L-complete | exactly one entry per input gate; none dropped/merged; duplicates kept (FR-007) |
| L-order | sorted by the total order — `gateIdValue` ordinal, then structural `compare` of the whole entry; order-independent (FR-007) |
| L-total | well-formed report for every input; never throws (FR-008) |
| L-pure | identical inputs ⇒ byte-identical report; no I/O (FR-009) |
| L-candidate | `candidate (Resolved …)` accepted by F041 `evaluate`/`evaluateGate` unchanged (FR-010) |
| L-necessary | a `Resolved` outcome carries no reuse/skip/severity/ship/exit-code meaning (FR-011) |

## Scope guard (asserted by the surface-drift test)

The assembly references **only** `FS.GG.Governance.CacheEligibility` (F041) and its transitive pure cores
(`EvidenceReuse`, `Gates`, `FreshnessKey`, `Config`, `Kernel`). No new third-party `PackageReference`; no
`RouteJson`/`AuditJson`/`Enforcement`/`Ship`/`Snapshot`/`Routing`/`host`/CLI coupling; no JSON, git, clock, or
filesystem surface. The published surface is exactly the two modules above.
