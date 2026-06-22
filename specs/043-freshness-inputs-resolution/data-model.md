# Phase 1 Data Model: Per-Gate Freshness-Inputs Resolution Core

**Feature**: `043-freshness-inputs-resolution` | **Date**: 2026-06-22

All types live in namespace `FS.GG.Governance.FreshnessResolution`, split across `Model` (the vocabulary) and
`FreshnessResolution` (the `resolve` join + accessors). Reused upstream types are **never redefined** — they
arrive through the single F041 `CacheEligibility` project reference (research D2). New types are marked **NEW**.

## Reused vocabulary (verbatim, not redefined — FR-012)

| Type | Origin | Role in this core |
|---|---|---|
| `Gate`, `GateId`, `FreshnessKey` (`{Check;Domain;Cost;Environment;Command}`) | F018 `Gates.Model` | the input gate, its identity, and its carried five-field freshness-key identity |
| `gateIdValue : GateId -> string` | F018 `Gates` | ordinal sort key for the report (research D7) |
| `FreshnessInputs` (ten fields) + `RuleHash`, `ArtifactHash`, `CommandVersion`, `GeneratorVersion`, `Revision` | F029 `FreshnessKey.Model` | the assembled resolved value and the sensed-fact newtypes |
| `CheckId`, `DomainId`, `CommandId`, `EnvironmentClass` | F014 `Config.Model` | the carried-identity field types |
| `CandidateGate` (`{Gate:GateId; Inputs:FreshnessInputs}`) | F041 `CacheEligibility.Model` | the F041 input a `Resolved` outcome becomes, **without adaptation** |

## NEW vocabulary (the minimal set FR-012 permits)

### `SensedFacts` — the already-sensed repository facts, supplied (research D4)

```fsharp
type SensedFacts =
    { RuleHash: RuleHash option
      GeneratorVersion: GeneratorVersion option
      Base: Revision option
      Head: Revision option
      CoveredArtifacts: Map<GateId, ArtifactHash list>
      CommandVersions: Map<CommandId, CommandVersion> }
```

- **Repo-wide facts** (`RuleHash`, `GeneratorVersion`, `Base`, `Head`) are `option`: `None` ⇒ *not sensed* ⇒
  every gate needing it is unresolved on that fact (Edge Cases).
- **Per-key facts** are `Map`s: a **present** key is *sensed* (its value, including an empty `ArtifactHash`
  list, is a legitimate resolved value); an **absent** key is *not sensed* (unresolved). This is the
  "sensed-empty vs unsensed" distinction (FR-003, Edge Cases) — `CoveredArtifacts = map [g, []]` resolves;
  `CoveredArtifacts` with no `g` key is unresolved on covered artifacts.
- The core **senses none of this**; it is supplied verbatim (Assumption: sensing is upstream).

### `MissingFact` — the closed no-hide vocabulary (research D5/D6)

```fsharp
type MissingFact =
    | MissingRuleHash
    | MissingCoveredArtifacts
    | MissingCommandVersion
    | MissingGeneratorVersion
    | MissingBaseRevision
    | MissingHeadRevision
```

Closed, in **FR-002 field order**. `MissingCommandVersion` is *only* possible for a gate that declares a
command (FR-005). Each case has a stable, injective wire token via `missingFactToken` (research D8).

### `ResolutionOutcome` — the closed two-outcome per-gate result (research D5)

```fsharp
type ResolutionOutcome =
    | Resolved of FreshnessInputs
    | Unresolved of MissingFact list      // non-empty; names every missing fact (no-hide)
```

- `Resolved` carries the complete F029 `FreshnessInputs`. It is **recompute-safe by construction**: the
  alternative `Unresolved` carries *no* `FreshnessInputs`, so no consumer can convert an unresolved gate into a
  resolved input set (FR-004).
- `Unresolved` carries the **non-empty** `MissingFact` list, ordered by the `MissingFact` enum order, naming
  *exactly* the gaps and no others (FR-003).

### `FreshnessResolutionEntry` — one gate-attributed outcome (FR-006)

```fsharp
type FreshnessResolutionEntry = { Gate: GateId; Outcome: ResolutionOutcome }
```

Every outcome — resolved or unresolved — is attributed to its originating `GateId` so the host can run F041 and
a later projection can place each result under the correct gate.

### `FreshnessResolutionReport` — the per-change roll-up (FR-007)

```fsharp
type FreshnessResolutionReport = FreshnessResolutionReport of FreshnessResolutionEntry list
```

