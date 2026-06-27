# Phase 0 Research: CommandHost skeleton extraction

This phase resolves the only genuine unknowns the spec deferred to planning
(FR-005, FR-008): **per-helper move-or-keep membership**, and the design of the
three hard cases where text is identical but type is divergent (`exitCode` over a
per-host `ExitDecision`, the `GateClassification` DU, and the `executionPlan`
tuple). There were no `NEEDS CLARIFICATION` markers in the Technical Context; the
research below is the duplication/divergence audit that fixes the leaf's surface.

The governing rule throughout (from the spec's Session 2026-06-27 clarifications):
**only genuinely-shareable members move; type-divergent members stay local, and
the divergence is recorded. Byte-identity is the gate that catches a wrong move.**

---

## Audit of candidate helpers (working-tree spot-check)

Source: `grep` over `src/*Command/Loop.fs` and `Loop.fsi` (2026-06-27 working tree).
"Sites" lists the defining hosts; `Loop.fs` line numbers are representative.

| Helper | Defining hosts | Text identical? | Decision |
|---|---|---|---|
| `under repo rel` | Route, Ship, Verify, Refresh, CacheEligibility (+Release) | **Yes** (verbatim) | **MOVE** |
| `fail cat msg model` | Route, Ship, Verify, Refresh, CacheEligibility, Release | **Yes** | **MOVE** |
| `revOfCommit (CommitId c)` | Route, Ship, Verify, CacheEligibility | **Yes** (1-liner) | **MOVE** |
| `baseHeadOf model` | Route, Ship, Verify, CacheEligibility | **Yes** | **MOVE** |
| `emptySensedFacts` | Route, Ship, Verify | **Yes** | **MOVE** |
| `describeInvalid` | Route, Ship, Verify | verify in impl | **MOVE if identical** |
| `persistedContent` / `awaitingPersist` | Route, Ship, Verify | verify in impl | **MOVE if identical** |
| `exitCode decision` | all 7 | logic same, **DU differs** | **MOVE via canonical DU** — see D2 |
| `GateClassification` (DU) | Route(3), Ship(4), Verify(4) | **DU differs** (Route lacks `Deferred`) | **MOVE as superset** — see D3 |
| `executionPlan model` | Route(2-tuple), Ship/Verify(3-tuple+budget) | **signature differs** | **MOVE parameterized** — see D4 (FR-006 MUST) |
| `tryExecute` | Route, Ship, Verify | verify in impl | **MOVE if identical** |
| `buildSnapshot` | Verify, Ship, Release | **signature differs** (Release takes a different input) | Move Verify↔Ship common form; **Release stays local** — see D5 |
| `kindedRunsOf` / `kindOf` | Verify, Ship | verify in impl | **MOVE if identical (Verify↔Ship)** |
| `cacheReportOf` | CacheEligibilityCommand only | n/a (single site) | **STAY LOCAL** — no duplication to remove (D6) |

"verify in impl" = confirmed-identical text is the move precondition; the
one-concern-per-commit discipline (FR-013) means each move's commit runs the full
golden suite and reverts on any drift.

---

## Decisions

### D1 — New leaf: name, placement, packaging

**Decision.** Add `FS.GG.Governance.CommandHost` as a new pure leaf under
`src/FS.GG.Governance.CommandHost/` with `CommandHost.fsi` + `CommandHost.fs`,
`IsPackable=true`, `PackageId=FS.GG.Governance.CommandHost`, `Version=0.1.0` —
identical project shape to the Phase A `JsonWriters` leaf. Placed **below** the
command hosts and **above** the domain-type projects it walks.

**Rationale.** Mirrors the delivered Phase A convention exactly; keeps the graph
acyclic (.NET forbids reference cycles, and a scope-guard test forbids host edges).

**Alternatives rejected.** (a) Putting the helpers in `Kernel` — rejected for the
same reason Phase A did: pure leaves sit *below* everything and `Kernel` carries a
"BCL/FSharp.Core-only" guard. (b) One leaf per helper group — unjustified
fragmentation; the host skeleton is one cohesive concern.

### D2 — `exitCode` and the canonical `ExitDecision` DU

**Problem.** `exitCode` is the same `match` in all hosts, but each host declares its
own `ExitDecision` DU in its own `Loop.fsi`, and the cases diverge: Route/Refresh/
Evidence/CacheEligibility have `{Success; UsageError'; InputUnavailable; ToolError}`
while Ship/Verify/Release add `Blocked -> 1` (the merge-blocking verdict). FR-005
mandates moving `exitCode` "accommodating the optional blocked path".

**Decision.** Move a **canonical `ExitDecision`** DU (the superset, *with* `Blocked`)
and a total `exitCode : ExitDecision -> int` into the leaf:
`Success->0; Blocked->1; UsageError'->2; InputUnavailable->3; ToolError->4`. Each
host's `Loop` re-exports the leaf type to preserve its own public surface
(`type ExitDecision = CommandHost.ExitDecision`) and calls the leaf `exitCode`.
Hosts that never produce `Blocked` simply never construct that case — output is
unchanged.

**Why this is byte-identical and warning-clean.** `exitCode` is total over the
superset, so no non-exhaustive match arises *in the leaf*. The risk is the other
direction: a host's *own* `match` that pattern-matches an `ExitDecision` value (e.g.
the `ExitCodeBasis -> ExitDecision` mappers at `ShipCommand:349`, `Verify:365`,
`Release:256`) — those construct, not exhaustively consume, so adding a `Blocked`
case to a host that previously lacked it does not force new arms there. Where a host
*does* exhaustively `match` over `ExitDecision`, adopting the superset adds a
required `Blocked` arm; under `TreatWarningsAsErrors` the compiler flags it
immediately and we add a behavior-preserving arm (mapped exactly as `exitCode`
would). The exit-code integers every host emits are unchanged → byte-identical.

