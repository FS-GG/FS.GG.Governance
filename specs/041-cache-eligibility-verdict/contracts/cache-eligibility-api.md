# Contract — `FS.GG.Governance.CacheEligibility` public API (F041)

The Tier-1 public surface this row commits, with the laws each member upholds. The two `.fsi` files are the sole
declaration of visibility (Principle II); the reflective `SurfaceDrift` test pins this surface to
`surface/FS.GG.Governance.CacheEligibility.surface.txt` and guards the dependency scope. All operations are **pure,
total, and deterministic** (FR-007/FR-008): defined for every well-typed input, never throwing; reading no
clock/filesystem/git/environment/network, invoking no gate, computing no hash or freshness key, resolving none of
the supplied inputs, rendering no JSON, persisting nothing; identical for identical input regardless of evaluation
time, machine, process, or working directory.

## `Model` (types — see [data-model.md](../data-model.md))

`CandidateGate`, `CacheEligibilityVerdict`, `CacheEligibilityEntry`, `CacheEligibilityReport`. Reuses `GateId` from
`FS.GG.Governance.Gates.Model`; `FreshnessInputs` / `InputCategory` from `FS.GG.Governance.FreshnessKey.Model`; and
`ReuseStore` / `ReuseDecision` / `RecomputeCause` / `EvidenceRef` from `FS.GG.Governance.EvidenceReuse.Model`,
verbatim.

## `CacheEligibility` operations

```fsharp
val evaluate: candidates: CandidateGate list -> store: ReuseStore -> CacheEligibilityReport
val evaluateGate: candidate: CandidateGate -> store: ReuseStore -> CacheEligibilityVerdict
val entries: report: CacheEligibilityReport -> CacheEligibilityEntry list
val isReusable: verdict: CacheEligibilityVerdict -> bool
val reusableEvidence: verdict: CacheEligibilityVerdict -> EvidenceRef option
val recomputeCause: verdict: CacheEligibilityVerdict -> RecomputeCause option
```

### `evaluateGate` — the per-gate verdict (FR-002/FR-003/FR-004)

Composes F030 verbatim and relabels. Let `d = EvidenceReuse.decide candidate.Inputs store`:

- **L-G1 (compose, no new policy, FR-004)** — `evaluateGate candidate store` is `Reusable ref` when `d = Reuse ref`,
  and `MustRecompute cause` when `d = Recompute cause`. The mapping is total, 1-to-1, and information-preserving; it
  re-implements no matching, re-ranks no entries, and introduces no new or divergent reuse policy.
- **L-G2 (recompute by default, FR-003/SC-001)** — for an empty store, or any store with no entry F030 deems a
  defensible match, `evaluateGate candidate store = MustRecompute _`. There is no input by which an unmatched
  candidate yields `Reusable`.
- **L-G3 (no prior evidence, FR-002)** — when no recorded entry shares the candidate's gate (F030 `NoPriorEvidence`),
  the verdict is `MustRecompute NoPriorEvidence`.
- **L-G4 (changed inputs named, no-hide, FR-002/SC-003)** — when prior evidence for the gate exists but the world
  moved, the verdict is `MustRecompute (InputsChanged cats)` where `cats` is exactly F030's `diff` (the changed
  freshness-input categories — no missing category, no spurious category, never truncated to the first difference,
  never the identity categories).
- **L-G5 (reusable names the evidence, FR-002/SC-002)** — when F030 returns `Reuse ref`, the verdict is `Reusable
  ref` carrying that exact evidence reference; with multiple recorded entries it carries the same most-recent-wins
  reference F030 chooses (no new recency policy here).
- **L-G6 (necessary-not-sufficient, FR-010/SC-007)** — `CacheEligibilityVerdict` carries no skip action, severity,
  ship verdict, or exit-code basis; `Reusable` asserts only "prior evidence may be reused for this gate".

### `evaluate` — the per-change roll-up (FR-005/FR-006)

Let `report = evaluate candidates store`.

- **L-E1 (one attributed verdict per candidate, FR-005/FR-006/SC-006)** — `entries report` has exactly `List.length
  candidates` items; each `{ Gate; Verdict }` has `Gate = c.Gate` and `Verdict = evaluateGate c store` for its
  originating candidate `c`. No candidate is dropped, merged into another, or silently duplicated.
- **L-E2 (deterministic `GateId`-ordinal order, FR-006/SC-006)** — `entries report` is ordered by
  `String.CompareOrdinal` on `gateIdValue Gate`, ties broken by a total structural comparison of the
  `CacheEligibilityEntry` itself (`Gate`, then `Verdict`) — the candidates are mapped to entries *before* the sort,
  so the tiebreak resolves on the entry's `Verdict`, not on the candidate's `FreshnessInputs` (which the entry does
  not carry). This is total and order-independent: two same-`GateId` entries can only collide in the sort when their
  `Verdict`s are equal, and equal `Verdict`s under a tied `Gate` make the entries byte-identical, so their relative
  order is immaterial.
