---
title: Implementation plan (Spec Kit)
category: Governance
categoryindex: 6
index: 12
description: Detailed, Spec-Kit-tailored implementation plan that decomposes the governance-design kernel into a sequence of standard Spec Kit features (specify → plan → tasks → implement), each respecting the constitution's FSI-first / .fsi-visibility / Elmish-MVU / Tier-1-2 discipline and the light-by-default, light-dependency stance.
---

# Implementation plan (Spec Kit)

This is the **detailed** implementation plan for FS.GG.Governance. It elaborates
the org repository's coarse staged plan
([Stages G1–G5](https://github.com/FS-GG/.github/blob/main/docs/governance-implementation-plan.md))
and turns the [governance design](governance-design/index.md) into a concrete,
ordered set of **standard Spec Kit features** — each a `specs/NNN-slug/` unit run
through `specify → plan → tasks → implement` per the
[constitution](../.specify/memory/constitution.md).

It is a roadmap (the plan that *generates* the per-feature `plan.md`s), not a
substitute for them. Nothing here is built yet; the repo is at the end of Stage
G1 (fresh Spec Kit repo bootstrapped).

## 1. How this plan relates to the others

| Document | Role |
|---|---|
| [Governance design](governance-design/index.md) | *What* the system is (kernel, CheckTier, reified `Check`, routing, adapters). |
| [Org Stages G1–G5](https://github.com/FS-GG/.github/blob/main/docs/governance-implementation-plan.md) | *When* — coarse milestones and the adoption bar. |
| [Open questions](governance-design/open-questions.md) (issues [#1–#6](https://github.com/FS-GG/FS.GG.Governance/issues)) | Decisions to lock as we hit the relevant features. |
| **This plan** | *How*, in Spec Kit units — the feature sequence, surfaces, tests, and exit criteria. |

The mapping to org stages: the **kernel + evidence + JSON explanation**
(features F01–F06) *is* the "first useful product" (G2/G3); the **CLI + external
run** (F12–F13) is G3/G4; the **two-domain adapter set** (F09–F11) is the G5
adoption-bar evidence.

## 2. Ground rules every feature inherits (from the constitution)

Each feature's `spec.md`/`plan.md`/`tasks.md` MUST embody these — they are not
re-litigated per feature:

- **Order: Spec → FSI → Semantic Tests → Implementation.** Draft the public
  surface as a `.fsi`, exercise it in `scripts/prelude.fsx` / FSI first, write
  semantic tests against the *packed* library (or prelude), then implement `.fs`.
- **Visibility lives in `.fsi`.** Every public module ships a curated `.fsi`; no
  `private`/`internal`/`public` on top-level `.fs` bindings. A surface-area
  baseline per public module, checked by a drift test.
- **Change classification.** Each feature declares **Tier 1** (new/changed public
  API — full artifact chain incl. `.fsi` + baseline updates) or **Tier 2**
  (internal). Almost every feature below is Tier 1.
- **Elmish/MVU at the edge only.** The kernel and all interpreters are **pure** —
  Principle IV is *not applicable* to F01–F07, F09–F11. The effects shell (F08)
  and CLI (F12) are the stateful/IO features and MUST expose
  `Model`/`Msg`/`Effect`/`init`/`update` + an edge interpreter, with the only
  nondeterminism (agent calls) pushed to the boundary and reified as evidence.
- **Test evidence is mandatory; prefer real evidence.** Synthetic evidence is
  allowed only when disclosed (use-site `// SYNTHETIC:` comment, `Synthetic`
  token in the test name, PR listing). No evidence-audit gate machinery (it was
  stripped); disclosure is the discipline.
- **Idiomatic simplicity & observability.** Plain F#; complex features justified
  in the spec. Operationally significant events emit structured diagnostics;
  errors fail fast or degrade explicitly.
- **Light dependencies / dependency direction.** The kernel takes **no**
  dependency on FAKE, git, filesystem scanning, Skia, NuGet publishing, template
  profiles, or rendering paths. Generic code carries zero domain vocabulary.
  Governance may inspect a project; a project must never require governance.

## 3. Target architecture & solution layout

`net10.0`, package identity `FS.GG.Governance.*`, pack to
`~/.local/share/nuget-local/`. Dependency arrows point downward (lower depends on
nothing above it):

```text
FS.GG.Governance.Kernel        pure; BCL only (incl. System.Text.Json)
  ├─ facts, rules, fixed-point, provenance        (F01)
  ├─ verdicts + Kleene 3-valued logic             (F02, may fold into F03)
  ├─ reified Check algebra + 4 interpreters       (F03)
  ├─ CheckTier + Rule bridge + review cache key   (F04)
  ├─ evidence model + taint over a DAG            (F05)
  ├─ JSON explanation + evidence-freshness        (F06)
  └─ routing: Stakes / Severity / RunMode / Route (F07)

FS.GG.Governance.Adapters.Spi  pure; depends on Kernel   (F09)
FS.GG.Governance.Adapters.SpecKit   depends on Spi       (F10)
FS.GG.Governance.Adapters.DesignSystem  depends on Spi   (F11)

FS.GG.Governance.Host          effects shell (IO); depends on Kernel + Spi   (F08)
FS.GG.Governance.Cli           depends on Host + adapters                    (F12)

tests/         semantic tests load the PACKED libraries or the prelude
scripts/prelude.fsx   FSI entry point used by spec + tests
surface/       per-module surface-area baselines
fixtures/      non-rendering sample artifacts (a tiny RON/JSON tree, an essay)
```

Rationale for keeping the algebra *in* the kernel: per the design the four
interpreters and `CheckTier` carry zero domain vocabulary, so adapters reuse them
and supply only facts/artifacts/probes/rules/fences. JSON lives in the kernel
because `System.Text.Json` ships with the runtime (no new dependency).

## 4. The feature roadmap

Each feature is a Spec Kit unit. Entries give: **intent** (the spec's
user-visible outcome), **Tier**, **public surface** (the `.fsi` to design first),
**FSI focus**, **semantic-test focus**, **MVU**, **depends on**, **exit
criteria**. Run them roughly in order; `[P]` marks ones that can proceed in
parallel once their dependencies land.

### Phase A — The kernel (the first useful product) · org G2–G3

#### F01 · `001-kernel-core`
- **Intent:** a pure reasoner derives facts from asserted facts + rules to a fixed
  point and records why each derived fact holds.
- **Tier:** 1.
- **Surface:** `FactId`, `RuleId`, `ProvenanceStep`, `FactAssertion<'fact>`,
  `FactSet<'fact>`, `Rule<'fact>`, `EvaluationResult<'fact>`,
  `FixedPoint.evaluate (identify) (rules) (supplied)`.
- **FSI focus:** assert a handful of toy facts + 2–3 monotonic rules; watch
  derivation reach quiescence; inspect provenance.
- **Tests:** termination on a bounded fact space; **order-independence** (shuffle
  rule order → identical least fixed point); provenance records rule + inputs;
  injective `identify` dedup.
- **MVU:** N/A (pure).
- **Depends on:** —.
- **Exit:** monotonic forward-chaining engine with provenance, zero deps, surface
  baseline recorded. **Locks decision #4** (kernel constraints): rules are
  monotonic; negated/aggregated facts are *supplied* (lower stratum), never
  derived in the same fixed point — documented as a precondition.

#### F02 · `002-verdicts-kleene` [P after F01]
- **Intent:** three-valued verdicts compose order-independently.
- **Tier:** 1. **Surface:** `Verdict = Pass | Fail of string | Uncertain of string`
  + Kleene `and`/`or`/`negate` combinators.
- **Tests:** commutativity/associativity of `All`/`Any` combination; `Uncertain`
  is not silently coerced to pass/fail. **MVU:** N/A. **Depends on:** F01.
- **Exit:** verdict algebra ready for the `Check` interpreters. *(May be folded
  into F03 if small.)*

#### F03 · `003-check-algebra`
- **Intent:** a rule's check is one reified value that can be evaluated, rendered,
  hashed, and explained from a single source.
- **Tier:** 1.
- **Surface:** `ArtifactRef`, `Outcome`, `ProbeArg`, `Probe<'fact>`,
  `Check<'fact> = Atom | All | Any | Not | Implies | Opaque`; smart constructors
  (`probe`, `allOf`, `anyOf`, `not'`, `==>`, `.&`, `.|`); `Check.eval/render/hash/
  explain/reads/isReified`.
- **FSI focus:** build a small check by hand; fold it four ways; confirm
  `render`/`hash` work **without executing** `Eval`.
- **Tests:** `eval` Kleene semantics; `hash` canonicalizes commutative nodes
  (`All [a;b] == All [b;a]`) but stays positional for `Implies`/`Atom` args
  (**decision #4 / hazard 3**); `explain` proof tree matches `eval`; `isReified`
  is false iff an `Opaque` is present.
- **MVU:** N/A (applicative, no `bind`). **Depends on:** F02.
- **Exit:** the inspectable algebra + four interpreters; the keystone of the design.

#### F04 · `004-checktier-rule-bridge`
- **Intent:** every rule declares who is competent to decide it, and agent
  reviews are cached so a stochastic judge stays reproducible.
- **Tier:** 1.
- **Surface:** `CheckTier = Deterministic | AgentReviewed | HumanOnly`;
  `Severity = Advisory | Blocking`; `SpecSource`; `Rule` record (tier/spec/
  severity/check/question); `rule`, `blocking`, `asking`; `toRule`;
  review-request + recorded-verdict facts; the cache-key function.
- **Tests:** `rule` **refuses `Deterministic` when `not isReified`** (forces
  Agent/Human); cache hit vs miss; key changes when inputs change.
- **MVU:** N/A (pure; the actual agent call is F08). **Depends on:** F03.
- **Exit:** the bridge from `Check` to kernel `Rule`. **Locks decision #1**: the
  cache key = `Check.hash` + artifact hashes **+ judge model id + judge version +
  reviewer-prompt hash**, with a defined re-review policy when the judge changes.
  **Notes decision #2** (single-sample noise — whether to aggregate N runs /
  require a confidence threshold before freezing) for the F08 interpreter.

#### F05 · `005-evidence-model` [P after F01]
- **Intent:** evidence state is tracked and synthetic taint propagates over the
  dependency graph and clears when the root cause is upgraded.
- **Tier:** 1.
- **Surface:** `EvidenceState = Pending | Real | Synthetic | Failed | Skipped |
  AutoSynthetic`; `EvidenceGraph` (DAG, rejects cycles); `effective` taint
  closure.
- **Tests:** transitive `AutoSynthetic` flow; auto-clear on `Synthetic → Real`;
  cycle rejection; least-fixed-point determinism. Generalize beyond software (a
  finding on simulated data is `AutoSynthetic`).
- **MVU:** N/A. **Depends on:** F01. **Exit:** evidence taint as a kernel
  derivation, not a bespoke engine. **Reinforces #4** (DAG only; no cycles).

#### F06 · `006-explanation-output`
- **Intent:** explanations, the rendered rule contract, and evidence-freshness
  predicates are emitted as JSON-friendly, human/agent-readable output.
- **Tier:** 1.
- **Surface:** `Explanation` serialization; `contract : Rule list -> ...`
  (a *fold of the rules*, not a hand-maintained file); evidence-freshness
  predicates (the "simple freshness" from the first-product scope).
- **Tests:** contract is the rendered selector (cannot drift); JSON round-trips;
  freshness predicates over fixture timestamps.
- **MVU:** N/A. **Depends on:** F03, F05.
- **Exit:** **the first useful product is complete** — a kernel that stores facts,
  evaluates rules to a fixed point, carries provenance, taints synthetic
  evidence, and emits JSON explanations, with zero heavy deps. Pack
  `FS.GG.Governance.Kernel` to `~/.local/share/nuget-local/`.

### Phase B — Routing & the effects edge · org G3

#### F07 · `007-routing-severity-modes`
- **Intent:** a change gets only the proof its risk warrants, and every routing
  decision explains itself.
- **Tier:** 1.
- **Surface:** `ChangeSet` (abstract), `Stakes = Routine | Fenced of string`,
  `Fence`, `stakesOf`; `RunMode = Sandbox | Inner | Gate`; `Route` +
  `renderRoute`.
- **FSI focus:** a no-fence change → "light — no gates"; a fenced change → a
  blocking gate that names rule + fence + rendered check.
- **Tests:** light-by-default (unclassified ⇒ `Routine`, no gates); blocking set
  is filterable and short; `Route` always carries a reason; combination is
  deterministic-precedence (forbid-trumps-permit), **never positional** (decision
  #4 / hazard 5).
- **MVU:** N/A (pure). **Depends on:** F04.
- **Exit:** light, advisory, explainable routing over the kernel.

#### F08 · `008-effects-interpreter`
- **Intent:** the impure shell gathers facts, runs the pure kernel, and interprets
  effects (read artifacts, dispatch an agent review, record a verdict) at the
  edge.
- **Tier:** 1.
- **Surface (MVU):** `Model` (loaded facts + pending reviews), `Msg`
  (artifact-read results, agent verdicts, transitions), `Effect`/`Cmd<Msg>`
  (ReadArtifact / DispatchReview / RecordVerdict), `init`, pure `update`, and an
  interpreter that executes effects.
- **FSI focus:** drive `init`/`update` through the packed library; assert emitted
  effects without running IO.
- **Tests:** pure transition tests (Model+Msg ⇒ Model+effects); interpreter tests
  against a **real** filesystem fixture; a recorded agent verdict round-trips and
  hits the F04 cache on re-run.
- **MVU:** **applicable** — this is the boundary feature.
- **Depends on:** F06, F07.
- **Exit:** sense→plan→act loop with nondeterminism reified as evidence. **Locks
  decision #2** (aggregate/threshold before freezing a verdict) and **decision
  #3** (reviewer prompt-injection: treat governed artifacts as untrusted data,
  isolate instruction vs. data in the review prompt). **Opens decision #5**
  (cost/latency budget + judge-vs-human meta-validation) as a tracked deferral.

### Phase C — Adapters & composition (the adoption bar) · org G5

#### F09 · `009-adapter-spi`
- **Intent:** a domain plugs in by supplying only facts, an artifact mapping,
  probes, a rule catalog, and fences; everything else is reused.
- **Tier:** 1.
- **Surface:** the adapter interface; the composition root — a coproduct
  `ProjectFact` with `Rule.contramapFacts` lifting and single-case active
  patterns; deterministic, order-independent cross-domain precedence.
- **Tests:** an adapter's rules lift into `ProjectFact` and evaluate unchanged;
  a cross-domain `Implies` rule is order-independent; removing one adapter leaves
  the kernel + other adapters intact (the boundary test).
- **MVU:** N/A. **Depends on:** F04 (+F05). **Exit:** the SPI + composition root;
  the "kernel is a library, not a platform" contract made concrete.

#### F10 · `010-adapter-speckit`
- **Intent:** the Spec Kit workflow is governed as data — phases and task states
  are facts, phase checks are reified rules, the merge boundary is the one fence.
- **Tier:** 1.
- **Surface:** `SpecKitArtifact`, `Phase`, `SpecKitFact`
  (`PhaseReached`/`ArtifactPresent`/`TaskState`/`TaskDependsOn`/…), `whenPhase`
  guard, the phase-check rule catalog, `mergeFence`, constitution-as-dial.
- **Tests:** `whenPhase Plan` contributes only at/after Plan; everything is
  advisory in the inner loop and only `merge` flips to `Gate`; the
  evidence/`TaskDependsOn` graph runs through the F05 model (not a bespoke
  engine).
- **MVU:** N/A. **Depends on:** F09. **Exit:** governance dogfoods **this repo's
  own** Spec Kit workflow — domain #1 for the adoption bar.

#### F11 · `011-adapter-designsystem` [P after F09]
- **Intent:** a second, unrelated domain governs a design language from fixtures,
  proving generality without copying domain #1's shape.
- **Tier:** 1.
- **Surface:** `DesignArtifactRef`, design probes (`surfaceMatches`,
  `contrastMeets`), the tiered rule catalog (deterministic token/contrast =
  blocking; judgement rules via `Opaque` ⇒ AgentReviewed).
- **Tests:** deterministic rules render + hash; `Opaque` judgement rules route to
  an agent with the rule's `Question`; runs against a **fixture** token tree (no
  rendering dependency, no rendering vocabulary in generic code).
- **MVU:** N/A. **Depends on:** F09. **Exit:** domain #2 — **adoption bar met**
  (two unrelated domains adopt the kernel cheaply).

### Phase D — CLI & external validation · org G3–G4

#### F12 · `012-cli`
- **Intent:** a person or CI runs `route` / `explain` / `contract` / evidence
  report against a repo snapshot and gets text or JSON out.
- **Tier:** 1.
- **Surface (MVU/edge):** CLI commands wired to the F08 interpreter; text +
  `--json`; exit codes (advisory = 0, blocking-fail at `Gate` = nonzero).
- **Tests:** smoke runs against the fixtures and against this repo's own
  `.specify` tree (dogfood); JSON output is stable; `Sandbox`/`Inner`/`Gate`
  behave per mode.
- **MVU:** applicable (IO at the edge). **Depends on:** F08, F10 (+F11). **Exit:**
  the optional CLI tool (org G3); pack as a tool to `~/.local/share/nuget-local/`.

#### F13 · `013-run-against-external-repo`
- **Intent:** point the tool at an external checkout (a rendering repo, or a
  Sojourn fixture) from the outside and produce an advisory report.
- **Tier:** 1 (mostly docs + an adapter/fixture; little new kernel surface).
- **Tests:** the external repo needs **no** dependency on governance; removing the
  tool changes nothing for it; findings convert to ordinary issues/tasks.
- **MVU:** reuses F08/F12. **Depends on:** F12. **Exit:** org **G4** — validated
  against an external customer from outside; advisory by default (org **G5**
  adoption decision starts here).

## 5. Cross-cutting concerns

- **Surface baselines + drift test** (Principle II): stand up the API-surface
  baseline mechanism alongside F01 and extend it per public module thereafter.
- **Observability:** pick a structured-logging approach — `TODO(STRUCTURED_LOGGING)`
  in the constitution. Record the choice in an ADR (`docs/decisions/`) before F08
  (the first feature that does real IO).
- **Packaging:** `Directory.Build.props` / `Directory.Packages.props`, `net10.0`,
  `FS.GG.Governance.*` ids, pack to `~/.local/share/nuget-local/`. The Kernel
  packs after F06; the CLI tool after F12.
- **FSI prelude:** `scripts/prelude.fsx` loads the packed kernel for spec-time
  sketching and semantic tests (the constitution's "exercise through the same FSI
  surface" rule).
- **Fixtures:** a tiny, non-rendering sample tree (a few JSON/RON files + an essay
  doc) so adapters and the CLI can be tested without any consumer repo.

## 6. Decisions to lock (from the open questions)

| # | Decision | Locked at |
|---|---|---|
| [#1](https://github.com/FS-GG/FS.GG.Governance/issues/1) | Agent-review cache key includes judge model id + version + prompt hash; define re-review-on-judge-change policy. | F04 |
| [#2](https://github.com/FS-GG/FS.GG.Governance/issues/2) | Aggregate N runs / require a confidence threshold before freezing a verdict. | F04 spec → F08 impl |
| [#3](https://github.com/FS-GG/FS.GG.Governance/issues/3) | Reviewer prompt-injection: governed artifacts are untrusted data; isolate instruction vs. data. | F08 |
| [#4](https://github.com/FS-GG/FS.GG.Governance/issues/4) | Kernel preconditions: monotonic; stratify negated facts; forbid/stratify aggregation & recursive negation; commutative-node hash canonicalization. | F01, F03, F05 |
| [#5](https://github.com/FS-GG/FS.GG.Governance/issues/5) | Cost/latency budget for agent reviews + a judge-vs-human meta-validation loop. | Deferred; revisit at F12 |
| [#6](https://github.com/FS-GG/FS.GG.Governance/issues/6) | Narrow the OPA claim; frame the policy-engine analogy as architectural. | Docs task (no code) |

## 7. Milestones

1. **M1 — First useful product (F01–F06).** Pure kernel + evidence + JSON
   explanation, packed, zero heavy deps. Satisfies org G2/G3 "narrow tool" and
   the project-scope "first useful product."
2. **M2 — Light routing + effects edge (F07–F08).** Explainable, light-by-default
   routing and the MVU interpreter that dispatches agent reviews as reified
   evidence.
3. **M3 — Adoption bar (F09–F11).** SPI + composition root + two unrelated
   domains (Spec Kit, design-system). The kernel is now demonstrably a library,
   not a platform.
4. **M4 — Tool + external validation (F12–F13).** Optional CLI; run against an
   external repo from the outside (org G4); begin the org G5 adoption decision.

## 8. Driving it with Spec Kit

For each feature, in order:

1. `/speckit-specify` — author `specs/NNN-slug/spec.md`: user-visible outcome,
   scope, **Tier**, public-API impact, verification approach.
2. *(optional)* `/speckit-clarify` — resolve unknowns.
3. `/speckit-plan` — design: the `.fsi` contract, the FSI sketch, the
   project/layout deltas, MVU model where applicable, and which decisions (§6)
   this feature locks.
4. `/speckit-tasks` — author `tasks.md` (FSI sketch → semantic tests → impl →
   `.fsi`/baseline update → pack, in that constitutional order).
5. `/speckit-implement` — implement against the stable signature and passing
   tests; disclose any synthetic evidence per Principle V.
6. `/speckit-analyze` before merge as an advisory cross-artifact check.

Feature IDs above (`001`…`013`) are proposed `specs/` slugs; renumber freely as
features split or merge (F02 may fold into F03; F13 is largely docs).

## 9. Risks & deferrals

- **LLM-judge realities** (#1–#3, #5) are the main exposure; they are quarantined
  to F04/F08 and not on the M1 critical path — the first useful product has no
  agent dependency at all.
- **Scope creep into a platform.** Each feature must keep the kernel
  domain-vocabulary-free and deletable; the adoption bar (M3) is the explicit
  check that generality is real, not assumed.
- **Adapter realism.** The design-system and Sojourn examples are sketches;
  expect probe bodies (the `fun facts -> … Met` parts) to be the bulk of real
  adapter work and to need their own fixtures.
- **Confluence edge cases** (#4) are theory-level today; they become real only if
  a rule ever needs aggregation or negation over still-being-derived facts — keep
  the kernel inside the safe fragment by construction.
