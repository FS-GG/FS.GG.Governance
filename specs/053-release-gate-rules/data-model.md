# Phase 1 Data Model: Pure Release-Gate Readiness Rules Core (F053)

The committed type contracts are [`contracts/Model.fsi`](./contracts/Model.fsi) (vocabulary) and
[`contracts/Release.fsi`](./contracts/Release.fsi) (the pure functions). This document narrates the entities,
the reused primitives, and the `evaluate → rollup` flow. It duplicates no `.fsi` body — see the contracts for
the authoritative field lists.

## Reused primitives (NOT redefined — FR-009)

| Primitive | From | Role here |
|-----------|------|-----------|
| `Maturity` (incl. `BlockOnRelease`) | F014 `Config.Model` | A rule's declared maturity lever; `BlockOnRelease` is the blocking-at-release maturity. |
| `SurfaceId` | F014 `Config.Model` | The governed identity a rule names (typically a declared `ReleaseSurface`). |
| `Severity` (`Advisory`/`Blocking`) | F023 `Enforcement` | A rule's declared base severity and the derived effective severity. |
| `RunMode.Release`, `Profile.Release` | F023 `Enforcement` | The fixed mode/profile the rollup derives under (research D2). |
| `EnforcementInput`, `EnforcementDecision`, `deriveEffectiveSeverity` | F023 `Enforcement` | The per-finding severity decision, called **verbatim** (FR-003). |
| `Verdict` (`Pass`/`Fail`), `ExitCodeBasis` (`Clean`/`Blocked`) | F024 `Ship.Model` | The whole-release verdict + exit-code basis, reused **verbatim** (FR-004). |

This row references **only** F014 `Config`, F023 `Enforcement`, and F024 `Ship` — not `Route`/`Gates`/
`Findings`, because a release rule is neither an F018 gate nor an F017 unknown-path finding (research D1/D5).

## New entities (this row)

```text
ReleaseRuleKind  =  VersionBump | PackageMetadata | TemplatePins
                  | PublishPlan | TrustedPublishing | Provenance        -- closed (FR-002)

FactState        =  Met | Unmet | Unrecoverable                         -- tri-state (research D3)

RuleOutcome      =  Satisfied | Violated                                -- (FR-001)

ReleaseRule      =  { Kind: ReleaseRuleKind                             -- declared input
                      Surface: SurfaceId                                --   (FR-002, research D6)
                      BaseSeverity: Severity                            --   F023 input shape, verbatim
                      Maturity: Maturity }                              --   BlockOnRelease blocks; Warn relaxes

ReleaseFacts     =  { States: Map<ReleaseRuleKind, FactState> }         -- provided input; absent ⇒ Unrecoverable

ReleaseFinding   =  { Kind; Surface; Outcome: RuleOutcome              -- one per declared rule (FR-001/FR-006)
                      BaseSeverity; Maturity; Reason: string }          --   declared levers carried (FR-003)

EnforcedReleaseFinding = { Finding: ReleaseFinding                      -- mirrors Ship.Model.EnforcedItem
                           Decision: EnforcementDecision }              --   F023 decision, verbatim

ReleaseDecision  =  { Verdict: Verdict                                  -- F024 types, verbatim (FR-004)
                      Blockers:  EnforcedReleaseFinding list            -- the 3-way partition (FR-004/FR-006)
                      Warnings:  EnforcedReleaseFinding list
                      Passing:   EnforcedReleaseFinding list
                      ExitCodeBasis: ExitCodeBasis }
```

### Field notes

- **`ReleaseRule.BaseSeverity` + `.Maturity`** are the two F023 levers, declared explicitly so they feed
  `EnforcementInput` with **no** re-derivation of any unexported mapping (research D6). A blocking release rule
  declares `Blocking` + `BlockOnRelease`; relaxing `Maturity` to `Warn`/`Observe` makes a violation advisory
  without changing the rule's satisfied/violated truth or its visibility (FR-010).
- **`ReleaseFacts.States`** is keyed by kind. A rule reads the fact for **its** kind; an absent key resolves
  to `Unrecoverable` (⇒ `Violated`, FR-005). Facts for an undeclared kind are never read (no fabricated
  finding). Duplicate rules of a kind each read the same fact and each emit their own finding.
