---
description: "Task list for F04 · 004-checktier-rule-bridge — the CheckTier arbitration model & the Rule bridge to the kernel"
---

# Tasks: CheckTier & Rule Bridge — Who Decides, and Reproducible Agent Reviews

**Input**: Design documents from `/specs/004-checktier-rule-bridge/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/CheckRule.fsi](./contracts/CheckRule.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a Tier 1 feature whose headline guarantees (the reified-ness
refusal, cache-key reproducibility/sensitivity, cache hit/miss + re-review-on-judge-change,
no-drift description, tier⟂severity orthogonality, totality) are only credible with real
evidence (Principle V). Per Principle I the semantic tests are written against the public
surface and FAIL before the matching `CheckRule.fs` body exists.

**Tier**: whole feature is **Tier 1** (plan Constitution Check). No per-task tier
annotations — every task matches; `[T1]`/`[T2]` omitted throughout.

**Elmish/MVU**: **N/A (functional core of it)** — `toRule` is pure: no I/O, no agent call,
no state machine. It emits a `RuleOutcome` (incl. `NeedsReview`, the review request) **as
data** — exactly the "I/O represented as data, interpreted only at the edge" half of
Principle IV; the `update`/effect interpreter that dispatches the review and records the
verdict is **F08**. No `Model`/`Msg`/`Effect`/interpreter-boundary tasks here. This is
recorded once in the evidence-obligations task (T018).

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (with rationale on the line).
  Never mark a failing task `[X]`; never weaken an assertion to green a build.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another *incomplete* task in this phase (parallel-safe hint).
- **[Story]**: `[US1]`..`[US4]`; unlabelled tasks are shared setup/foundational/polish.
- Exact file paths are given in every task.

> **File-coupling caveat.** The whole implementation lives in one file,
> `src/FS.GG.Governance.Kernel/CheckRule.fs` (the `rule`/`blocking`/`asking` constructors,
> `cacheKey`, and `toRule`), and all semantic tests live in one file,
> `tests/FS.GG.Governance.Kernel.Tests/CheckRuleTests.fs`. So tasks that touch the *same*
> one of those two files are **not** `[P]` with each other even when they belong to
> different stories — `[P]` is reserved here for genuinely different files (the FSI sketch,
> the two `.fsproj` edits, the surface baseline, the read-only hygiene check). Stories
> remain independently *testable*, but within `CheckRule.fs`/`CheckRuleTests.fs` the work is
> sequential to avoid edit conflicts.

> **Scenario numbering.** Test scenarios continue the kernel's running V-series. Quickstart
> §"Validation scenarios" lists V13–V20; this breakdown expands quickstart's combined V19/V20
> into per-story scenarios **V19–V22** so each user story is pinned independently (V19 US1
> Deterministic, V20 US1 HumanOnly, V21 US4 severity⟂tier, V22 polish totality). V13–V18 map
> one-to-one to the quickstart.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the new files into the build and exercise the contract in FSI first
(Principle I — the design pass happens before any `CheckRule.fs` body).

- [X] T001 [P] Copy the curated contract verbatim into the kernel as
  `src/FS.GG.Governance.Kernel/CheckRule.fsi` — it must match
  `specs/004-checktier-rule-bridge/contracts/CheckRule.fsi` byte-for-byte (quickstart
  done-when). Do not add a `CheckRule.fs` yet.
- [X] T002 Add `CheckRule.fsi` then `CheckRule.fs` to the `<Compile>` list in
  `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`, **after** `Check.fs` (and
  therefore after `Verdict.fs`/`Kernel.fs` — `CheckRule` depends on `Check` for the algebra
  it bridges, `Verdict` for the verdict a `RuleOutcome` carries, and `Kernel` for the
  `Rule<'fact>`/`FactSet`/`ProvenanceStep`/`RuleId`/`ArtifactRef` it emits into; plan
  Structure Decision / research D1). Create a minimal stub
  `src/FS.GG.Governance.Kernel/CheckRule.fs` (the types are in the `.fsi`; bodies are filled
  in later phases) so the project compiles.
