# Phase 0 — Research (F10 · 010-adapter-speckit)

Engineering decisions for the Spec Kit adapter — the first concrete production adapter and domain #1 of
Milestone M3 (the adoption bar). Each resolves a design question the spec deferred to "the plan and the
curated `.fsi`" (spec Assumptions): the precise `SpecKitFact`/`Phase`/`SpecKitArtifact` cases, the
`whenPhase`/`mergeFence` signatures, the rule ids, and the `Bridge` wiring. No NEEDS CLARIFICATION
remained in the Technical Context; these record *why* the chosen shapes. The footing is
`docs/governance-design/speckit-in-the-system.md` (the fixed design) and the F09 SPI it is authored
against (`docs/governance-design/adapters.md`).

---

## D1 — A new `FS.GG.Governance.Adapters.SpecKit` project, pure, depending on the SPI

- **Decision**: Ship F10 as a **new pure library** `src/FS.GG.Governance.Adapters.SpecKit/` (namespace
  `FS.GG.Governance.Adapters.SpecKit`) with a single `ProjectReference` on
  `FS.GG.Governance.Adapters.Spi` and **zero `PackageReference`**. The kernel and the SPI gain **no**
  reference to it.
- **Rationale**: FR-016 demands "a new component separate from the kernel and the SPI, depend on the SPI
  (and through it the kernel) without either depending on it, light footprint (BCL + `FSharp.Core` + SPI
  + kernel only)." The roadmap (§3) names exactly this project: `FS.GG.Governance.Adapters.SpecKit … pure;
  depends on Spi (F10)`. A concrete adapter is definitionally an *adopter* of the SPI, so it takes the SPI
  reference (not the kernel directly) — that is what lets F12 compose F10 and F11 at one root over the
  SPI's `Composition` machinery, and preserves the dependency direction (governance may inspect a project;
  a project must never require governance).
- **Alternatives considered**:
  - *Reference the kernel directly (skip the SPI).* Rejected: the adapter must be a `Adapter<…>` SPI
    record and must lift via `Lift`/`Composition`, which live in the SPI. Referencing only the kernel
    would re-implement the SPI's adoption contract.
  - *Add the adapter to the Spi test project (where the toy examples live).* Rejected: F10 is a real,
    shipped adapter with its own surface baseline and dependency hygiene — it belongs in `src/`, not in
    another project's tests.

## D2 — The adapter supplies exactly the five components + the F04 `Bridge`; nothing cross-cutting

- **Decision**: Author the Spec Kit adapter as a single `Adapter<SpecKitFact, SpecKitArtifact,
  SpecKitChange>` value (`Catalog.adapter judge dial`) supplying the **five** SPI components — the closed
  `SpecKitFact` union (named by `SpecKit.identify`), the `SpecKit.toRef` artifact mapping, the declared
  `SpecKit.probes`, the `Catalog.catalog` rule list (dial-promoted), and the `Catalog.fences dial` — plus
  the F04 `SpecKit.bridge judge` kernel wiring. The adapter module contains **no** inference, arbitration,
  evidence, rendering, hashing, explanation, severity, or routing code, and **no** artifact-authoring
  operation.
- **Rationale**: FR-003 requires "exactly the five SPI components and nothing more; everything
  cross-cutting reused from the kernel," and FR-004 requires the adapter to be an **observer, not an
  author** (the anti-goal is structural). The F09 `Adapter` record makes the five-part contract *total*
  (a missing component does not compile), and the absence of any `System.IO`/serializer/`Model`/`Msg` in
  the `.fsi` makes the observer-only and purity properties checkable by inspection (SC-001). The
  `SpecKitGov of RuleOutcome` case is the kernel wiring the `Bridge.Embed`/`Project` need — two one-liners,
  not a cross-cutting facility (mirrors F09's example `DocGov`/`TaskGov`).
- **Alternatives considered**:
  - *Expose a richer adapter that also formats explanations / computes routes.* Rejected: those are kernel
    facilities (F03 `Check.render`/`explain`, F07 `Route`); re-exposing them would violate FR-003's "reuses
    100%" and bloat the surface. The adapter supplies values; the kernel interprets them.
  - *Fold the judge identity into a constant.* Rejected (see D8): the judge is per-run config, so `bridge`/
    `adapter` take a `JudgeId`.

## D3 — `whenPhase` is `Implies (phaseAtLeast required, check)` — the inertness mechanism, not new logic

- **Decision**: `SpecKit.whenPhase : Phase -> Check<SpecKitFact> -> Check<SpecKitFact>` is
  `Implies (Atom phaseAtLeast, check)`, where `phaseAtLeast required` is an atomic probe that reads the
  supplied `PhaseReached` facts and reports `Met` iff some `PhaseReached q` has `Phase.reached q required`
  (`rank q ≥ rank required`), else `Unmet "phase not yet reached"`. The `required` phase is carried as a
  `LiteralArg` so `render`/`hash` distinguish `whenPhase Plan` from `whenPhase Tasks`.
- **Rationale**: this is the single most load-bearing design move (US2) and it is the **kernel's own
  `Implies`, reused** — no new combinator, no new verdict case. Against the kernel's exact semantics
  (`Implies(a,b) = Any[Not a; b]`, F03; `negate (Fail _) = Pass`, F02):
  - **Before the phase** (or no `PhaseReached` at all): antecedent `Unmet → Fail`; `Not (Fail) = Pass`;
    `Any [Pass; b] = Pass`. The guarded rule is a definite **`Pass`** — a *vacuously satisfied* /
    not-applicable result, **never `Fail` or `Uncertain`** for a not-yet-authored artifact (FR-005, edge
    cases "phase before an artifact exists" and "phase fact absent entirely"). This is exactly the F09
    cross-domain inertness mechanism (`Unmet ⇒ Not ⇒ Pass ⇒ vacuous Implies`), applied to a phase
    antecedent instead of a domain antecedent.
  - **At or after the phase**: antecedent `Met → Pass`; `Not (Pass) = Fail ""`; `Any [Fail ""; b] = b`.
    The guard is transparent — the implication reduces to the check's own verdict.
  - **Reified-ness preserving**: `Implies (Atom, reified)` is reified, so `whenPhase Phase.Tasks (allOf
    […])` stays `Deterministic`-eligible; `Implies (Atom, Opaque …)` is not reified, so `whenPhase
    Phase.Plan (Opaque …)` is forced to `AgentReviewed`/`HumanOnly` (FR-006, US2 acceptance preserved).
- **Alternatives considered**:
  - *Introduce a fourth `Verdict`/`Outcome` case "NotApplicable".* Rejected: it would change the kernel
    (forbidden — F10 adds adapter code only) and re-derive a distinction the three-valued algebra already
    expresses (a vacuous `Pass` via `Implies`). The spec frames not-applicable as "never a failing or
    unknown verdict" — i.e. a `Pass` — which the `Implies` desugaring delivers for free.
  - *Filter rules by phase before evaluation (drop guarded rules whose phase isn't reached).* Rejected: it
    would add adapter-side control flow over the catalog and lose the uniform "the catalog always runs;
    guards make rules inert" model — and a report (`analyze`) wants every rule present, inert or live.

## D4 — `Phase` and `SpecKitArtifact` are `RequireQualifiedAccess` (the case-name collision)

- **Decision**: Both `Phase` (`Constitution | Specify | Clarify | Plan | Tasks | Analyze | Implement |
  Merge`) and `SpecKitArtifact` (`Constitution | Spec | Plan | Research | DataModel | Contracts |
  Quickstart | Tasks | TaskDeps`) carry `[<RequireQualifiedAccess>]`; callers write `Phase.Plan` and
  `SpecKitArtifact.Spec`. `Phase` is ordered, with `Phase.rank`/`Phase.reached` giving "at or after".
- **Rationale**: the design doc writes both unions with bare cases, but `Constitution`, `Plan`, and
  `Tasks` appear in **both** — in F# two unions in the same namespace sharing case names do not compile.
  `RequireQualifiedAccess` is the idiomatic, zero-cost fix (Principle III sanctions it), and it reads
  well at the use site (`whenPhase Phase.Plan`, `SpecKit.toRef SpecKitArtifact.Spec`). The order of
  `Phase`'s declaration *is* the lifecycle order; `rank` reads it and `reached` is `rank current ≥ rank
  required` — the only thing `whenPhase` needs.
- **Alternatives considered**:
  - *Prefix the cases (`PhaseConstitution`, `ArtifactConstitution`).* Rejected: noisier than qualified
    access and departs from the design doc's names.
  - *Put the two unions in separate sub-modules.* Rejected: heavier than one attribute, and the facts
    reference both from one `SpecKitFact` union anyway.

## D5 — `TaskState`/`TaskDependsOn` run through the kernel's F05 evidence model, not a bespoke engine

- **Decision**: `TaskState` carries one of the **five AUTHORED** `EvidenceState`s
  (`Pending`/`Real`/`Synthetic`/`Failed`/`Skipped`); `AutoSynthetic` is **computed-only** and never
  supplied. The well-formedness sub-checks (`depsResolve`, `acyclic`) and the `evidenceNotSynthetic` rule
  build the kernel's `EvidenceGraph` from the `TaskState` nodes and `TaskDependsOn` edges via
  `Evidence.build`, and `evidenceNotSynthetic` then runs `Evidence.effective` and fails iff any node's
  effective state is `Synthetic`/`AutoSynthetic`. The taint propagation is the kernel's forward-chaining
  least-fixed-point — the adapter ships **no graph code**.
- **Rationale**: FR-012/US5 require "the kernel's F05 model run unchanged — `EvidenceGraph` is a
  derivation, not a bespoke engine," and call this "the concrete proof that this adapter reuses 100% of
  kernel facilities" (the heaviest legacy gate collapses to a kernel derivation). `Evidence.build` already
  returns `Cycle`/`UnknownNode` errors, so `acyclic` and `depsResolve` are read straight off its result
  (Met iff `Ok`, else `Unmet` with the `GraphError`), and `Evidence.effective` is the taint closure. This
  also distinguishes a *malformed* graph (`Evidence.build` error) from a *real* synthetic taint
  (`effective` state) — the Principle-VI defect-vs-bad-input distinction.
- **Alternatives considered**:
  - *Validate the DAG in the adapter.* Rejected outright: it would duplicate F05 and is exactly the
    "bespoke engine" FR-012 forbids.
  - *Carry `AutoSynthetic` as a supplied `TaskState`.* Rejected: `Evidence.build` refuses an
    `AutoSynthetic`-declared node (F05 FR-002); the taint is computed, never authored.

## D6 — The catalog is reified `CheckRule`s built with the F04 smart constructors; ids fixed here

- **Decision**: Eight named rules (the design doc's table), each built with `CheckRule.rule id tier spec
  check` then `|> blocking` / `|> asking`:
  - **Deterministic** (reified): `tasks-graph` (`whenPhase Phase.Tasks (allOf [everyTaskHasDeps;
    depsResolve; acyclic; skillIdsResolve])`), `constitution-complete` (`whenPhase Phase.Constitution`
    over the areas-filled probe), `contracts-current` (`whenPhase Phase.Plan` over the contract-currency
    probe), `evidence-not-synthetic` (the `Evidence.effective` probe), `fenced-surfaces-verified`.
  - **AgentReviewed** (advisory, `Opaque` + `asking`): `plan-satisfies-spec` (`whenPhase Phase.Plan`),
    `tasks-complete-ordered` (`whenPhase Phase.Tasks`).
  - **HumanOnly**: `feature-in-scope` (`Opaque`, no `asking`).
  The four `tasks-graph` sub-checks have documented total predicates: `everyTaskHasDeps` (every dependency
  endpoint a stated task), `depsResolve` (every dependent a stated task), `acyclic` (`Evidence.build` has
  no `Cycle`), `skillIdsResolve` (every `SkillBound` task is stated).
- **Rationale**: FR-006/FR-007 require a *catalog of reified rules*, each with a tier and severity, each
  rendering to a sentence — replacing the monolithic `analyze`. Building with the F04 constructors gets the
  `Deterministic`-reified-ness guardrail for free (an `Opaque` check cannot be `Deterministic` — the
  judgement rules are forced to `AgentReviewed`/`HumanOnly`), and every rule's `Check.render` becomes its
  contract statement (F06) and route line (F07) with no drift (SC-006). The rule ids are stable handles the
  dial names (`BlockingAtMerge`) and tests assert over. The precise sub-check predicates are design details
  fixed here and in the data model (spec Assumptions).
- **Alternatives considered**:
  - *One big `analyze` rule.* Rejected: it is exactly the opacity FR-006 removes — a wall of output that
    neither renders per-check nor declares per-check who decides / whether it blocks.

## D7 — Run-modes map to phases through F07 `Route`; nothing blocks before merge

- **Decision**: The inner-loop phases are evaluated with `Route.route fences applicableRules mode change`
  in `mode = Inner` (or `Sandbox` during `specify`/`clarify`); **merge** is evaluated in `mode = Gate` with
  the change at `Phase.Merge` (so `mergeFence` trips). Because `Route.route` makes a requirement a
  **blocking** gate iff `Severity = Blocking ∧ change Fenced ∧ mode = Gate`, every inner-loop result is
  advisory regardless of severity (a failing deterministic check reports, never blocks), and only at merge
  do the `Blocking` rules bite. The adapter ships **no run-mode logic** — it supplies the catalog and the
  fences; the host (F08/F12) chooses the mode per phase.
- **Rationale**: FR-008/FR-009/US3 require "advisory in the inner loop; merge is the single fence that
  flips to Gate." This is **entirely F07**, reused: the run-mode → block mapping is `Route.route`'s
  partition, and "merge recomputes from base" is the host evaluating the merge route over base-branch facts
  (F08's job). The adapter does not re-implement the mode dial; it just declares one fence and a catalog of
  rules with severities.
- **Alternatives considered**:
  - *Have the adapter decide blocking per phase.* Rejected: it would re-implement F07's run-mode
    arbitration and couple the adapter to a notion of "current mode" it does not own (the host picks the
    mode).

## D8 — The constitution dial is data (`ConstitutionDial`); the blocking set is authored, not hard-coded

- **Decision**: A `ConstitutionDial = { BlockingAtMerge: Set<RuleId>; EarlyFences: (string * Phase) list }`
  record models the constitution's "enforcement ↔ light" dial. `Catalog.adapter judge dial` promotes each
  catalog rule whose id is in `dial.BlockingAtMerge` to `CheckRule.blocking` (and keeps
  `evidence-not-synthetic` blocking regardless of the dial), and `Catalog.fences dial` is `mergeFence`
  plus one fence per `dial.EarlyFences` entry. `defaultDial` promotes `constitution-complete`,
  `contracts-current`, and `fenced-surfaces-verified`; a "light" dial is an empty `BlockingAtMerge`.
- **Rationale**: FR-010/FR-011/US4 require the constitution to be "the dial that configures which surfaces
  are fenced and which rules block at merge," with the blocking set being "the set the constitution
  authored, not a fixed list in the adapter" (SC-005). Modelling the dial as **data** the consumer (F12,
  from `constitution.md`) supplies makes the blocking set vary with the dial — verified by composing the
  adapter under two different dials and observing different merge routes. The opt-in earlier hard-stop
  (FR-010) is one `EarlyFences` entry plus the rule's own `|> blocking`, requiring no kernel change. The
  judge is a separate per-run `JudgeId` argument (it is run config, not constitution config — F04 doc).
  `evidence-not-synthetic` stays blocking outside the dial because FR-013 makes evidence honesty
  non-negotiable.
- **Alternatives considered**:
  - *Hard-code the merge-blocking set in the adapter.* Rejected: SC-005 explicitly requires it to be the
    authored dial's, not fixed logic — that is the whole point of "the constitution is the dial."
  - *Parse `constitution.md` to derive the dial.* Rejected: parsing the live repo is F08/F12's sensing
    job, not this pure feature (FR-015); the adapter takes the dial as a value.

## D9 — The adapter lifts unchanged into a coproduct; faithful lift proven against a second toy domain

- **Decision**: The Spec Kit adapter references only the SPI and the kernel (never another adapter). The
  faithful-lift guarantee (FR-014) is proven in the test project by authoring a **second, unrelated
  synthetic toy domain**, a closed `ProjectFact = SpecKit of SpecKitFact | Toy of ToyFact | Gov of
  RuleOutcome` coproduct with its single-case active patterns / `inject` / project `Identify`/`Bridge`,
  and asserting that for 100% of the catalog the lifted rule's `(verdict, provenance)` over
  coproduct-wrapped facts is identical to the standalone original (`Composition.lift (|SpecKit|_|)
  narrowSpecKit (Catalog.adapter judge dial)` → `Composition.compose`).
- **Rationale**: FR-014/SC-007 require the standalone and lifted `(verdict, provenance)` to be byte-for-byte
  identical (the F09 faithful-lift guarantee, since `Lift.check` keeps each probe's `Name`/`Reads`/`Args`
  and the combinator structure, so `render`/`hash` are invariant and the cache key does not move). Proving
  it needs a *second* domain to compose against; the Spec Kit adapter itself is the real adopter under test
  (not synthetic), but the second composition partner is a synthetic example domain, disclosed per
  Principle V (the same conservative framing F09 used for its toy domains). The project `Identify`
  delegating per case (`identify (SpecKit f) = SpecKit.identify f`) is the F09 law L3 that makes provenance
  ids survive the lift.
- **Alternatives considered**:
  - *Prove the lift with no second domain (trivial coproduct).* Rejected: it would not exercise lifting
    into a real multi-domain coproduct, which is what F11+F12 will do; a second toy domain is the minimal
    faithful test of composition.

---

### Build order (consequence of D1/D3)
The new project compiles `SpecKit.fsi`/`SpecKit.fs` **then** `Catalog.fsi`/`Catalog.fs` (`Catalog`
references `SpecKit`'s vocabulary/`whenPhase`/`bridge`/`probes` and the F09 `Adapter<…>`). The project
references only `FS.GG.Governance.Adapters.Spi`; **zero new `PackageReference`** (D1). The test project
references SpecKit (Expecto + FsCheck, already pinned) and authors the concrete `ProjectFact` coproduct +
the second unrelated example domain (D9), disclosed as a synthetic example domain (Principle V).