- **L-E3 (order-independence, SC-006)** — for any permutation `p`, `evaluate (p candidates) store = evaluate
  candidates store` (byte-identical report).
- **L-E4 (duplicates kept, Edge Cases)** — two candidates with the same `GateId` yield **two** entries (never merged
  or dropped), each carrying its own verdict, in a deterministic, input-order-independent order (the L-E2 tiebreak).
- **L-E5 (empty is total, Edge Cases)** — `evaluate [] store = CacheEligibilityReport []`; `entries` of it is `[]` —
  a valid result, not an error.
- **L-E6 (no key computed, FR-008)** — ordering uses only `gateIdValue` ordinal comparison plus structural
  comparison; `evaluate` computes no freshness key, hash, or fingerprint and resolves none of the supplied inputs.

### Cross-cutting laws

- **L-T1 (totality, SC-004)** — `evaluate` / `evaluateGate` return a well-formed report / verdict and never throw
  across the full cross-product of candidate counts (zero, one, many, duplicate `GateId`s) and store states (empty,
  matching, non-matching).
- **L-T2 (determinism / purity, SC-005)** — `evaluate c s = evaluate c s` and `evaluateGate g s = evaluateGate g s`
  always, including under a changed working directory, clock, and filesystem state; no I/O is performed.

### `entries` / `isReusable` / `reusableEvidence` / `recomputeCause` — projections

- **L-P1** — `entries (CacheEligibilityReport xs) = xs`.
- **L-P2** — `isReusable (Reusable _) = true`; `isReusable (MustRecompute _) = false`.
- **L-P3** — `reusableEvidence (Reusable ref) = Some ref`; `reusableEvidence (MustRecompute _) = None`.
- **L-P4** — `recomputeCause (MustRecompute cause) = Some cause`; `recomputeCause (Reusable _) = None`.

## Worked examples (pinned by tests)

Let `inputs0` be any `FreshnessInputs` value with gate `("build", "tests")`, and let
`gid d c = GateId (d + ":" + c)`. Let `refA = EvidenceRef "ev-A"`.

| Store | Candidate `Inputs` | `evaluateGate candidate store` |
|---|---|---|
| `EvidenceReuse.empty` | `inputs0` | `MustRecompute NoPriorEvidence` |
| `record inputs0 refA empty` | `inputs0` (exact match) | `Reusable (EvidenceRef "ev-A")` |
| `record inputs0 refA empty` | `inputs0` but `RuleHash` differs | `MustRecompute (InputsChanged [RuleHashCat])` |
| `record inputs0 refA empty` | `inputs0` but `RuleHash` **and** `Head` differ | `MustRecompute (InputsChanged [RuleHashCat; HeadRevisionCat])` |

Roll-up ordering. Candidates supplied as `[ {Gate = gid "z" "a"; …}; {Gate = gid "a" "b"; …}; {Gate = gid "a" "a";
…} ]` against any store ⇒ `entries` ordered `a:a`, `a:b`, `z:a` (ordinal on the `"<domain>:<checkId>"` string),
independent of the supplied order. Supplying the same three in any other permutation yields a byte-identical report.

Duplicate-`GateId` example. Two candidates both `Gate = gid "build" "tests"` with **different** `Inputs` ⇒ **two**
entries under `build:tests`, ordered by the structural tiebreak on the entry's `Verdict` — deterministic and the same
for any supply order (L-E4). When the two candidates' differing `Inputs` yield **equal** `Verdict`s (e.g. both
`MustRecompute NoPriorEvidence` against an empty store, or both `MustRecompute (InputsChanged [RuleHashCat])`), the two
entries are byte-identical and their order is immaterial — the report is byte-identical for any supply order.

## Scope guard (SurfaceDrift test, Principle II / SC-008)

The `FS.GG.Governance.CacheEligibility` assembly references **only** `FS.GG.Governance.EvidenceReuse` (F030) and
`FS.GG.Governance.Gates` (F018), their transitive pure cores (`FreshnessKey`, `Config`), and `FSharp.Core` / BCL. It
references no host/CLI/adapter assembly, no `RouteJson` / `AuditJson` / `Enforcement` / `Ship` / `Snapshot` /
`Routing` / `Findings`, and adds no third-party package. Any drift in the rendered public surface or the
referenced-assembly set fails the test (with the `BLESS_SURFACE=1` intentional-rebless path).