- [X] T003 [P] Add `CheckRuleTests.fs` to the `<Compile>` list in
  `tests/FS.GG.Governance.Kernel.Tests/FS.GG.Governance.Kernel.Tests.fsproj`, **before**
  `Main.fs`. Create an empty `tests/FS.GG.Governance.Kernel.Tests/CheckRuleTests.fs`
  exposing an empty Expecto `testList "CheckRule"` so the test project compiles and Main can
  reference it.
- [X] T004 [P] Extend `scripts/prelude.fsx` with the short `CheckRule`/`toRule` FSI sketch
  from quickstart §"FSI sketch": define a tiny in-test adapter fact
  (`type Gov = GovOut of RuleOutcome | Art of ...`) and a **real** `Bridge<Gov>`
  (`Judge`/`ArtifactHash`/`Embed`/`Project`); reuse the F03 `contrast`/`tone` probes already
  in the prelude; author rules (`rule`/`asking`/`blocking`), compute a `cacheKey`, `toRule`
  it, and `Apply` it for a hit and a miss. This is the Principle-I design pass: if any shape
  is awkward, fix `CheckRule.fsi` (T001) **before** writing `CheckRule.fs` bodies.

**Checkpoint**: `dotnet build` is clean with the empty `CheckRule.fs`/`CheckRuleTests.fs`;
the FSI sketch type-checks against the contract.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the data types and the base authoring entry point that every story
depends on — including the FR-006 reified-ness guardrail, which "lives in the `rule`
constructor" and must ship *with* the constructors (US3 then pins it).

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [X] T005 Confirm the public types in `src/FS.GG.Governance.Kernel/CheckRule.fsi` compile
  and require no `.fs`-side definition beyond the `.fsi` (records/unions are declared in the
  signature): `CheckTier` (`Deterministic | AgentReviewed | HumanOnly`), `Severity`
  (`Advisory | Blocking`), `SpecSource` (`{ Document; Section }`), `JudgeId`
  (`{ ModelId; Version }`), `ReviewRequest` (`{ Rule; Question; Key }`), `RecordedReview`
  (`{ Rule; Key; Verdict }`), `RuleOutcome` (`Decided | NeedsReview | Reviewed |
  Escalated`), `CheckRule<'fact>` (`{ Id; Tier; Spec; Severity; Check; Question }`),
  `RuleRejection` (`OpaqueCannotBeDeterministic of RuleId`) and `Bridge<'fact>`
  (`{ Judge; ArtifactHash; Embed; Project }`) — per data-model.md §Entities. Adjust the stub
  `CheckRule.fs` so the assembly still builds.
- [X] T006 Implement the base `rule` smart constructor in
  `src/FS.GG.Governance.Kernel/CheckRule.fs` `module CheckRule`:
  `rule id tier spec check : Result<CheckRule<'fact>, RuleRejection>` sets
  `Severity = Advisory`, `Question = None`, and **refuses** the `Deterministic` tier when
  `not (Check.isReified check)`, returning `Error (OpaqueCannotBeDeterministic id)`; every
  other tier (and `Deterministic` over a reified check) returns
  `Ok { Id = id; Tier = tier; Spec = spec; Severity = Advisory; Check = check; Question =
  None }` (data-model.md §`rule` refusal table, FR-005/FR-006, research D3). No
  `private`/`internal`/`public` modifiers on any top-level binding (Principle II). This is
  the shared authoring entry every story uses; it lands the guardrail (US3 pins it at V13,
  T015). Verify against the T004 FSI sketch.

**Checkpoint**: types + the base authoring constructor (with the guardrail) in place;
`dotnet build` clean. Per-tier bridge work can now begin per story.

---

## Phase 3: User Story 1 — Bridge a tiered rule into the executable kernel (Priority: P1) 🎯 MVP

