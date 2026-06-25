# Implementation Plan: Cost-Cache Host Wiring — `fsgg verify` / `fsgg ship` Budget Filtering, Kinded-Run Recording, and the Two Provenance Sidecars (F25 wiring)

**Branch**: `064-cost-cache-host-wiring` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/064-cost-cache-host-wiring/spec.md`

## Summary

F25 (`060-cost-cache-command-provenance`) landed four pure cores — `FS.GG.Governance.CostBudget` (the
ordered-`Cost`-ceiling `budgetFor`, the per-gate `decide` folding the F046 cache verdict with the budget into
`Reuse`/`Recompute`/`OverBudget`, and the advisory `cacheFindings`/`enforce`), `FS.GG.Governance.CommandKind` (the
seven-kind `CommandKind` taxonomy over the F032 `CommandRecord` and the `auditSnapshot` roll-up), and the two
deterministic sidecar projections `FS.GG.Governance.CostBudgetJson` (`fsgg.cost-budget/v1`, `ofReport`) and
`FS.GG.Governance.ProvenanceJson` (`fsgg.provenance/v1`, `ofSnapshot`) — fully built, packed, and green (85
semantic/property tests; four blessed surface baselines), but **wired into no command host**. This row consumes
those four surfaces **additively** at the MVU interpreter edge of the two mature hosts `fsgg verify`
(`FS.GG.Governance.VerifyCommand`) and `fsgg ship` (`FS.GG.Governance.ShipCommand`). It adds **no** new pure core,
**no** new report object, and **no** new dependency beyond ProjectReferences onto the four already-built libraries.

**Technical approach, grounded in a host reconnaissance** (research.md D1–D8). Both hosts already share an almost
identical MVU shape, and three of the four raw materials the wiring needs are *already flowing*:

- **The cache verdict is already computed.** Each host's `executionPlan` (`Loop.fs`) already calls
  `CacheEligibility.evaluate candidates store` and classifies each gate `ToExecute | ToReuse | NoCommand`. The
  budget filter is a thin pure layer that re-reads those same per-gate `CacheEligibilityVerdict`s as
  `CandidateCost`s, runs `CostBudget.budgetFor` + `decide`, and demotes an over-budget `MustRecompute` gate from
  `ToExecute` to deferred/skipped — it does not re-sense anything.
- **The `CommandRecord`s already flow back.** Both hosts' `Msg` already carries
  `GatesExecuted of (GateId * CommandRecord) list`; `GateExecution.Interpreter.senseExecution` already assembles a
  real `CommandRecord` per executed gate. Kinded-run recording is one pure `kindOf : Gate -> CommandKind` map over
  those records into `KindedCommandRun`s — no new execution I/O.
- **The provenance inputs are already sensed.** Base/Head revisions live in the `RepoSnapshot`; rule hash, generator
  version, and artifact digests live in `SensedFacts` (F046). Only two genuinely new edge senses are needed —
  `EnvironmentClass` and a normalized `BuilderIdentity` — and both must be normalized (no username, no wall-clock) to
  keep `provenance.json` byte-deterministic.

So the new code is small and lives entirely at the host edge: a pure budget-filter step inside `update`, a pure
`kindOf` map, two new `WriteArtifact` sidecar effects, and two new edge senses (environment/builder). The four cores
are consumed verbatim.

**The central reconciliation (research.md D1) — byte-identity vs. budget filtering.** The spec demands two things
that pull against each other: FR-001 says the budget MUST change *which gates execute* (an over-budget
must-recompute gate is deferred), while FR-007/SC-004 say every existing `verify.json` / `audit.json` / ship golden
MUST stay **byte-identical**. These reconcile only through one fact, which the reconnaissance pins down precisely:
the **default** budget is `budgetFor Standard Verify = budgetFor Standard Gate = min(Medium, High) = Medium`. An
existing golden therefore stays byte-identical **iff its fixture's must-recompute gates all fit the Medium ceiling**
(`Cheap`/`Medium`). The plan's safety obligation is to **prove that property of every frozen golden fixture before
wiring**, then exercise deferral only through **new** tight-budget fixtures (a `High`/`Exhaustive` must-recompute
gate under a `Light`/inner budget) that produce the **new** sidecars. If any existing golden fixture is found to
hold an over-ceiling must-recompute gate, that is surfaced as a real behavioral change (not silently absorbed) and
escalated — it is not papered over by re-blessing the golden. This is the row's non-negotiable anchor (SC-004).

**Confirmed planning decisions** (full rationale in [research.md](./research.md)):

1. **Budget filter is a pure demotion layer inside `executionPlan`/`tryExecute` (D1, D3).** The host builds a
   `CandidateCost` per selected gate from the cost it already knows (`Config.Cost`), the `CacheEligibilityVerdict` it
   already computed, and an `AgentReviewMark`; runs `CostBudget.decide (budgetFor profile mode) mode candidates`; and
   uses the resulting `CacheDecisionReport` to demote `OverBudget` gates out of the `ToExecute` set. A deferred/skipped
   gate is never added to `Passing` (it never reaches `applyExecution`'s passed set), so it is **never reported as
   passed** (FR-001, SC-002).
2. **Existing goldens stay byte-identical by fixture-budget invariant, verified up front (D1).** Every frozen
   `verify.json` / `audit.json` / ship golden fixture is audited to confirm its must-recompute gates fit the default
   `Medium` ceiling; deferral is exercised only by new fixtures (FR-007, SC-004).
3. **Kinded-run recording is a pure `kindOf` map over the `CommandRecord`s already returned (D2).** On
   `GatesExecuted`, each executed gate's `CommandRecord` is paired with `kindOf gate : CommandKind` into a
   `KindedCommandRun`; identity is `CommandRecord.canonicalId` verbatim, so two runs differing only in sensed
   duration share an identity (FR-004).
4. **Provenance inputs reuse the already-sensed facts; only environment/builder are new, and both are normalized
   (D4).** `auditSnapshot` is fed Base/Head from the snapshot, rule-hash/generator-version/artifact-digests from
   `SensedFacts`, `SourceCommit = Head`, the kinded runs from (3), and a normalized `EnvironmentClass` /
   `BuilderIdentity` sensed at the edge with **no** username, hostname, or wall-clock — guaranteeing
   `provenance.json` byte-determinism (FR-006, SC-003).
5. **Two new sidecars via two new `WriteArtifact` effects beside the existing artifacts (D5).** `cost-budget.json`
   (`CostBudgetJson.ofReport report findings`) and `provenance.json` (`ProvenanceJson.ofSnapshot snapshot`) are
   written through each host's **existing** atomic `WriteArtifact` interpreter port under two new `ArtifactKind`
   cases, to default paths `readiness/cost-budget.json` and `readiness/provenance.json`. No existing write path
   changes (FR-005, FR-010).
6. **Cost/cache findings fold through the existing enforcement machinery as advisory only, and land in the sidecar —
   not in the existing goldens (D6).** `Findings.cacheFindings report taint` + `Findings.enforce mode profile` are
   run to confirm each finding is advisory; the findings are projected into `cost-budget.json`. They are **not**
   appended to the `ShipDecision` that `audit.json`/`verify.json` project — that is precisely what keeps those
   goldens byte-identical. No new verdict, no new exit-code, no truth-table change (FR-008, SC-004).
7. **Agent-reviewed checks stay advisory under every profile/mode (D7).** The `AgentReviewMark` on each
   `CandidateCost` carries the agent-review identity through `decide`; an agent-reviewed gate's cache decision never
   promotes it to a blocker, and its evidence reuses only on a matching `CacheKey` (FR-009, SC-006).
8. **Standalone path uses product-local sources only; missing/unreadable inputs surface a clear input signal (D8).**
   The budget/cache/provenance path draws only on the host's existing product-local store-reader and sensed facts —
   no monorepo path. A missing/unreadable evidence store keeps the host's existing degrade-with-currency-note
   behavior and produces well-formed empty-array sidecars; an absent provenance input names the offending source as
   input-not-defect (FR-011, FR-012, SC-005).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`, `LangVersion=latest`).

