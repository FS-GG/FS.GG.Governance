# Phase 0 Research — Cost-Cache Host Wiring (F25 wiring)

This row wires four already-built F25 cores into the two mature hosts `fsgg verify`
(`FS.GG.Governance.VerifyCommand`) and `fsgg ship` (`FS.GG.Governance.ShipCommand`). The decisions below are grounded
in a direct reconnaissance of both hosts and the four cores. Nothing here changes a pure core.

## Host & core reconnaissance (ground truth)

**Both hosts share their MVU shape.** `VerifyCommand` and `ShipCommand` carry near-identical `Model`/`Msg`/`Effect`:

- `Msg` already includes `GatesExecuted of (GateId * CommandRecord) list` — **the F032 `CommandRecord`s already flow
  back to `update`.**
- `Effect` already includes `ExecuteGates of (GateId * GateCommand) list`, `WriteArtifact of kind: ArtifactKind *
  path: string * content: string`, `LoadStore of path`, `PersistStore`, `SenseFreshness`.
- `executionPlan (model)` already calls `CacheEligibility.evaluate candidates store` and classifies each selected gate
  `ToExecute of GateCommand | ToReuse of ExitCode | NoCommand`. `tryExecute` projects the `ToExecute` gates into
  `ExecuteGates`. **The per-gate cache verdict the budget needs already exists here.**
- The interpreter executes gates via `GateExecution.Interpreter.senseExecution ports.Execute command`, reads the
  store via `FreshnessSensing.loadStore ports.Store path` (absent → empty; malformed → degrade + currency note), and
  writes artifacts atomically (temp-file + rename) under `ArtifactKind`.
- `verify` writes `verify.json` (`VerifyJson.ofVerifyDecision`, `fsgg.verify/v1`) at `readiness/verify.json`; `ship`
  writes `audit.json` (`AuditJson.ofShipDecision`, `fsgg.audit/v2`) at `readiness/audit.json`. Both already embed the
  cache report and the per-gate outcomes; the `--json` stdout is byte-identical to the persisted file.

**The four F25 cores' entry points** (consumed verbatim):

- `CostBudget.Budget`: `budgetFor: Profile -> RunMode -> CostBudget`, `fits: CostBudget -> Cost -> bool`,
  `decide: CostBudget -> RunMode -> CandidateCost list -> CacheDecisionReport`, plus
  `recomputeGates`/`reuseGates`/`overBudget`/`entries`.
- `CostBudget.Findings`: `cacheFindings: CacheDecisionReport -> (GateId -> EvidenceTaint) -> CostFinding list`,
  `enforce: RunMode -> Profile -> CostFinding -> EnforcementDecision`, `kindToken`.
- `CommandKind.Audit`: `auditSnapshot: Revision -> Revision -> Revision -> RuleHash -> GeneratorVersion ->
  ArtifactHash list -> KindedCommandRun list -> EnvironmentClass -> BuilderIdentity -> AuditSnapshot`,
  `runIdentity`/`snapshotIdentity` (identity is `CommandRecord.canonicalId` / `Provenance.canonicalId` verbatim —
  kind and duration do **not** participate).
- `CostBudgetJson`: `schemaVersion = "fsgg.cost-budget/v1"`, `ofReport: CacheDecisionReport -> CostFinding list ->
  string` (decisions + findings always present; empty arrays when none).
- `ProvenanceJson`: `schemaVersion = "fsgg.provenance/v1"`, `ofSnapshot: AuditSnapshot -> string` (durationNanos is
  sensed metadata only, never in the identity field).

**Input types the host constructs:** `CandidateCost { Gate; Cost; Verdict: CacheEligibilityVerdict; Review:
AgentReviewMark }`, the `(GateId -> EvidenceTaint)` taint lookup, and `KindedCommandRun { Kind: CommandKind; Record:
CommandRecord }`. All other inputs (`Profile`, `RunMode`, `Cost`, `Revision`, `RuleHash`, `GeneratorVersion`,
`ArtifactHash`, `EnvironmentClass`, `BuilderIdentity`) come from libraries the hosts already reference, except
`FS.GG.Governance.Provenance` (F033), added for `BuilderIdentity`/`Provenance`.