- **`ReleaseFinding.Reason`** is deterministic and product-neutral — the kind token + governed `SurfaceId` +
  outcome basis ("met" / "not met" / "no recoverable evidence"), no host paths or timestamps (research D7).

## The evaluate → rollup flow

```text
 rules : ReleaseRule list ─┐
                           ├─►  evaluate  ─►  findings : ReleaseFinding list  ─►  rollup  ─►  ReleaseDecision
 facts : ReleaseFacts ─────┘     (US1)            (one per rule, sorted)          (US2)        (verdict + partition)
                                                                    └────────── evaluateRelease ───────────┘
```

### `evaluate rules facts` (US1, FR-001/FR-005/FR-006)

1. For each `rule`, look up `factFor facts rule.Kind`:
   - `Met` → `Outcome = Satisfied`, reason "… met".
   - `Unmet` → `Outcome = Violated`, reason "… not met".
   - `Unrecoverable` → `Outcome = Violated`, reason "… no recoverable evidence" (fail-safe, distinct text).
2. Build one `ReleaseFinding` carrying the rule's `Kind`, `Surface`, the `Outcome`, the declared
   `BaseSeverity` + `Maturity`, and the `Reason`.
3. Sort the findings by `(releaseRuleKindOrdinal Kind, surfaceIdString Surface)` — the stable composite key
   (research D4). Result: exactly `|rules|` findings, byte-identical across runs, multiset-preserving.

### `rollup findings` (US2, FR-003/FR-004)

For each `finding`:
1. `input = { BaseSeverity = finding.BaseSeverity; Maturity = finding.Maturity; Mode = Release; Profile = Release }`.
2. `decision = deriveEffectiveSeverity input` — F023, **verbatim** (FR-003).
3. Place `{ Finding = finding; Decision = decision }` by **re-applying the F024 partition rule** (research D1),
   gated by the finding's outcome:

   | Outcome | base `BaseSeverity` | effective `Decision.EffectiveSeverity` | bucket |
   |---------|---------------------|----------------------------------------|--------|
   | `Satisfied` | (any) | (any) | **Passing** |
   | `Violated`  | `Blocking` | `Blocking` | **Blockers** |
   | `Violated`  | `Blocking` | `Advisory` (relaxed by maturity) | **Warnings** |
   | `Violated`  | `Advisory` | `Advisory` | **Passing** (never escalated) |

4. `Verdict = if Blockers ≠ [] then Fail else Pass`; `ExitCodeBasis = if Verdict = Fail then Blocked else Clean`
   — the F024 rule and types, **verbatim** (FR-004).

Each partition list preserves the deterministic finding order from `evaluate`. Invariant:
`|Blockers| + |Warnings| + |Passing| = |findings|` (no drop, FR-006).

`evaluateRelease rules facts = rollup (evaluate rules facts)` — the single whole-gate entry.

## How the requirements map onto the model

| Requirement | Mechanism |
|-------------|-----------|
| FR-001 one finding per rule | `evaluate` is a `List.map` over `rules` + stable sort — `|findings| = |rules|`. |
| FR-002 closed rule-kind set | `ReleaseRuleKind` (6 cases); extends additively (new case + `factFor` key). |
| FR-003 carry declared severity; effective via F023 | finding carries `BaseSeverity`+`Maturity`; `rollup` calls `deriveEffectiveSeverity` verbatim under Release mode/profile. |
| FR-004 verdict + exit basis reuse F024 | `Verdict`/`ExitCodeBasis` reused verbatim; partition rule re-applied; `Fail` iff Blockers ≠ []. |
| FR-005 absent fact ⇒ violated | `factFor` maps an absent key to `Unrecoverable` ⇒ `Violated`. |
| FR-006 no-hide | every finding lands in exactly one partition list; satisfied findings in `Passing`; relaxed violations in `Warnings`. |
| FR-007 pure/total/deterministic | total `match`/`map`/sort; no I/O; stable sort ⇒ byte-identical. |
| FR-008 no sensing/process/document | facts are typed input; no file/process/JSON anywhere. |
| FR-009 no edit/duplicate frozen cores | F023/F024/F014 referenced and called verbatim; nothing edited; the partition *rule* is reused, `Ship.rollup` is not forked (D1). |
| FR-010 maturity-only relaxation | relaxation is changing `Maturity`/`BaseSeverity` → effective severity → bucket; outcome truth + visibility unchanged. |