**Primary Dependencies**: **No new external/NuGet dependency.** The two hosts add **ProjectReferences** onto the four
already-built F25 libraries: `FS.GG.Governance.CostBudget`, `FS.GG.Governance.CommandKind`,
`FS.GG.Governance.CostBudgetJson`, `FS.GG.Governance.ProvenanceJson`. Their transitive leaf deps
(`Config`, `Enforcement`, `Gates`, `EvidenceReuse`, `CacheEligibility`, `FreshnessKey`, `AgentReviewKey`,
`CommandRecord`) are **already** referenced by both hosts except `FS.GG.Governance.Provenance` (F033) — added to each
host for `auditSnapshot`'s `BuilderIdentity`/`Provenance` inputs. All four cores are consumed verbatim; none is
modified.

**Storage**: Two **new** deterministic JSON sidecars written through each host's **existing** atomic
`WriteArtifact` port: `cost-budget.json` (`fsgg.cost-budget/v1`) and `provenance.json` (`fsgg.provenance/v1`), at
default `readiness/cost-budget.json` and `readiness/provenance.json`. Every existing artifact
(`verify.json`/`audit.json`/route/ship goldens, `evidence-reuse.json` store) keeps its existing write path,
byte-identical (FR-007). No new store or port is introduced; the budget reads the store the host already loads.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck / FsCheck 2.16.6 (repo standard). Each host's existing `.Tests`
project is **extended** with: (a) a **fixture-budget invariant** test proving every frozen golden fixture's
must-recompute gates fit the default `Medium` ceiling, plus the existing `verify.json`/`audit.json` golden compared
byte-for-byte against its frozen pre-wiring baseline (SC-004); (b) a **tight-budget deferral** integration fixture
(one `Cheap` in-budget must-recompute gate, one `High`/`Exhaustive` over-budget must-recompute gate, one reusable
gate) asserting the over-budget gate is absent from the executed set, recorded `OverBudget` with a named
`BudgetReason`, charges nothing, and is **never** in `Passing` (SC-001, SC-002); (c) a **re-run determinism** test
asserting both sidecars are byte-identical across two runs over an unchanged tree and order-independent under
candidate reordering (SC-003); (d) a **standalone + missing-store** pair asserting product-local-only sources and a
clear input diagnostic with no fabricated reuse (SC-005); (e) an **agent-reviewed-stays-advisory** assertion across
the enforcement path (SC-006). Pure `budgetFor`/`decide`/`cacheFindings`/`auditSnapshot` are already covered by F25's
85 tests and are reused, not re-tested. Real cores are never mocked; only the edge ports (store reader, executor,
artifact writer, environment/builder sensors) are faked, and any synthetic terminal/environment input carries
`Synthetic` in the test name and a use-site disclosure (Constitution V).