**Alternative rejected.** Keep `exitCode` local (treat it as type-divergent). FR-005
explicitly lists it as a move target with the blocked path called out, and a single
canonical exit mapping is the entire point of the finding; the superset DU is the
honest shared form, not a leaky abstraction.

### D3 — `GateClassification` DU

**Problem.** Route's DU is `{ToExecute; ToReuse; NoCommand}` (3 cases); Ship and
Verify add `Deferred of BudgetReason` (the F25 cost-budget demotion). Identical
text for three cases, divergent in the fourth.

**Decision.** Move the **superset** 4-case `GateClassification`
(`ToExecute of GateCommand | ToReuse of ExitCode | Deferred of BudgetReason |
NoCommand`) into the leaf. Route never *produces* `Deferred` (its `executionPlan`
params carry no budget fold — D4), so its plans are unchanged. Any Route `match`
that *consumes* a classification gains a `Deferred` arm; under
`TreatWarningsAsErrors` the compiler points at each site and we add a
behavior-preserving arm (Route's `Deferred` is unreachable; map it as it would map a
non-executed gate, or `failwith` an invariant message — chosen at the use site to
keep output identical). Byte-identity confirms no Route plan changed.

**Coupling note.** `GateClassification` and `executionPlan` move together (the plan
produces classifications) — they are sequenced as one commit-group (D4, FR-013).

**Alternative rejected.** Keep two DUs (a 3-case Route one + a 4-case Ship/Verify
one) and not share the type. That blocks the FR-006 shared `executionPlan` (its
return references the classification) and re-introduces the very divergence the
finding targets. The superset with never-produced cases is the Phase-A-style honest
share; the byte-identity gate is the safety net.

### D4 — Parameterized `executionPlan` (FR-006, the gated hard case)

**Problem.** Route's `executionPlan : Model -> (Gate*GateClassification) list *
Map<string,FreshnessInputs>` (2-tuple, no budget). Ship/Verify's is
`... * CacheDecisionReport` (3-tuple) and inserts the F25 cost-budget fold
(`CostBudget.Budget.budgetFor`/`decide`/`overBudget`) that can demote a `ToExecute`
to `Deferred`. The non-budget prefix (resolve freshness → cache-eligibility →
`verdictMap`/`inputsMap` → `classify` base) is **identical** across all three.

**Decision.** Move one shared `executionPlan` to the leaf, parameterized by a
**plain record** of the per-command optional folds (Constitution III — no SRTP, no
generics):

```fsharp
type ExecutionPlanParams =
    { /// Per-command budget fold. None ⇒ no demotion (Route). Some ⇒ Ship/Verify:
      /// given the verdict map, returns (over-budget gate-id → reason) map + the report.
      BudgetFold: (Map<string, CacheEligibilityVerdict> -> Map<string, BudgetReason> * CacheDecisionReport) option }