Single-case wrapper (the F030 `ReuseStore` / F041 `CacheEligibilityReport` precedent), unwrapped by `entries`.
One entry per input gate, in deterministic `GateId`-ordinal order with a structural tiebreak (research D7);
every gate preserved, none dropped/merged/duplicated; the empty report is a valid success.

**Ordering rule (the total order).** Entries are sorted by `List.sortWith` on a comparison that is
(1) the **ordinal** comparison of `gateIdValue entry.Gate` (`System.String.CompareOrdinal`, culture-invariant —
not the current-culture `compare` on `string`), and, when two entries share a `GateId`, (2) the F# **structural**
comparison (`compare`) of the **entire `FreshnessResolutionEntry`** (`{ Gate; Outcome }`, hence its `Outcome`,
since the `Gate` is equal). This is a *total* order on entries: two entries compare equal only when the whole
record is structurally equal, in which case `List.sortWith` (stable) preserves them as adjacent duplicates. The
totality of the tiebreak is what makes the report byte-identical regardless of input order (L7) even when
distinct gates share a `GateId` — e.g. same `GateId`, one `Resolved` and one `Unresolved`, or differing only in
a dropped-`Cost`-induced `Command` difference.

## The join (`resolve`) — field-by-field

For each input `Gate g` with `fk = g.FreshnessKey`, against `SensedFacts s`:

| `FreshnessInputs` field | Sourced from | Missing-fact when unavailable |
|---|---|---|
| `Check` | `fk.Check` (carried identity) | — (always present) |
| `Domain` | `fk.Domain` (carried identity) | — |
| `Command` | `fk.Command` (carried identity, `option`) | — |
| `Environment` | `fk.Environment` (carried identity) | — |
| `RuleHash` | `s.RuleHash` | `MissingRuleHash` if `None` |
| `CoveredArtifacts` | `s.CoveredArtifacts.[g.Id]` | `MissingCoveredArtifacts` if key absent |
| `CommandVersion` | `fk.Command`: `Some c` → `s.CommandVersions.[c]`; `None` → `None` (FR-005) | `MissingCommandVersion` if `Some c` and `c` absent |
| `GeneratorVersion` | `s.GeneratorVersion` | `MissingGeneratorVersion` if `None` |
| `Base` | `s.Base` | `MissingBaseRevision` if `None` |
| `Head` | `s.Head` | `MissingHeadRevision` if `None` |

`fk.Cost` is **dropped** (FR-002 — not a freshness input). If the missing-fact list is empty → `Resolved
inputs`; otherwise → `Unresolved missingFacts`. Nothing is fabricated, defaulted, or zero-filled.

## Validation & invariants (the laws the tests assert)

- **L1 (carry, FR-001/FR-002)**: a `Resolved` gate's four identity fields equal `fk` verbatim and its six
  sensed fields equal `SensedFacts` verbatim; `Cost` never appears.
- **L2 (no fabrication, FR-003)**: a gate missing ≥1 required fact is `Unresolved` naming exactly those facts;
  no `FreshnessInputs` is produced; no placeholder hash/version/revision exists.
- **L3 (no-hide, FR-003)**: `Unresolved` lists *every* missing fact, never truncated; ordered by enum order.
- **L4 (recompute-safe, FR-004)**: `candidate` of an `Unresolved` entry is `None`; there is no total function
  from `Unresolved` to `FreshnessInputs`.
- **L5 (consistent command absence, FR-005)**: `fk.Command = None` ⇒ `Resolved` with `Command = None` and
  `CommandVersion = None`; never `MissingCommandVersion`.
- **L6 (attribution + completeness, FR-006/FR-007)**: exactly one entry per input gate, each carrying its
  `GateId`; no gate dropped/merged; duplicates preserved as separate entries.
- **L7 (order-independent determinism, FR-007/FR-009)**: two input orders yield value-equal reports, ordered by
  the total order above (`gateIdValue` ordinal, then structural `compare` of the whole
  `FreshnessResolutionEntry`); identical inputs yield byte-identical reports under changed cwd/clock/
  filesystem (no I/O).
- **L8 (totality, FR-008)**: a well-formed report for zero / one / many gates and all-present / partial /
  all-absent sensed facts; never throws.
- **L9 (F041 candidate, FR-010/SC-007)**: `candidate` of every `Resolved` entry is accepted by
  `CacheEligibility.evaluate`/`evaluateGate` unchanged; a `Resolved` outcome carries no reuse decision, skip,
  severity, ship verdict, or exit code.