**Target Platform**: Cross-platform .NET CLI executables (Linux/macOS/Windows). The two sidecars are normalized
(no path/username/clock/environment leakage) so they are byte-identical across machines (FR-006).

**Project Type**: Host-edge wiring — no new library, no new pure core. Extends **2** existing command-host MVU edges
(`VerifyCommand`, `ShipCommand`); consumes four already-built F25 libraries; single-solution F# layout.

**Performance Goals**: Not a hot path. The budget filter is one `decide` fold over the already-selected gates; kinded
recording is one `kindOf` map over the records already returned; each sidecar is one `ofReport`/`ofSnapshot` pass.
No new process spawn, no extra store read.

**Constraints**: Every existing persisted/`--json` contract (`verify.json`, `audit.json`, route/ship goldens) stays
**byte-identical** for identical repository state (FR-007, SC-004). Both sidecars are deterministic — stable
ordering, normalized paths, no wall-clock/username/environment dependence — so identical inputs yield byte-identical
output and reordering candidates changes nothing (FR-006, SC-003). The cost/cache findings are **advisory only**: no
new verdict, no new exit-code, no enforcement-truth-table change (FR-008, SC-006). No filesystem/process/registry
dependency enters any pure core; the only new I/O (the two sidecar writes, the environment/builder senses) lives at
the interpreter edge through existing-shaped ports (FR-010). Safe failure preserved: a missing/unreadable store or
absent provenance input surfaces a clear input-vs-defect signal with no swallowed error and no fabricated reuse/pass
(FR-012, Constitution VI).

**Scale/Scope**: Extends **2 hosts** (`verify`, `ship`): each gains the budget-filter step in `update`, the `kindOf`
map on `GatesExecuted`, two new `WriteArtifact` sidecar effects + two new `ArtifactKind` cases, two new edge senses
(environment/builder), two new `RunRequest` output paths, and ProjectReferences onto the four F25 cores (+
`Provenance`). **No** new library, report object, verdict, exit-code scheme, JSON schema, or external dependency.
P1 = budget-filtered execution + the two sidecars + byte-identity (US1+US2); P2 = standalone/missing-store safe
failure (US3).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. The four consumed surfaces (`budgetFor`/`decide`/
  `cacheFindings`/`enforce`, `auditSnapshot`/`kindOf`-target `CommandKind`, `ofReport`, `ofSnapshot`) already exist
  as `.fsi` and are exercised by F25 tests. The only new public surface is each host's grown `RunRequest`/`Effect`/
  `ArtifactKind` and the `kindOf` map; it is drafted in each host's curated `.fsi`, exercised through the loaded host
  surface (parse/dispatch/persist), then surface-baselined.