```

The shared function always computes the identical non-budget prefix, then applies
`BudgetFold` when present (Ship/Verify) or skips it (Route), and returns the
**3-tuple** uniformly with `CacheDecisionReport` being the budget report when a fold
ran or the empty `CacheDecisionReport []` when it did not. Route destructures and
discards the report (it never used one); Ship/Verify use it as before. The
`classify` closure is the shared base; the `Deferred` demotion is applied only
inside `BudgetFold`'s consumer path, so Route's classifications are bit-for-bit what
they were.

**Why a record of folds, not a flag or command-identity branch.** The spec edge case
is explicit: per-command differences must be expressed *as parameters*, never as
`if command = Ship` branches inside the leaf. The record carries behavior (the fold
closure), supplied by each host, so the leaf has no knowledge of which command it
serves.

**Risk + gate.** This is the highest-risk move. It is sequenced **last**, as its own
commit-group, immediately gated by the Route/Ship/Verify `route.json`/`audit.json`/
`verify.json` goldens. If any byte moves, the parameterization changed behavior and
is revised (or, in the worst case, the helper is split back to local per FR-008 with
the divergence recorded — but D4 is expected to hold because the prefix is provably
identical and the fold is the only difference). The semi-radical single
`GateRunHost` unification remains **out of scope** (FR-012, deferred to Phase C and
gated on this diff staying clean).

### D5 — `buildSnapshot`, `kindedRunsOf`, `kindOf`

**Decision.** Move the **Verify↔Ship** common forms of `buildSnapshot`,
`kindedRunsOf`, `kindOf` once confirmed byte-identical between those two. The
**ReleaseCommand** `buildSnapshot` takes a different input
(`ReleaseDeclaration * PackEvidenceSet` rather than a `KindedCommandRun list`), so
it is a *different function that happens to share a name* — it **stays local**
(FR-008 type-divergence; recorded here). `kindedRunsOf`/`kindOf` exist only in
Verify/Ship, so moving them removes a 2-copy duplication.

### D6 — `cacheReportOf` stays local

**Decision.** `cacheReportOf` is defined in **CacheEligibilityCommand only** (the
design report's "4 copies" predates drift; the working tree shows a single
surviving site). With no remaining duplication to remove, it **stays local** — moving
a single-site helper would add surface for no de-duplication gain (FR-008 spirit:
the leaf stays *honestly* shared). Recorded as a deliberate keep.

### D7 — Leaf dependency set

**Decision.** The leaf's ProjectReferences are exactly the domain-type projects whose
values the moved helpers construct or walk — a subset of the command hosts' own
references, never a host:
`Config`, `Snapshot`, `Gates`, `GateRun`, `GateExecution`, `Route`,
`FreshnessSensing`, `FreshnessResolution`, `FreshnessKey`, `CacheEligibility`,
`EvidenceReuse`, `CommandKind`, `CostBudget` (for `BudgetReason`/`CacheDecisionReport`
in the `executionPlan`/`GateClassification` surface). The exact list is finalized by
the compiler during implementation (add only what the moved bodies need). A
**scope-guard test** asserts the leaf references none of:
`Host`, `Cli`, any `*Command`, and takes no filesystem/git/process project —
mirroring the Phase A `JsonWriters` guard.

**Rationale.** Purity (FR-002) and the acyclic pure-core/impure-host split (FR-011)
are enforced *as a test*, not just by convention.

---

## D9 — Implementation-discovered stay-local set (FR-008, recorded at delivery)

The working-tree audit during D1–D8 was a textual spot-check. Implementation
applied the byte-identity + **type-honesty** gate to the actual code and found that
several helpers the spec's FR-005 list named as move candidates are in fact
**type-divergent on each host's own `Model`/`Effect` record** — exactly the
`dispositionToken`/`CaptureHelpers` precedent the spec's Session-2026-06-27 Q2
clarification and FR-008 anticipate. A shared form would require generics over each
host's concrete `Model`/`Effect` (Constitution III forbids the unjustified SRTP/
generics that would need), so they correctly **stay local**:

| Helper | Why it stays local (type divergence) |
|---|---|
| `fail` | Returns `Model * Effect list` via record-update on the host's OWN `Model`; constructs the host's OWN `Diagnostic`. Distinct type per host. |
| `tryExecute` | Returns `Model * Effect list` over the host's OWN `Model`/`Effect`; orchestrates host-specific join state. |
| `awaitingPersist` | Reads three host-`Model` fields; a one-liner whose only shareable form would take three decomposed `bool`s — no readability or de-dup gain. |
| `exitCode` + `ExitDecision` | Public in all six hosts' `Loop.fsi` (with public `Model.Exit`/`Diagnostic.Category` fields typed `ExitDecision`). The canonical superset DU + total `exitCode` were built and unit-tested in the leaf (research D2), but host **adoption** is a high-churn public-surface cascade (six host surface re-bless + interpreter/Cli/test case-resolution ripple) disproportionate to a 5-line mapping. Deferred as a bounded follow-up; the leaf retains the tested canonical form for that adoption. |
| RefreshCommand `fail`/`exitCode` | Parameterized by `RefreshOutcome` (a different DU with different cases/codes), not `ExitDecision` — confirmed divergent at audit. |

**What actually moved** (all gated by byte-identical goldens — every command and
projection golden/snapshot unchanged): `under`, `revOfCommit`, `baseHeadOf`
(decomposed to the snapshot diff-range), `emptySensedFacts`, `describeInvalid`,
`persistedContent`, the superset `GateClassification`, the parameterized
`executionPlan` (FR-006 — Route supplies `BudgetFold = None`, Ship/Verify supply
their budget-fold closure), and the Verify↔Ship `kindOf` (re-exported to preserve
each host's public surface), `kindedRunsOf`, `buildSnapshot` (both decomposed to
their model-view inputs). The model-reading movers are **decomposed** into the
fields they read so the leaf depends on NO host `Model`.

**New edge, blessed and noted (FR-011):** Route gains a direct `CostBudget` edge
because the shared superset `GateClassification` carries `Deferred of BudgetReason`
and `executionPlan` returns `CacheDecisionReport`. Route never *produces* `Deferred`
(its `BudgetFold = None`); it only *consumes* the shared type. `CostBudget` is a
pure domain core, so the edge keeps the graph acyclic and the pure/impure split
intact. Recorded in `RouteCommand.Tests/SurfaceDriftTests` allowlist.

**SC-004 note:** net host reduction came to ≈ **−318 LOC** (170 ins / 488 del across
the six hosts), below the spec's 400–500 estimate. The gap is precisely the
stay-local set above (the estimate assumed `fail`/`tryExecute`/`awaitingPersist`/
`exitCode` would move); keeping them local is the honest FR-008 outcome, not a
shortfall in the extraction.

## Consolidated outcome

- **Move:** `under`, `fail`, `revOfCommit`, `baseHeadOf`, `emptySensedFacts`,
  `describeInvalid`, `persistedContent`, `awaitingPersist`, `tryExecute`,
  canonical `ExitDecision`+`exitCode`, superset `GateClassification`,
  parameterized `executionPlan`, and the Verify↔Ship `buildSnapshot`/`kindedRunsOf`/
  `kindOf` — all gated by byte-identical goldens, moved one concern per commit.
- **Stay local (recorded divergence):** Release's `buildSnapshot` (different input
  type — D5); `cacheReportOf` (single site, no duplication — D6); any host-specific
  `ExitCodeBasis -> ExitDecision` mapper (host policy, not skeleton).
- **Out of scope (FR-012):** the `GateRunHost` unification (Phase C),
  the `VerifyCommand`/`VerifyJson` god-module split (Phase C), CLI decomposition
  (Phase E).
- **Verification:** existing golden/snapshot suites byte-identical (FR-009);
  new leaf surface baseline + drift test + scope guard (FR-003/FR-004/FR-002);
  full suite green with per-project counts unchanged except the additive
  `CommandHost.Tests` (FR-010).