**Goal**: `toRule` turns an authored `CheckRule<'fact>` into the kernel's executable
`Rule<'fact>` whose `Description` is the rendered check (no drift). A `Deterministic` rule
asserts `Decided (id, Check.eval facts check)` verbatim (verdict never coerced); a
`HumanOnly` rule asserts `Escalated id` and never decides.

**Independent Test**: author a `Deterministic` rule over a reified check, `toRule` it, and
confirm `Apply` asserts the verdict that evaluating the check directly produces and that
`Description = Check.render check`; author a `HumanOnly` rule and confirm `Apply` asserts an
escalation/blocker and never a decided verdict.

### Tests for User Story 1 (write first; must FAIL before T009)

- [X] T007 [US1] In `tests/FS.GG.Governance.Kernel.Tests/CheckRuleTests.fs` add **V19**:
  build `Deterministic` rules over reified checks that `Check.eval` to pass / fail /
  uncertain, `toRule` each, and assert `Apply facts` emits exactly
  `[ embed (Decided (id, v)) ]` where `v = Check.eval facts check` **verbatim** — `Uncertain`
  is never coerced to pass/fail; **and** assert `(toRule bridge r).Description =
  Check.render r.Check` for every rule (US1 AS1/2, FR-007/008, SC-005/006, INV-7/INV-8).
- [X] T008 [US1] In `CheckRuleTests.fs` add **V20**: a `HumanOnly` rule, `toRule`-ed,
  `Apply`s to `[ embed (Escalated id) ]` — it asserts an escalation/blocker and **never** a
  `Decided` verdict (US1 AS3, FR-010, INV-9; severity-independence of `HumanOnly` is pinned
  further in US4/V21). Depends on T007 (same file).

### Implementation for User Story 1