- **II. Visibility Lives in `.fsi`** — PASS. Each extended host already ships curated `.fsi` files
  (`Loop.fsi`/`Interpreter.fsi`); the new `ArtifactKind` cases, `Effect` cases, `RunRequest` fields, and `kindOf`
  are declared there; `.fs` bodies carry no access modifiers. Host surface baselines re-blessed; the four F25
  cores' baselines are unchanged (consumed, not modified).
- **III. Idiomatic Simplicity** — PASS. Plain pattern matches on `CacheDecision`/`CommandKind`/`Cost`, pipelines,
  exhaustive matches; no SRTP/reflection/type-providers/custom CEs. `kindOf` is a total match over a gate's declared
  command category. No new external dependency.
- **IV. Elmish/MVU Is the Boundary** — PASS. Both hosts are already MVU `Loop`/`Interpreter`/`Program`. The budget
  filter and `kindOf` map are pure additions inside `update`/`executionPlan`; the two sidecar writes and the
  environment/builder senses are `Effect`s executed at the interpreter edge. Pure-transition coverage (budget demotes
  the right gates, sidecars project deterministically) plus interpreter-edge coverage (real atomic write, real
  store reader) both land.
- **V. Test Evidence Is Mandatory** — PASS. Fail-before/pass-after fixtures over the **real** F25 cores and real
  hosts: the tight-budget deferral fixture, the re-run determinism fixture, the byte-identity goldens vs. frozen
  baselines, the standalone + missing-store pair. Edge ports are faked; synthetic environment/builder inputs are
  `Synthetic`-named and disclosed.
- **VI. Observability and Safe Failure** — PASS. A deferred/skipped gate is recorded with a named `BudgetReason`
  (never silent, never a pass); a missing/unreadable store keeps the existing degrade-with-note path and yields
  well-formed empty sidecars; an absent provenance input names the offending source as input-not-defect. No swallowed
  error, no fabricated reuse/pass.

**Change Classification: Tier 1 (contracted change)** — adds two new deterministic JSON contracts
(`cost-budget.json` `fsgg.cost-budget/v1`, `provenance.json` `fsgg.provenance/v1`) and changes the two wired hosts'
public effect/model/request surface (re-blessing their surface baselines), while leaving every existing JSON golden
byte-identical and introducing no new external dependency. The full chain applies: spec, plan, host `.fsi` updates,
re-blessed surface baselines, test evidence, and docs (including flipping F25 Phase 8 to complete in
`specs/060-cost-cache-command-provenance/tasks.md` and updating `docs/initial-implementation-plan.md`'s "Remaining"
note).

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/064-cost-cache-host-wiring/
├── plan.md                          # This file (/speckit-plan output)
├── research.md                      # Phase 0 — D1..D8 (incl. the byte-identity vs. budget reconciliation)
├── data-model.md                    # Phase 1 — host-edge glue: CandidateCost build, kindOf map, snapshot inputs,
│                                    #   grown RunRequest/Effect/ArtifactKind, sidecar paths
├── quickstart.md                    # Phase 1 — per-story validation scenarios
├── contracts/                       # Phase 1
│   ├── budget-filter.md             #   selected gates → CandidateCost → decide → demote OverBudget from ToExecute
│   ├── kinded-run-recording.md      #   CommandRecord (already returned) + kindOf → KindedCommandRun → auditSnapshot
│   ├── sidecars.md                  #   ofReport/ofSnapshot → two new WriteArtifact effects; default paths; determinism
│   ├── findings-fold.md             #   cacheFindings + enforce → advisory-only; in sidecar, NOT in existing goldens
│   └── host-surface.md              #   grown RunRequest/Effect/ArtifactKind; JSON byte-identity anchor; deferrals
├── checklists/
│   └── requirements.md              # (already present — spec quality checklist)
└── tasks.md                         # Phase 2 (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.VerifyCommand/                       # EXTEND — budget filter + kinded runs + 2 sidecars
│   ├── Loop.fsi / Loop.fs                                #   executionPlan: build CandidateCost, decide, demote
│   │                                                     #     OverBudget from ToExecute; on GatesExecuted: kindOf →
│   │                                                     #     KindedCommandRun → auditSnapshot; new ArtifactKind +
│   │                                                     #     WriteArtifact for cost-budget.json/provenance.json;
│   │                                                     #     verify.json stays byte-identical
│   ├── Interpreter.fsi / Interpreter.fs                  #   handle the 2 new WriteArtifact (existing atomic write);
│   │                                                     #     senseEnvironment / senseBuilder edge ports (normalized)
│   ├── Program.fs                                        #   wire the new real ports
│   └── FS.GG.Governance.VerifyCommand.fsproj             #   + ProjectReference CostBudget, CommandKind,
│                                                         #     CostBudgetJson, ProvenanceJson, Provenance
├── FS.GG.Governance.ShipCommand/                         # EXTEND — same wiring; audit.json stays byte-identical;
│   ├── Loop.fsi / Loop.fs                                #     budget uses (request.Profile, request.Mode) at the
│   ├── Interpreter.fsi / Interpreter.fs                  #     merge boundary (Gate/Release)
│   ├── Program.fs
│   └── FS.GG.Governance.ShipCommand.fsproj               #   + ProjectReference (same five)
└── (the four F25 cores + Provenance are CONSUMED VERBATIM — unchanged):
      FS.GG.Governance.CostBudget / CommandKind / CostBudgetJson / ProvenanceJson / Provenance

