// Per-gate cache-eligibility operations for the cache-eligibility roll-up core (F041). The public surface is
// fixed by CacheEligibility.fsi (Principle II); no top-level binding here carries an access modifier (the
// ordinal sort-comparator helper is private by its absence from the .fsi). The operations are pure, total, and
// deterministic (FR-007, FR-008): no clock, filesystem, git, environment, or network; no gate run; no evidence
// produced; no bytes hashed; no freshness key computed; none of the supplied inputs resolved; no cache lookup
// against a real store; identical inputs always yield the identical report. The roll-up is fixed by
// contracts/cache-eligibility-api.md and data-model.md.

namespace FS.GG.Governance.CacheEligibility

open System
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CacheEligibility =

    let evaluateGate (candidate: CandidateGate) (store: ReuseStore) : CacheEligibilityVerdict =
        // Compose F030 `decide` VERBATIM and relabel 1-to-1: no new matching, ranking, or reuse policy
        // (L-G1..L-G5, FR-004). Recompute is the default — `Reuse` is returned by F030 only on a defensible
        // match, otherwise a located cause flows through unchanged (the no-hide rule).
        match EvidenceReuse.decide candidate.Inputs store with
        | Reuse ref -> Reusable ref
        | Recompute cause -> MustRecompute cause

    // The total, input-order-independent sort comparator: ordinal on the `GateId` wire string first, then a
    // structural comparison of the whole entry as the duplicate-`GateId` tiebreak. Private by its absence from
    // the .fsi. The entries are mapped BEFORE the sort, so the tiebreak resolves on the entry's `Verdict` (the
    // entry carries no `FreshnessInputs`); two same-`GateId` entries collide only when their `Verdict`s are
    // equal, and then the entries are byte-identical so their relative order is immaterial (L-E2). Computes no
    // freshness key or hash — only string/structural comparison (L-E6).
    let private compareEntries (a: CacheEligibilityEntry) (b: CacheEligibilityEntry) : int =
        match String.CompareOrdinal(gateIdValue a.Gate, gateIdValue b.Gate) with
        | 0 -> compare a b
        | n -> n

    let evaluate (candidates: CandidateGate list) (store: ReuseStore) : CacheEligibilityReport =
        // One attributed verdict per candidate (L-E1), then a stable ordinal sort (L-E2/L-E3); every gate
        // preserved, duplicates kept (L-E4); empty input ⇒ empty report (L-E5).
        candidates
        |> List.map (fun c -> { Gate = c.Gate; Verdict = evaluateGate c store })
        |> List.sortWith compareEntries
        |> CacheEligibilityReport

    let entries (report: CacheEligibilityReport) : CacheEligibilityEntry list =
        let (CacheEligibilityReport xs) = report // L-P1
        xs

    let isReusable (verdict: CacheEligibilityVerdict) : bool =
        match verdict with
        | Reusable _ -> true // L-P2
        | MustRecompute _ -> false

    let reusableEvidence (verdict: CacheEligibilityVerdict) : EvidenceRef option =
        match verdict with
        | Reusable ref -> Some ref // L-P3
        | MustRecompute _ -> None

    let recomputeCause (verdict: CacheEligibilityVerdict) : RecomputeCause option =
        match verdict with
        | MustRecompute cause -> Some cause // L-P4
        | Reusable _ -> None
