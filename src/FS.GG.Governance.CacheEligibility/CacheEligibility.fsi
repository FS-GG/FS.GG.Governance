// Curated public signature contract for the per-gate cache-eligibility operations (F041).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// CacheEligibility.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here. The internal ordinal sort-comparator helper is ABSENT from this surface (private by
// omission).
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any CacheEligibility.fs
// body exists (Principle I). All operations are PURE, TOTAL, and DETERMINISTIC (FR-007/FR-008): defined for
// every well-typed input, never throwing; reading no clock/filesystem/git/environment/network, invoking no
// gate, producing no evidence, computing no hash or freshness key, resolving none of the supplied inputs,
// rendering no JSON, making no cache lookup against a real store, persisting nothing, mapping no exit code;
// identical for identical input regardless of evaluation time, machine, process, or working directory. Its
// sole output is the typed `CacheEligibilityReport` / `CacheEligibilityVerdict`.

namespace FS.GG.Governance.CacheEligibility

open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CacheEligibility =

    /// The per-change roll-up: one attributed verdict per supplied candidate gate (FR-005/FR-006). PURE and
    /// TOTAL. Maps each candidate to `{ Gate = c.Gate; Verdict = evaluateGate c store }`, then sorts by
    /// `String.CompareOrdinal` on `gateIdValue Gate` with a total structural tiebreak on the entry itself
    /// (`Gate`, then `Verdict`) — so the order is independent of supply order (L-E2/L-E3) and any permutation
    /// of the candidate list yields a byte-identical report. Exactly `List.length candidates` entries: no
    /// candidate dropped, merged, or silently duplicated; two candidates with the same `GateId` yield TWO
    /// entries (L-E4). `evaluate [] store = CacheEligibilityReport []` — empty is total, not an error (L-E5).
    /// Computes NO freshness key, hash, or fingerprint and resolves none of the supplied inputs (L-E6).
    val evaluate: candidates: CandidateGate list -> store: ReuseStore -> CacheEligibilityReport

    /// The per-gate verdict: composes F030 `EvidenceReuse.decide candidate.Inputs store` VERBATIM and relabels
    /// 1-to-1 (FR-002/FR-003/FR-004). `Reuse ref` ⇒ `Reusable ref`; `Recompute cause` ⇒ `MustRecompute cause`
    /// (L-G1). The mapping is total and information-preserving: it re-implements no matching, re-ranks no
    /// entries, and introduces no new or divergent reuse policy. Recompute by default (L-G2): for an empty
    /// store, or any store with no entry F030 deems a defensible match, the verdict is `MustRecompute _` —
    /// there is no input by which an unmatched candidate yields `Reusable`. `NoPriorEvidence` when no recorded
    /// entry shares the gate (L-G3); `InputsChanged cats` naming exactly F030's `diff` when the world moved
    /// (L-G4, no-hide); `Reusable ref` carrying F030's most-recent-wins reference on an exact match (L-G5).
    /// PURE and TOTAL: reads no clock/filesystem/git/environment/network, runs no gate, produces no evidence,
    /// computes no freshness key/hash, resolves none of the supplied inputs, makes no cache lookup against a
    /// real store, persists nothing (FR-008).
    val evaluateGate: candidate: CandidateGate -> store: ReuseStore -> CacheEligibilityVerdict

    /// The attributed entries of a report (unwrap). TOTAL. `entries (CacheEligibilityReport xs) = xs` (L-P1).
    val entries: report: CacheEligibilityReport -> CacheEligibilityEntry list

    /// Whether a verdict permits reuse (boolean projection). TOTAL. `true` for `Reusable _`; `false` for
    /// `MustRecompute _` (L-P2). A necessary-not-sufficient signal — it carries no enforcement meaning.
    val isReusable: verdict: CacheEligibilityVerdict -> bool

    /// The reusable evidence reference named by a verdict (projection). TOTAL. `Some ref` for `Reusable ref`;
    /// `None` for `MustRecompute _` (L-P3).
    val reusableEvidence: verdict: CacheEligibilityVerdict -> EvidenceRef option

    /// The recompute cause named by a verdict (projection). TOTAL. `Some cause` for `MustRecompute cause`;
    /// `None` for `Reusable _` (L-P4) — so a `MustRecompute` always names its cause (no-hide).
    val recomputeCause: verdict: CacheEligibilityVerdict -> RecomputeCause option
