# Phase 0 Research: Cost, Cache, Command, and Provenance (F25)

This row adds the **cost dimension** of the evidence-reuse decision and its audit. The spec confirms the
freshness key (F029), reuse verdict (F030/F041), command runs (F032), provenance (F033), agent-review identity
(F036), and enforcement (F018/F023) are reused **unchanged**, and explicitly defers two decisions to planning:
the **budget representation** and the **skip-vs-defer semantics**. The decisions below resolve every
NEEDS-CLARIFICATION and record the rationale.

## D1 — Budget representation: ordered cost ceiling with a `Cheap` floor

**Decision.** A `CostBudget` is a single `Cost` **ceiling**. A must-recompute gate recomputes iff its cost
tier `<= ceiling` (the existing `Cheap < Medium < High < Exhaustive` ordering, `Config.Model.Cost`), otherwise
it is over budget. `Cheap` is the **floor**: a `Cheap` ceiling means "zero expensive budget" — cheap recompute
always proceeds, every `Medium`/`High`/`Exhaustive` recompute is over budget. `Exhaustive` admits the full
matrix. The budget for a (`Profile`, `RunMode`) pair is `min(profileCeiling p, modeCeiling m)` of two monotone
projections:

| `Profile` | `profileCeiling` | | `RunMode` | `modeCeiling` |
|-----------|------------------|-|-----------|---------------|
| Light     | Cheap            | | Sandbox   | Cheap         |
| Standard  | Medium           | | Inner     | Cheap         |
| Strict    | High             | | Focused   | Medium        |
| Release   | Exhaustive       | | Verify    | High          |
|           |                  | | Gate      | High          |
|           |                  | | Release   | Exhaustive    |

So a Light/Inner run has ceiling `Cheap` (never spends `Medium`+ on recompute), and a Release/Release run has
ceiling `Exhaustive` (the full matrix). Both the strictness lever (`Profile`) and the protectiveness lever
(`RunMode`) can restrict; the **more restrictive wins** (the `min`).

**Rationale.**
- Reuses the existing **ordered** four-value `Cost` DU directly — no new tier, no parallel enumeration (FR-001,
  FR-006).
- **No arbitrary numeric tier weights.** The rejected alternative — per-tier recompute *counts* — would require
  inventing a numeric weight or slot count per tier (how many `Exhaustive` recomputes does Strict permit?),
  which is an unmotivated calibration liability, and worse, when more candidates exist than slots it forces a
  *which-gate-gets-the-slot* tiebreak that is sensitive to gate ordering — directly at odds with the mandatory
  order-independence (FR-002, SC-008). A tier ceiling is a **per-gate** predicate (`cost <= ceiling`) and is
  therefore order-independent by construction.
- Satisfies every edge case in the spec:
  - *Budget exactly met* — a gate whose cost equals the ceiling runs (`<=` is inclusive); a gate one tier over
    the ceiling is the "next gate over the boundary" and defers.
  - *Budget zero / disabled* — the `Cheap` ceiling: every must-recompute *expensive* gate is over budget while
    cheap recompute and all reuse still proceed ("a zero budget bounds expensive recompute, not reuse").
  - *All gates reusable* — the budget is consulted only for `MustRecompute` gates, so all-reusable touches the
    budget zero times.
  - *Cost does not affect reuse* — the budget is read **after** the F041 verdict; a cost-tier change with every
    freshness dimension unchanged still yields `Reusable` (cost stays out of the freshness key), and only the
    budget accounting reflects the new tier.

