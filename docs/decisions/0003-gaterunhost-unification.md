# ADR 0003 — Defer the `GateRunHost` unification of `route → ship → verify`

**Status**: Accepted (verdict: **DEFER**) · **Date**: 2026-06-27 · **Feature**:
`specs/076-verify-module-split` (Phase C, US3)

**Resolves**: the open roadmap question of whether to unify the three gate-run host
loops (`route`, `ship`, `verify`) into one parameterized `GateRunHost`, recorded as
the explicit pursue/defer/drop verdict this feature owes (FR-008, SC-005).

## Context

The `route`, `ship`, and `verify` command hosts (`RouteCommand/Loop.fs`,
`ShipCommand/Loop.fs`, `VerifyCommand/Loop.fs`) share a near-identical MVU skeleton:
classify the selected gates → run the must-recompute command-gates → capture
evidence → relocate the verdict → project the document + sidecars → persist the
grown store. Phase B (feature `075`, ✅ DELIVERED) extracted the genuinely-shared,
host-`Model`-free parts of that skeleton into the pure `FS.GG.Governance.CommandHost`
leaf (`under`/`revOfCommit`/`baseHeadOf`/`emptySensedFacts`/`describeInvalid`/
`persistedContent`/`GateClassification`/`executionPlan`/`kindOf`/`kindedRunsOf`/
`buildSnapshot`), and did so under byte-identical goldens. The roadmap recorded that
delivery as the precondition for *considering* the larger, semi-radical step: fold
the remaining per-host `executionPlan`/`tryExecute`/projection scaffolding into a
single parameterized `GateRunHost` so route/ship/verify become thin configurations
of one host.

Phase C (this feature) splits the two Verify *god modules* along their feature seams
with zero observable behavior change. It deliberately does **not** touch the
route/ship/verify host skeletons; its committed scope is intra-project legibility,
not a cross-host unification.

## Decision

**DEFER** the `GateRunHost` unification. It is **not** implemented in this feature,
and the `route`/`ship`/`verify` host skeletons are left **unchanged** (FR-008, second
clause).

## Rationale

- **The gate to *consider* it is satisfied.** Phase B shipped the shared
  `CommandHost` leaf byte-identically (CLAUDE.md / roadmap), so the roadmap's
  precondition for evaluating the unification is met. This ADR therefore takes a
  position rather than declining to look.
- **It strictly contains an already-deferred cascade.** Phase B's own delivery note
  deferred the *smaller* `exitCode` + `ExitDecision` host adoption because it is "a
  six-host public-`Loop.fsi` surface cascade + interpreter/Cli/test ripple." A full
  `GateRunHost` — one parameterized host replacing the `executionPlan`/`tryExecute`/
  projection scaffolds across route/ship/verify — strictly *contains and exceeds*
  that cascade: it pays the same `Loop.fsi`/interpreter/Cli/test ripple across three
  more hosts, plus the type-reconciliation of each host's own `Model`/`Msg`/`Effect`/
  `ExitDecision` (which Phase B found textually identical but **type-divergent**, the
  reason `fail`/`tryExecute`/`awaitingPersist`/`exitCode` stayed local per FR-008).
- **Phase C does not require it.** The committed scope (the two Verify splits) is
  achievable — and was achieved — without it. Forcing the unification into this
  clarity-dominated phase would couple a byte-identical refactor to a Tier-1 surface
  cascade across three hosts, against FR-008's "left unchanged unless the ADR elects
  to."

## Re-entry condition

Revisit the unification when **either**:

1. the deferred `exitCode` + `ExitDecision` six-host adoption follow-up lands — it
   pays the same `Loop.fsi`/interpreter/Cli/test surface cascade, so the marginal
   cost of the full `GateRunHost` drops once that ripple is already being paid; **or**
2. a **fourth** gate-run host appears — at which point one parameterized host
   amortizes across four call sites instead of three and the duplication cost
   crosses the threshold that justifies the cascade.

## Consequences

- `route`/`ship`/`verify` keep their three separate, already-`CommandHost`-backed MVU
  loops; no public `Loop.fsi` surface across the three hosts changes from this ADR.
- The shared skeleton continues to live in the `CommandHost` leaf; further sharing is
  additive to that leaf (the Phase B pattern), not a new cross-host host module.
- This ADR is the sole Phase-C deliverable for US3; no source under
  `src/FS.GG.Governance.RouteCommand` or `src/FS.GG.Governance.ShipCommand` is
  modified by feature `076` (verified: `git diff --stat` over both is empty).