---

## D1 — The central reconciliation: byte-identity vs. budget filtering

**Decision.** The budget filter genuinely changes which gates execute (FR-001), **and** every existing golden stays
byte-identical (FR-007/SC-004), because the **default budget admits the existing fixtures**. The reconnaissance pins
the default ceiling exactly: `profileCeiling Standard = Medium`, `modeCeiling Verify = modeCeiling Gate = High`, so
`budgetFor Standard Verify = budgetFor Standard Gate = min(Medium, High) = Medium`. An existing golden therefore
stays byte-identical **iff its fixture's must-recompute gates all fit the `Medium` ceiling** (`Cheap`/`Medium`).
Deferral — the new behavior — is exercised only by **new** fixtures that introduce a `High`/`Exhaustive`
must-recompute gate under a tight budget (a `Light` profile and/or inner mode flooring to `Cheap`), which produce the
**new** sidecars.

**Rationale.** This is the only honest way both requirements hold at once: budget filtering is a real behavioral
change, so it must be invisible to existing goldens precisely where those goldens don't trip the budget, and visible
(in new fixtures + sidecars) where they do. Re-blessing an existing golden to absorb a newly-deferred gate would
violate SC-004 and hide a behavioral change.

**Safety obligation (a first-class task).** Before wiring, **audit every frozen `verify.json` / `audit.json` / ship
golden fixture** and prove its must-recompute gates fit the default `Medium` ceiling (a `fixture-budget invariant`
test). If any fixture holds an over-ceiling must-recompute gate, surface it as a real behavioral change and escalate
— do not paper over it. After wiring, the existing goldens are compared byte-for-byte against frozen pre-wiring
baselines.

**Alternatives rejected.** (a) Make the budget filter opt-in behind a flag so existing behavior is untouched —
rejected: FR-001/FR-002 make budget filtering the default behavior of `verify`/`ship`, not an opt-in. (b) Record
deferrals as advisory observers without changing the executed set — rejected: contradicts FR-001's "execute **only**
the must-recompute gates that fit." (c) Regenerate the existing goldens under a permissive `Release` budget —
rejected: that re-blesses contracts SC-004 freezes.

---

## D2 — Kinded-run recording: a pure `kindOf` map over records already returned

**Decision.** On `GatesExecuted records`, pair each executed gate's `CommandRecord` with `kindOf gate : CommandKind`
into a `KindedCommandRun { Kind; Record }`. `kindOf : Gate -> CommandKind` is a total pure map from the gate's
declared command category to one of the seven kinds (`Build | Test | Pack | TemplateInstantiation | GitDiff |
PackageInspection | VisualCapture`). The kinded runs feed `auditSnapshot`.

**Rationale.** The `CommandRecord`s already flow back through `GatesExecuted` (`senseExecution` assembles a real
record per executed gate); recording is therefore a pure relabel, not new execution I/O. `runIdentity` is
`CommandRecord.canonicalId` verbatim, so two runs differing only in sensed duration share an identity (FR-004) — no
host work needed to guarantee duration-invariance.

**Open detail for data-model.** `kindOf`'s source: prefer a category already present on the `Gate` (or its
`GateCommand`); if no such field exists, a small explicit host-side match over the gate's command/id, disclosed at
the use site. Either way the map is total (no wildcard fallthrough that silently mislabels).

**Alternatives rejected.** Inferring the kind from the executable string at the JSON layer — rejected: kind belongs
to the gate's declaration, not to a fragile argv heuristic, and inference would not be deterministic across hosts.

---

## D3 — Where the budget filter inserts; host parity