**Alternatives considered.** (a) Per-tier recompute counts — rejected (arbitrary weights + order-sensitivity,
above). (b) A single global numeric budget that depletes as gates spend tier-weighted cost — rejected (same
weight-invention problem, and depletion makes the decision order-dependent). (c) A boolean "expensive allowed"
flag — rejected (cannot distinguish `Medium` from `Exhaustive`, failing SC-001's tier granularity).

## D2 — Skip vs defer: by run-mode class

**Decision.** When a must-recompute gate is over budget, the outcome distinguishes **`Defer`** from **`Skip`**
by the run mode's class:
- **Boundary modes** (`Verify`, `Gate`, `Release`) → **`Defer`**: the gate is postponed and *must eventually
  run at a stricter boundary*; the reason names the gate, its cost tier, the exceeded ceiling, and that a
  higher-budget run would admit it.
- **Inner-loop modes** (`Sandbox`, `Inner`, `Focused`) → **`Skip`**: the inner loop deliberately will not run
  the gate this row; the reason names the gate, its cost tier, and the exceeded ceiling.

Both carry a non-empty named reason; **neither is ever reported as a pass** and neither is silently dropped
(FR-003).

**Rationale.** The spec's edge case asks the result to distinguish "skipped (will not run this row)" from
"deferred (could run later / in a higher profile)". Run-mode class is the deterministic, already-present signal
that captures exactly that intent: an inner-loop run is iterative and expects to skip expensive work outright;
a pre-merge/release boundary run cannot simply drop an expensive gate — it owes it to a stricter boundary, so
"deferred to a higher profile/mode" is the honest verdict. The mapping is total over the six modes, introduces
no new vocabulary, and keeps both cases live (a pure tier ceiling alone would leave `Skip` or `Defer` as dead
code — see below).

**Alternatives considered.** (a) Skip iff even the maximum budget (`Exhaustive`) would reject the gate —
rejected: with a tier ceiling `Exhaustive` admits everything, so `Skip` would never fire (dead code).
(b) Skip iff the gate has no declared command — rejected: that is already the F052 `NotExecuted` disposition,
a different axis. (c) A single undifferentiated "over-budget" outcome — rejected: the spec explicitly requires
the two be distinguishable.

## D3 — Surfacing: two pure cores + two sidecar projections, no new host command

**Decision.** Add two pure leaf libraries and two dedicated deterministic-JSON sidecar projections; wire them
through the **existing** `fsgg verify` / `fsgg ship` hosts. No new top-level command.
- `FS.GG.Governance.CostBudget` (pure) — `CostBudget`, `budgetFor`, the budgeted `CacheDecision`, and the
  cost/cache findings. Carries the F036 agent-review cache identity (Story 5).
- `FS.GG.Governance.CommandKind` (pure) — the `CommandKind` taxonomy, the `KindedCommandRun` wrapper over F032,
  and the provenance-audit-snapshot roll-up over F033.
- `FS.GG.Governance.CostBudgetJson` → `cost-budget.json` (`fsgg.cost-budget/v1`): the per-gate cache decisions
  + the cost/cache findings.
- `FS.GG.Governance.ProvenanceJson` → `provenance.json` (`fsgg.provenance/v1`): the provenance audit snapshot.

**Rationale.** This mirrors the established split exactly: pure leaf cores (F041 `CacheEligibility`, F046
`FreshnessSensing`) projected by a dedicated `*Json` module (F042 `CacheEligibilityJson`, F025 `AuditJson`),
written as a sidecar by a host command (F044 `CacheEligibilityCommand` writes a cache sidecar + an unresolved
sidecar). Dedicated sidecars keep the load-bearing `route.json`/`verify.json`/`audit.json` projections
**byte-identical** (zero risk to existing goldens) while still making the budget outcome and the provenance
snapshot "appear in the result" (Story 1, Story 4). The behavioral change — the budget bounding which gates the
host actually executes — lands at the already-built F052 `ExecuteGates` edge: `Loop.update` calls the pure
`CostBudget.decide` and builds the `ExecuteGates` effect from only the `Recompute` gates; `Reuse` gates reuse;
`Skip`/`Defer` gates are neither executed nor reused, and surface in the sidecar.

**Alternatives considered.** (a) Bump `AuditJson` to `fsgg.audit/v3` with budget + provenance sections —
viable and additive, but enlarges the most load-bearing projection and couples two concerns (verdict + cost)
into one schema bump; rejected in favor of independent sidecars that can evolve separately. (b) A brand-new
`fsgg audit` / `fsgg cost` command — rejected: the spec's assumption prefers reusing an existing host, and the
budget naturally belongs in the verify/ship pipeline where gate execution already happens.

## D4 — Reuse F029/F030/F041/F032/F033/F036/F018-F023 unchanged

**Decision.** F25 changes **none** of: `FreshnessKey.FreshnessInputs` (10 dimensions, `Cost` absent),
`FreshnessKey.matches`/`diff`, `EvidenceReuse.decide`/`RecomputeCause`, `CacheEligibility.evaluate`/the
`Reusable`/`MustRecompute` verdict, `CommandRecord` and its `canonicalId`, `Provenance` and its `canonicalId`,
`AgentReviewKey` and its `matches`, or `Enforcement.deriveEffectiveSeverity` and the truth table. It adds the
`CostBudget`, the budgeted `CacheDecision` *around* the F041 verdict, the cost/cache findings, the command-run
*kind*, and the audit-snapshot roll-up — all in new libraries that **reference** the existing ones.

**Rationale.** This is the spec's central scope guarantee (FR-005, FR-006, FR-013) and the constitution's
Tier-1 reuse discipline. The budgeted cache decision consumes a `CacheEligibilityVerdict` already produced by
F041 (`Reusable of EvidenceRef` → `Reuse`; `MustRecompute of RecomputeCause` → either `Recompute` if the cost
tier fits the ceiling, or `Skip`/`Defer` if not). Reuse therefore happens **only** when the freshness key
proved the evidence applies — F25 never fabricates a reuse and never re-computes a freshness match.

**The stale/cache-invalidated finding is derived, not re-decided.** When F041 returned
`MustRecompute (InputsChanged categories)`, the cost/cache `Stale` finding (a single kind — "stale" and
"cache-invalidated" are synonyms here, not two cases) names exactly those F029 `InputCategory` values via the
existing `FreshnessKey.categoryToken` — no new dimension, no second opinion on what changed.

## D5 — Command-run *kind* is descriptive metadata; F032/F033 identity unchanged

**Decision.** Add `CommandKind = Build | Test | Pack | TemplateInstantiation | GitDiff | PackageInspection |
VisualCapture` and `KindedCommandRun = { Kind: CommandKind; Record: CommandRecord }`. The kind is **descriptive
metadata** that does **not** participate in the reproducible identity: a kinded run's identity is exactly
`CommandRecord.canonicalId record` (F032), reused verbatim, and the provenance audit snapshot's identity is
exactly `Provenance.canonicalId` over the snapshot's command records (F033). Wall-clock duration stays the F032
`SensedDuration`, structurally excluded from identity.

**Rationale.** `CommandRecord` has no `kind` field today and must be reused unchanged (FR-008). Wrapping it
(rather than extending it) keeps F032 pristine and the identity formula untouched, satisfying SC-005 ("command
runs of every kind recorded with reproducible identity; two runs differing only in duration share an
identity") with zero change to the proven identity machinery. The kind is sensed at the host edge — the call
site always knows whether it is spawning a build, a test, a pack, a template instantiation, a git diff, a
package inspection, or a visual capture (FR-008). The provenance audit snapshot is the F033 `Provenance`
(built via `Provenance.build` from the kinded runs' `.Record`s + the sensed provenance inputs) carried
alongside the kinds for the projection; its byte-identity for identical inputs is inherited from
`Provenance.canonicalId` (FR-009, SC-006).

**Note on the synthetic-taint signal.** Evidence taint ("produced synthetically rather than by a real run") is
not a field on F030 `RecordedEvidence` (which stays unchanged). It is a **supplied sensed input** to the pure
`cacheFindings` function — `EvidenceTaint = Real | Synthetic` per gate — consistent with FR-014's
"already-sensed inputs" and the constitution's synthetic-evidence-disclosure principle. A `Synthetic` taint
surfaces a distinct `SyntheticTaint` finding **even when the freshness key matches** (spec edge "synthetic
evidence reused").

## D6 — Determinism, enforcement, and the advisory guarantee

**Decision.** Every decision, finding, and snapshot is pure, total, and byte-identical for identical inputs:
entries are emitted in `GateId`-ordinal order (the F042 precedent), freshness dimensions in `InputCategory`
encoding order, command runs in the order the provenance snapshot fixes (order-significant in F033 identity),
and the JSON projections follow the `Utf8JsonWriter` + closed-enum-token + fixed-field-order discipline shared
by every `*Json` module (FR-011, SC-008). Cost/cache findings carry a `BaseSeverity` and are enforced by
calling the existing `Enforcement.deriveEffectiveSeverity` directly — the truth table is **not** re-opened
(FR-013). Agent-reviewed checks carry `BaseSeverity = Advisory`; because `deriveEffectiveSeverity` never
escalates a base-advisory finding (the F023 guarantee, research D4 there), an agent-reviewed check can never
block under any (`Profile`, `RunMode`) — and F25 **never** calls `AdvisoryPromotion` (F039, a later row)
(FR-010, SC-007).

**Rationale.** Reusing `deriveEffectiveSeverity` for the new findings and the never-escalate guarantee for
agent-reviewed checks is what lets F25 add findings and a budget without touching enforcement, exactly as F24
added `SurfaceFinding`s by setting `BaseSeverity = Advisory` and leaning on the same guarantee.

## Resolved unknowns summary

| Unknown (spec) | Resolution |
|----------------|------------|
| Budget representation (per-tier counts vs ordered ceiling) | **Ordered `Cost` ceiling with a `Cheap` floor**, `min(profileCeiling, modeCeiling)` (D1) |
| Skip vs defer semantics | **By run-mode class**: boundary modes `Defer`, inner-loop modes `Skip`; both named, neither a pass (D2) |
| Surface through existing command vs dedicated projection | **Two dedicated sidecar projections** written by existing `fsgg verify`/`ship`; existing goldens byte-identical (D3) |
| How the command-run *kind* relates to F032 identity | **Descriptive metadata; identity unchanged** — wrap, do not extend (D5) |
| Where the synthetic-taint signal comes from | **Supplied sensed input** to pure `cacheFindings`; F030 unchanged (D5) |
| How new findings reach a blocking verdict | **`deriveEffectiveSeverity` reused directly**; truth table untouched; advisory never escalates (D6) |
