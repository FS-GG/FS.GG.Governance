// Curated public signature contract for the per-gate freshness-inputs resolution operations (F043).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// FreshnessResolution.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here. The internal per-gate join helper, the ordinal sort comparator, and the token map
// are ABSENT from this surface (private by omission).
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any FreshnessResolution.fs
// body exists (Principle I). All operations are PURE, TOTAL, and DETERMINISTIC (FR-008/FR-009): defined for
// every well-typed input, never throwing; reading no clock/filesystem/git/environment/network, running no
// command, computing no hash/freshness key/digest, evaluating no cache eligibility, resolving none of the
// supplied newtypes (consumed opaquely — never parsed, re-hashed, or fabricated), rendering no JSON, persisting
// nothing, mapping no exit code; identical for identical input regardless of evaluation time, machine, process,
// or working directory. Its sole output is the typed `FreshnessResolutionReport` / `ResolutionOutcome`.

namespace FS.GG.Governance.FreshnessResolution

open FS.GG.Governance.Gates.Model // Gate
open FS.GG.Governance.CacheEligibility.Model // CandidateGate
open FS.GG.Governance.FreshnessResolution.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FreshnessResolution =

    /// The join: one attributed outcome per supplied gate, ordered by the total order — `gateIdValue` ordinal
    /// (`String.CompareOrdinal`), then, for entries sharing a `GateId`, structural `compare` of the whole
    /// `FreshnessResolutionEntry` (duplicates preserved as adjacent entries). For each gate, sources
    /// `Check`/`Domain`/`Environment`/`Command` from the gate's carried `FreshnessKey` (dropping `Cost`) and the
    /// six remaining fields from `sensed`; a gate missing any required sensed fact is `Unresolved` naming every
    /// gap (no-hide, in enum order), else `Resolved` with the complete `FreshnessInputs`. PURE and TOTAL —
    /// fabricates/defaults/zero-fills nothing, never throws. `resolve [] sensed = FreshnessResolutionReport []`.
    val resolve: gates: Gate list -> sensed: SensedFacts -> FreshnessResolutionReport

    /// Unwrap a report to its attributed entries. TOTAL. `entries (FreshnessResolutionReport xs) = xs`.
    val entries: report: FreshnessResolutionReport -> FreshnessResolutionEntry list

    /// The F041 bridge — recompute-safe by construction. `Some { Gate = entry.Gate; Inputs = inputs }` for a
    /// `Resolved inputs` entry; `None` for `Unresolved _`. The ONLY function producing a `CandidateGate`, so an
    /// unresolved gate can never become a cache-eligibility candidate (FR-004/FR-010). TOTAL.
    val candidate: entry: FreshnessResolutionEntry -> CandidateGate option

    /// `true` for `Resolved _`; `false` for `Unresolved _`. TOTAL.
    val isResolved: outcome: ResolutionOutcome -> bool

    /// The named missing facts of an outcome, in enum order. `[]` for `Resolved _`; the non-empty no-hide list
    /// for `Unresolved facts`. TOTAL.
    val missingFacts: outcome: ResolutionOutcome -> MissingFact list

    /// Stable, injective wire token for a missing fact (for messages, tests, and the later projection):
    /// `MissingRuleHash -> "ruleHash"`, `MissingCoveredArtifacts -> "coveredArtifacts"`,
    /// `MissingCommandVersion -> "commandVersion"`, `MissingGeneratorVersion -> "generatorVersion"`,
    /// `MissingBaseRevision -> "baseRevision"`, `MissingHeadRevision -> "headRevision"`. Deterministic, total,
    /// and INJECTIVE — mirroring F029 `categoryToken` and the F042 token precedent. TOTAL.
    val missingFactToken: fact: MissingFact -> string