**Decision.** The budget filter is a pure step layered into `executionPlan`/`tryExecute`. After `CacheEligibility.evaluate`
produces the per-gate verdicts, build a `CandidateCost` per selected gate (`Cost` from `Config`, `Verdict` from the
evaluation, `Review` from the gate's agent-review mark), run `decide (budgetFor profile mode) mode candidates`, and
use the `CacheDecisionReport` to **demote** any `OverBudget` gate out of the `ToExecute` set (it becomes
deferred/skipped, never executed, never in `Passing`). The wiring is identical in both hosts; only the budget's
`(profile, mode)` differs: `verify` uses `budgetFor profile Verify` (its `--mode` is rejected by design, F056);
`ship` uses `budgetFor profile mode` at the merge boundary (`--mode` default `Gate`).

**Rationale.** `executionPlan` is the single chokepoint where the executed set is decided and where the cache verdict
already lives, so the demotion is local and the rest of the pipeline (capture, outcomes, rollup) is unchanged for the
gates that still run. A demoted gate never reaches `applyExecution`'s passed set, so it is structurally incapable of
being reported as passed (SC-002).

**Alternatives rejected.** Filtering after execution (run then discard) — rejected: wastes the expensive recompute
the budget exists to prevent and contradicts FR-003 ("charges nothing"). Filtering in the interpreter — rejected:
that is I/O-side; the decision is pure and belongs in `update`.

---

## D4 — Provenance inputs: reuse sensed facts; normalize the two new senses

**Decision.** Feed `auditSnapshot` from already-sensed facts wherever possible: Base/Head `Revision` from the
`RepoSnapshot`; `SourceCommit = Head`; `RuleHash`/`GeneratorVersion`/`ArtifactDigests` from `SensedFacts` (F046); the
kinded runs from D2. Only `EnvironmentClass` and `BuilderIdentity` are genuinely new edge senses; both are sensed at
the interpreter edge and **normalized** — no username, hostname, absolute path, or wall-clock — so `provenance.json`
is byte-identical across machines and re-runs (FR-006, SC-003). `EnvironmentClass` is the existing `Config` DU
(`Local | Ci | LocalOrCi | Release`); `BuilderIdentity` is a normalized tool/CI identity string.

**Rationale.** `ofSnapshot` is deterministic by construction (durationNanos is metadata only), so determinism rests
entirely on the inputs being normalized. Reusing the sensed facts avoids a second sensing pass and keeps the snapshot
consistent with the verdict the host already computed.

**Alternatives rejected.** Sensing a raw `whoami`/hostname/timestamp builder identity — rejected: it would make
`provenance.json` machine- and clock-dependent, breaking SC-003.

---

## D5 — Two sidecars via two new `WriteArtifact` effects

**Decision.** Add two `ArtifactKind` cases (e.g. `CostBudgetArtifact`, `ProvenanceArtifact`) and emit two new
`WriteArtifact(kind, path, content)` effects from `update`, with `content = CostBudgetJson.ofReport report findings`
and `content = ProvenanceJson.ofSnapshot snapshot`. They are written through the host's **existing** atomic writer
(temp-file + rename) to default paths `readiness/cost-budget.json` and `readiness/provenance.json`, configurable via
two new `RunRequest` fields. No existing write path is touched.

**Rationale.** Reusing the existing `WriteArtifact` port keeps the new I/O on the audited edge (FR-010) and inherits
the atomic-write durability the existing goldens already rely on. Two new `ArtifactKind` cases keep each write
self-describing for the interpreter and the tests.

**Alternatives rejected.** A bespoke sidecar writer — rejected: needless new port; the existing `WriteArtifact` is
exactly shaped for it. Folding the sidecars into the existing artifact write — rejected: they are separate contracts
and must be independently determinism-tested.

---

## D6 — Findings fold: advisory-only, in the sidecar, not in the existing goldens

**Decision.** Run `Findings.cacheFindings report taint` to derive the advisory cost/cache findings and
`Findings.enforce mode profile` to confirm each is advisory; project them into `cost-budget.json` via
`ofReport report findings`. **Do not** append them to the `ShipDecision` that `audit.json`/`verify.json` project.

**Rationale.** This is what makes FR-008 ("fold through the existing enforcement machinery as advisory") and
FR-007/SC-004 (existing goldens byte-identical) both true: the findings pass *through* the same enforcement severity
machinery (proving they never block), but they land in the new sidecar, not in the existing report object. Appending
them to the `ShipDecision` would change `audit.json`/`verify.json` bytes and break SC-004. No new verdict, no new
exit-code, no truth-table change.

**Alternatives rejected.** Emitting the findings into the existing `Findings` section of `audit.json`/`verify.json` —
rejected: breaks byte-identity. Skipping `enforce` entirely — rejected: the spec requires the findings to be proven
advisory through the existing machinery, not merely assumed.

---

## D7 — Agent-reviewed checks stay advisory

**Decision.** Each `CandidateCost` carries an `AgentReviewMark` (`Deterministic | AgentReviewed CacheKey`). An
agent-reviewed gate's `decide` outcome (reuse/recompute/over-budget) is recorded like any other, but it is **never**
promoted to a blocker by this wiring, and agent-reviewed evidence reuses only on a matching `CacheKey`. The advisory
status is preserved by D6 (findings never block) and by the fact that the wiring touches no verdict.

**Rationale.** FR-009/SC-006: the cache decision must not change an agent-reviewed check's advisory verdict under any
profile/mode. Carrying the mark through `decide` keeps the reuse-identity correct (re-review on a changed
judge/prompt/check-artifact identity) without touching enforcement.

**Alternatives rejected.** Dropping the `AgentReviewMark` (treating every gate as deterministic) — rejected: would
let agent-reviewed evidence reuse on the wrong identity, contradicting FR-009.

---

## D8 — Standalone path and safe failure

**Decision.** The budget/cache/provenance path draws only on the host's existing product-local store reader and
sensed facts — there is no monorepo-only path, so a standalone-checked-out product is cost-budgeted from its own
evidence, runs, and provenance (FR-011). A missing/unreadable evidence store keeps the host's existing
degrade-to-empty-with-currency-note behavior; with no recorded evidence everything is `MustRecompute`/`NoEvidence`
(no fabricated reuse), and both sidecars are still well-formed with empty arrays. An absent provenance input names the
offending source as input-not-defect (FR-012, Constitution VI).

**Rationale.** The host already reads only product-local sources and already distinguishes input from defect on store
load; the wiring inherits both properties and must not introduce a monorepo dependency or a swallowed error.

**Alternatives rejected.** Failing hard on a missing store — rejected: contradicts the existing degrade-with-note
contract and FR-012's "no swallowed error and no fabricated reuse" (which is degrade, not crash).

---

## Resolved unknowns

- **Budget `(profile, mode)` per host** → `verify`: `budgetFor profile Verify`; `ship`: `budgetFor profile mode`
  (default `Gate`). Default `Standard` ⇒ `Medium` ceiling (D1, D3).
- **Where the filter inserts** → `executionPlan`/`tryExecute`, demoting `OverBudget` from `ToExecute` (D3).
- **Kinded-run source** → `CommandRecord`s already in `GatesExecuted`, relabeled by pure `kindOf` (D2).
- **Provenance inputs** → sensed facts + snapshot revisions; only normalized environment/builder are new (D4).
- **Sidecar mechanism** → two new `ArtifactKind`/`WriteArtifact` through the existing atomic writer (D5).
- **Findings placement** → advisory through `enforce`, into the sidecar only, never into existing goldens (D6).
- **Byte-identity** → guaranteed by the fixture-budget invariant proven up front (D1).

No NEEDS CLARIFICATION remain.