- [X] T009 [US1] Implement `toRule bridge rule : Rule<'fact>` in
  `src/FS.GG.Governance.Kernel/CheckRule.fs`: return
  `{ Id = rule.Id; Description = Check.render rule.Check; Apply = fun facts -> … }`. Add the
  private `emit`/`embed` helper `embed inputs outcome` that builds a `FactAssertion<'fact>` =
  `{ Id = <placeholder, overridden by FixedPoint identify>; Value = bridge.Embed outcome;
  Provenance = [ { Rule = rule.Id; Inputs = inputs; Note = Check.render rule.Check } ] }`
  (data-model.md §`toRule`, research D5). **`Inputs` rule** (data-model §"Provenance
  `Inputs`"): the domain-neutral `Bridge` cannot resolve a read `ArtifactRef` to a `FactId`
  (`ArtifactHash` yields a content-hash string; `Project` only `RuleOutcome`s — recovering
  ids would breach FR-015), so pass `Inputs = []` for `Deterministic`/`HumanOnly` (the
  `AgentReviewed`-hit case passes the recorded review's `FactId`, wired in T014). Implement
  the branches `Deterministic → [ emit [] (Decided (rule.Id, Check.eval facts rule.Check)) ]`
  (verdict not coerced) and `HumanOnly → [ emit [] (Escalated rule.Id) ]` (regardless of
  severity). **The `AgentReviewed` branch is completed in Phase 4 (T014)**; to keep `toRule`
  total before then, have it emit `[ emit [] (NeedsReview { Rule = rule.Id; Question =
  rule.Question; Key = "" }) ]` as a placeholder. ⚠️ The placeholder `Key = ""` MUST be gone
  after T014, and no `AgentReviewed` assertion is exercised before T014 (US1 AS4 defers the
  cache behaviour to US2, so V19/V20 cover only `Deterministic`/`HumanOnly`). No visibility
  modifiers on top-level bindings. Makes T007–T008 pass.

**Checkpoint**: MVP — a `Deterministic`/`HumanOnly` `CheckRule` bridges into the kernel and
runs; the bridged description is the rendered check. STOP and validate V19–V20 green
independently.

---

## Phase 4: User Story 2 — Reproducible, cacheable agent reviews (Priority: P1)

**Goal**: `cacheKey` is a pure, reproducible, input-sensitive content hash over the judge
identity + check hash + read-artifact hashes + reviewer prompt (decision #1); the
`AgentReviewed` branch of `toRule` uses it to reuse a recorded verdict on a hit (no agent
call) and emit exactly one `NeedsReview` on a miss, with re-review-on-judge-change falling
out for free.

**Independent Test**: compute `cacheKey` over fixed ingredients twice → identical; vary each
of {model id, version, check hash, an artifact hash, prompt} → key changes; permute/duplicate
the artifact hashes → key unchanged. `Apply` an `AgentReviewed` rule with a matching
`RecordedReview` present → `Decided`, zero `NeedsReview`; absent → exactly one `NeedsReview`
carrying the key; change the judge → the old recorded verdict misses → fresh `NeedsReview`.

### Tests for User Story 2 (write first; must FAIL before T013–T014)

- [X] T010 [US2] In `CheckRuleTests.fs` add **V14** (FsCheck property, Expecto.FsCheck):
  `cacheKey judge checkHash artifactHashes question` over fixed ingredients equals itself;
  and changing **any one** of {`judge.ModelId`, `judge.Version`, `checkHash`, one element of
  `artifactHashes`, `question`} yields a **different** key (US2 AS1/2, FR-011/012, SC-002,
  INV-2). Depends on prior `CheckRuleTests.fs` tasks (same file).
- [X] T011 [US2] In `CheckRuleTests.fs` add **V15**: `cacheKey` with a permuted and/or
  duplicated `artifactHashes` list yields the **same** key (de-duplication + ordinal sort —
  the artifact half is order- and duplicate-independent; spec "Empty read set" edge,
  FR-012, INV-3). Also assert an **empty** `artifactHashes` still produces a stable key that
  still varies with the check hash and judge identity. Depends on T010 (same file).
- [X] T012 [US2] In `CheckRuleTests.fs` add **V16/V17/V18**: with a real `Bridge` over the
  in-test adapter `'fact` — **V16 (hit)** a `RecordedReview` whose `Key` matches the rule's
  computed key is present in the facts ⇒ `Apply` emits `Decided (id, recorded verdict)` and
  **zero** `NeedsReview` (no agent call), and the emitted fact's provenance `Inputs` carries
  that `RecordedReview`'s `FactId` (US2 AS3, FR-009, SC-003, INV-4); **V17 (miss)** no
  matching `RecordedReview` ⇒ `Apply` emits **exactly one** `NeedsReview` carrying the key
  (US2 AS4, FR-009, SC-003, INV-5); **V18 (re-review)** a `RecordedReview` recorded under an
  old `JudgeId`/prompt no longer matches once the judge model/version or the rule's
  `Question` changes ⇒ a fresh `NeedsReview` (US2 AS5, FR-013, SC-004, INV-6). Depends on
  T011 (same file).

### Implementation for User Story 2

- [X] T013 [US2] Implement
  `cacheKey judge checkHash artifactHashes question : string` in
  `src/FS.GG.Governance.Kernel/CheckRule.fs` as a pure SHA-256 hex digest over a
  **prefix-free** pre-image (each component hashed to fixed-width hex first, then
  concatenated — the same discipline F03's `hash` uses), combining in fixed order:
  `judge.ModelId`, `judge.Version`, `checkHash`, `artifactHashes` **de-duplicated and
  ordinal-sorted** (`String.CompareOrdinal`), and the reviewer-prompt hash of `question`
  (SHA-256 of the string, or a fixed sentinel for `None`). Reuse
  `System.Security.Cryptography.SHA256` + `System.Text.Encoding.UTF8` (BCL, zero new deps —
  the same hash F03 uses; data-model.md §`cacheKey`, research D4). Makes T010–T011 pass.
- [X] T014 [US2] Complete the `AgentReviewed` branch of `toRule` in
  `src/FS.GG.Governance.Kernel/CheckRule.fs` (extending the T009 skeleton, **replacing its
  placeholder key**): compute
  `key = cacheKey bridge.Judge (Check.hash rule.Check) (Check.reads rule.Check |> List.map
  (bridge.ArtifactHash facts)) rule.Question`; look up the recorded verdict **and its fact
  id together** with
  `facts |> List.tryPick (fun f -> match bridge.Project f.Value with Some (Reviewed r) when
  r.Key = key -> Some (f.Id, r.Verdict) | _ -> None)`; on `Some (fid, v)` (**hit**) emit
  `[ emit [fid] (Decided (rule.Id, v)) ]` — recording the consumed `RecordedReview`'s
  `FactId` as the provenance `Inputs` — and **no** request/agent call; on `None` (**miss**)
  emit `[ emit [] (NeedsReview { Rule = rule.Id; Question = rule.Question; Key = key }) ]`
  (exactly one) (data-model.md §`toRule`, research D5, FR-009/FR-013). Makes T012 pass.

**Checkpoint**: US1 + US2 work independently; agent reviews are reproducible and cache
hit/miss + re-review-on-judge-change are pinned. Both P1 stories now deliver the keystone —
a check has a home and a stochastic judge sits inside a reproducible pipeline.

---

## Phase 5: User Story 3 — The reified-ness guardrail on the Deterministic tier (Priority: P2)

**Goal**: An `Opaque`-containing check can never be authored as `Deterministic` — opacity
is a typed, enforced refusal, not a silent leak. (The guardrail itself ships in the `rule`
constructor at T006; this story **pins** it.)

**Independent Test**: authoring a `Deterministic` rule over an opaque check is refused;
authoring the same opaque check as `AgentReviewed`/`HumanOnly` is accepted; authoring a
`Deterministic` rule over a fully reified check is accepted.

### Tests for User Story 3 (write first; the guardrail impl is T006)

- [X] T015 [US3] In `CheckRuleTests.fs` add **V13**: with an `Opaque`-containing check,
  `rule id Deterministic spec opaqueCheck = Error (OpaqueCannotBeDeterministic id)`; the
  same opaque check at `AgentReviewed` and at `HumanOnly` each return `Ok`; and a fully
  reified check at `Deterministic` returns `Ok` — i.e.
  `Error (OpaqueCannotBeDeterministic id)` **iff** the tier is `Deterministic` and
  `not (Check.isReified check)` (US3 AS1–3, FR-006, SC-001, INV-1). Depends on prior
  `CheckRuleTests.fs` tasks (same file).

> **No separate implementation task.** The refusal lives in the `rule` constructor (T006,
> foundational) so it ships *with* the constructors per the plan; T015 is the headline
> correctness test that pins SC-001.

**Checkpoint**: US1–US3 work independently; an irreducible judgement can never masquerade
as a reproducible machine decision.

---

## Phase 6: User Story 4 — Severity and spec provenance, orthogonal to tier (Priority: P3)

**Goal**: Severity (advisory/blocking) and the spec source are recorded independently of who
decides a rule. `blocking` promotes severity without touching the tier; `asking` attaches a
reviewer prompt and the `AgentReviewed` tier; severity defaults to advisory; `HumanOnly`
escalates regardless of severity; the `SpecSource` travels with the rule for provenance.

**Independent Test**: author two rules with the same tier/check but different severities and
confirm severity is recorded and independent of tier; an undeclared severity defaults to
advisory and `blocking` makes it blocking with the tier unchanged; a `HumanOnly` rule
escalates whether advisory or blocking; a rule's `SpecSource` is recoverable after `toRule`.

### Tests for User Story 4 (write first; must FAIL before T017)

- [X] T016 [US4] In `CheckRuleTests.fs` add **V21**: an undeclared severity is `Advisory`
  (the `rule` default); `blocking r` sets `Severity = Blocking` and leaves `r.Tier`
  unchanged; for every tier×severity combination both are recorded independently (severity
  does not constrain tier, tier does not constrain severity); a `HumanOnly` rule `Apply`s to
  `Escalated id` whether its severity is `Advisory` or `Blocking` (severity-independence of
  escalation); and a rule's `Spec` (`SpecSource`) is recoverable from the authored
  `CheckRule` after `toRule` (US4 AS1–4, FR-002/003/010, SC-008, INV-9). Depends on prior
  `CheckRuleTests.fs` tasks (same file).

### Implementation for User Story 4

- [X] T017 [US4] Implement the post-construction modifiers in
  `src/FS.GG.Governance.Kernel/CheckRule.fs`: `blocking rule : CheckRule<'fact>` sets
  `Severity = Blocking` (tier unchanged); `asking prompt rule : CheckRule<'fact>` sets
  `Tier = AgentReviewed` and `Question = Some prompt` (so it accepts any check and never
  trips FR-006) — both composing under `Result.map` (data-model.md §`blocking`/`asking`,
  research D3, FR-005). No visibility modifiers on top-level bindings. Makes T016 pass.

**Checkpoint**: all four stories independently testable; the full `CheckRule` surface
(`rule`/`blocking`/`asking`/`cacheKey`/`toRule` + the ten types) is one coherent contract.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: totality, surface discipline, dependency hygiene, and the feature exit gate.

- [X] T018 [US-all] In `CheckRuleTests.fs` add **V22** (totality): `toRule` + `Apply` over
  every tier, checks including **empty `All`/`Any`** combinators, an **empty fact set**, and
  an **unknown artifact** (`bridge.ArtifactHash` returns its sentinel) — none throws or
  returns a partial result (FR-017, SC-007, INV-10). Also assert the FR-015 boundary: the
  artifact hashes come **only** from `bridge.ArtifactHash` over the supplied facts and the
  `AgentReviewed` miss emits a `NeedsReview` value (no agent call, no I/O) (INV-11). Record
  here the **evidence-obligations note**: F04 is the pure functional core, so Principle IV
  (Elmish/MVU) is **N/A** (the dispatching `update`/interpreter is F08); all evidence is
  **real** — real `CheckRule`/`Bridge` values and a real in-test adapter `'fact` (exactly
  the shape F09 will materialise) — no synthetic fixtures, no `// SYNTHETIC:` disclosures.
- [X] T019 Re-bless the API surface baseline:
  `surface/FS.GG.Governance.Kernel.surface.txt` must grow to include the F04 types
  (`CheckTier`, `Severity`, `SpecSource`, `JudgeId`, `ReviewRequest`, `RecordedReview`,
  `RuleOutcome`, `CheckRule`, `RuleRejection`, `Bridge`) and the `CheckRule` module
  (`rule`/`blocking`/`asking`/`cacheKey`/`toRule`). Run `BLESS_SURFACE=1 dotnet test`,
  confirm the diff is **exactly** the F04 additions, and commit it (FR-018, plan
  Principle II). While reviewing the diff, **confirm the FR-016 "no domain vocabulary" half**:
  the added names carry no software/design/workflow domain vocabulary (this manual review is
  the vocab half of FR-016; the dependency/I-O half is tested mechanically by V12 / T020).
  The existing V11 surface-drift test then guards the `CheckRule` surface for free.
- [X] T020 [P] Confirm the existing **V12 dependency-hygiene** test still passes — the kernel
  assembly references only the BCL + FSharp.Core after `CheckRule.*` is added (SHA-256 is
  `System.Security.Cryptography`, already allowed; SC-009). No `<PackageReference>` was added
  to `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`.
- [X] T021 Run the quickstart done-when gate end-to-end: `dotnet build` clean; `dotnet test`
  green (V13–V22 + inherited V11 surface-drift + V12 dependency-hygiene); confirm
  `src/FS.GG.Governance.Kernel/CheckRule.fsi` still matches
  `specs/004-checktier-rule-bridge/contracts/CheckRule.fsi` and `CheckRule.fs` carries no
  `private`/`internal`/`public` on top-level bindings; confirm no packing was added (the
  kernel still packs at F06). Walk the FSI sketch (T004) once more to confirm SC-007 (a
  newcomer authors, keys, bridges, and runs a rule through the public surface alone).
  **Locks decision #1** (the cache key includes the judge identity, with the
  re-review-on-judge-change policy); **notes decision #2** (single-sample judge noise /
  aggregation) for F08.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Phase 1 — BLOCKS all stories.
- **User stories (Phases 3–6)**: each depends on Foundational. They are independently
  *testable*, but because the whole implementation shares `CheckRule.fs` and every test
  shares `CheckRuleTests.fs`, implement them in **priority order**
  (US1 → US2 → US3 → US4) to avoid same-file edit conflicts rather than truly in parallel.
- **Polish (Phase 7)**: depends on all desired stories; T019 (surface re-bless) must run
  after the public surface is final (after T017).

### Cross-story / cross-task dependencies

- **T014 (US2) extends T009 (US1)** — `toRule` is one function; US1 lands its
  `Deterministic`/`HumanOnly` branches + the `emit` helper with a *placeholder* key in the
  `AgentReviewed` branch, and US2 replaces that placeholder with the real `cacheKey`-driven
  hit/miss logic. This is a genuine logical dependency, not just same-file ordering.
- **T015 (US3 / V13) is pinned by the guardrail in T006 (foundational)** — no separate impl
  task; the refusal ships with the `rule` constructor.
- **T016 (US4 / V21) exercises `blocking` from T017** and the `Escalated` branch from T009.
- Same-file (`CheckRuleTests.fs`) ordering only: T008→T007; T011→T010, T012→T011; T015, T016,
  T018 follow the earlier test tasks in the file. These are edit-conflict ordering, not
  logical coupling.

### Parallel opportunities

- **Phase 1**: T001, T003, T004 are `[P]` (different files); T002 follows T001.
- **Phase 7**: T020 is `[P]` (read-only hygiene check) alongside T019 (surface baseline);
  T018 edits `CheckRuleTests.fs`.
- Across stories, true parallelism is limited by the two shared files — see the
  file-coupling caveat at the top. Different *people* could draft one story's test block
  while another implements an earlier story's code, but commits must serialize on
  `CheckRule.fs`/`CheckRuleTests.fs`.

---

## Task count & MVP

- **Setup**: 4 (T001–T004)
- **Foundational**: 2 (T005–T006) — types + the base `rule` constructor (with the guardrail)
- **US1 (P1, MVP)**: 3 — V19 + V20 tests + `toRule` (Deterministic/HumanOnly) (T007–T009)
- **US2 (P1)**: 5 — V14/V15/V16-18 tests + `cacheKey` + `toRule` AgentReviewed (T010–T014)
- **US3 (P2)**: 1 — V13 test pinning the foundational guardrail (T015)
- **US4 (P3)**: 2 — V21 test + `blocking`/`asking` (T016–T017)
- **Polish**: 4 (T018–T021)
- **Total**: 21 tasks.

**Suggested MVP scope**: Phases 1–3 (Setup + Foundational + **User Story 1**) — a
`Deterministic`/`HumanOnly` `CheckRule` bridges into the kernel and runs, with a
non-drifting description. Both P1 stories together (US1 bridge + US2 reproducible agent
reviews) deliver the keystone — a check gets a *home* (who decides it) and a stochastic
judge sits inside an otherwise reproducible pipeline; US3 (the safety guardrail) and US4
(severity/provenance) layer correctness and routing-facing data on top.