tests/
├── FS.GG.Governance.VerifyCommand.Tests/                 # EXTEND — fixture-budget invariant + verify.json
│                                                         #   byte-identity; tight-budget deferral; re-run
│                                                         #   determinism of both sidecars; standalone + missing-store;
│                                                         #   agent-reviewed-stays-advisory
└── FS.GG.Governance.ShipCommand.Tests/                   # EXTEND — same five at the (Profile, Gate) merge boundary;
                                                          #   audit.json byte-identity

surface/
├── FS.GG.Governance.VerifyCommand.surface.txt            # RE-BLESS — grown RunRequest/Effect/ArtifactKind/kindOf
├── FS.GG.Governance.ShipCommand.surface.txt              # RE-BLESS — same
└── (CostBudget/CommandKind/CostBudgetJson/ProvenanceJson baselines)  # UNCHANGED — consumed, not modified

Directory.Packages.props                                  # UNCHANGED — no new external/NuGet dependency
FS.GG.Governance.sln                                      # UNCHANGED — no new project
```

**Structure Decision**: Consume, don't create — and wire at the edge. No new library or pure core is added; the four
F25 surfaces are consumed at each host's existing MVU interpreter edge. The genuinely new code is small and edge-only:
a pure budget-filter demotion inside `executionPlan`, a pure `kindOf` map on `GatesExecuted`, two new `WriteArtifact`
sidecar effects (through the existing atomic writer), two normalized edge senses (environment/builder), and two new
`RunRequest` output paths. The byte-identity anchor (research.md D1) bounds the safety surface to a verifiable
fixture-budget invariant proven up front; deferral and the new sidecars are exercised by new fixtures, leaving every
existing golden untouched. The FR-010 boundary stays checkable: each host references the four F25 cores; no pure core
gains any filesystem/process dependency.

**Host parity and the verify/ship difference** (research.md D3). The two hosts share their MVU shape, store reader,
gate-execution port, atomic writer, and `GatesExecuted` record flow, so the wiring is the **same** in both — the only
difference is the budget's `(profile, mode)`: `verify` is fixed at `RunMode.Verify` (its `--mode` is rejected by
design, F056) with `--profile` (default `Standard`), so its budget is `budgetFor profile Verify`; `ship` threads
`--mode` (default `Gate`) and `--profile`, so its budget is `budgetFor profile mode` at the merge boundary. Both
yield the default `Medium` ceiling under `Standard`, which is exactly the invariant the byte-identity anchor rests on.

## Complexity Tracking

> No Constitution Check violations. **No new external dependency** (only ProjectReferences onto four already-built
> F25 libraries). The byte-identity-vs-budget reconciliation (research.md D1) is resolved by a verifiable
> fixture-budget invariant proven before wiring, not by any escape hatch; if a frozen golden fixture is found to hold
> an over-ceiling must-recompute gate, that is surfaced and escalated as a real behavioral change rather than
> absorbed. This section is intentionally empty.
