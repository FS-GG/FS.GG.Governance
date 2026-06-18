# Phase 0 Research: CheckTier & Rule Bridge (F04 · `004-checktier-rule-bridge`)

All Technical Context unknowns are resolved below. The behavioural model is fixed by
[spec.md](./spec.md), `docs/governance-design/rule-edsl.md` ("The bridge back to the
kernel" and "Guardrails this buys") and `docs/governance-design/kernel.md` (the
`CheckTier` arbitration model); the roadmap
(`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`, §F04) fixes the
public surface and assigns **decision #1** (cache key composition) and the **note on
decision #2** (single-sample noise). No `NEEDS CLARIFICATION` markers remain. The
decisions here are the *engineering* choices the spec deliberately left to planning (spec
Assumptions: "the exact encoding of the cache key, the exact field layout of the
review-request and recorded-verdict facts, and the exact signatures of the smart
constructors are implementation/design details fixed in the plan and the `.fsi`").

## D1 — Where the bridge lives, compile order, and the `CheckRule` name

- **Decision**: Add the bridge to the **existing `FS.GG.Governance.Kernel` assembly** as a
  new `CheckRule.fsi`/`CheckRule.fs` pair in `src/FS.GG.Governance.Kernel/`, compiled
  **after** `Check.*` (hence after `Verdict.*`/`Kernel.*`). No new project. The authored
  rule type is named **`CheckRule<'fact>`**, not `Rule<'fact>` as the design doc sketches.
- **Rationale**: The bridge genuinely depends on all three predecessors — it folds a
  `Check` (F03), a `RuleOutcome`/cache hit carries a `Verdict` (F02), and `toRule` produces
  a `Kernel.Rule<'fact>` and emits `FactAssertion`/`ProvenanceStep` (F01) — so it compiles
  last. The roadmap (§3) keeps the tier model and bridge *in the kernel* precisely so
  adapters (F09–F11) reuse them with **zero new dependencies**, supplying only a
  `Bridge<'fact>` and a probe set; the bridge is pure and domain-neutral (FR-015), exactly
  the kernel's contract. **The name `CheckRule` is forced**: F01 already ships a kernel type
  `FS.GG.Governance.Kernel.Rule<'fact>` (the executable rule), and a second `Rule<'fact>` in
  the same namespace is a duplicate-definition error. `rule-edsl.md` writes the authored
  type as `Rule<'fact>` and the executable one as `Kernel.Rule<'fact>` because it imagined
  the eDSL in a *separate* module/namespace; the implemented repo (F01–F03) keeps everything
  in one `FS.GG.Governance.Kernel` namespace, so the authored type takes the distinct,
  descriptive name `CheckRule<'fact>` ("a rule whose logic is a `Check`"). `toRule`'s return
  type is the unqualified kernel `Rule<'fact>`.
- **Alternatives considered**: a separate `FS.GG.Governance.Rules` namespace/assembly so the
  authored type could keep the name `Rule<'fact>` (referring to `Kernel.Rule`) — rejected:
  a new project for a value algebra is against Principle III / SC-009 and breaks the
  one-namespace convention F01–F03 established. Naming it `TieredRule`/`GovRule`/
  `AuthoredRule` — `CheckRule` was chosen as the clearest pairing with the F03 `Check`
  keystone and the most discoverable for the adapters that will reference it heavily.

## D2 — `CheckTier`, `Severity`, `SpecSource`, `JudgeId` shapes

- **Decision**: Adopt the design's exact arbitration model: `CheckTier = Deterministic |
  AgentReviewed | HumanOnly` (kernel.md §CheckTier) and `Severity = Advisory | Blocking`,
  the two **orthogonal** (any tier × any severity). `SpecSource = { Document: string;
  Section: string }` — a structural, renderable/hashable handle to the authoritative
  requirement. `JudgeId = { ModelId: string; Version: string }` — the judge identity folded
  into the cache key; the reviewer **prompt** is *not* here (it is the rule's `Question`).
- **Rationale**: The three tiers are the bridge between classical evaluation and the agent
  harness (the feature's whole point); two severities with an `Advisory` default match
  routing-and-modes.md and keep severity a routing concern, not a decision concern (FR-002,
  SC-008). A structural `SpecSource` keeps provenance domain-neutral (FR-003) and feeds the
  F06 contract fold without the kernel interpreting its content. Splitting the prompt
  (per-rule `Question`) from the model identity (`JudgeId`, per-run config) is what makes
  decision #1's "reviewer-prompt hash" naturally per-rule while "judge model id + version"
  is supplied once by F08's configuration (D4).
- **Alternatives considered**: `SpecSource of string` (single-case) — rejected as too thin
  for provenance (a document/section split is what F06's contract and audit trails want);
  folding `Severity` into `CheckTier` — rejected, the design is explicit that they are
  orthogonal (a Deterministic rule can be Advisory or Blocking). Putting the reviewer prompt
  in `JudgeId` — rejected: the prompt is authored per rule (the `Question`), so it belongs
  to the rule, and decision #1 lists it as a *separate* key ingredient from model/version.

## D3 — The authored `CheckRule<'fact>` and the reified-ness guardrail (FR-006)

- **Decision**: `CheckRule<'fact> = { Id: RuleId; Tier: CheckTier; Spec: SpecSource;
  Severity: Severity; Check: Check<'fact>; Question: string option }`. It is built through
  smart constructors, not by hand, so the guardrail cannot be bypassed:
  - `rule id tier spec check : Result<CheckRule<'fact>, RuleRejection>` sets
    `Severity = Advisory`, `Question = None`, and **refuses** `Deterministic` when
    `not (Check.isReified check)`, returning `Error (OpaqueCannotBeDeterministic id)`. Every
    other tier (and `Deterministic` over a reified check) returns `Ok`.
  - `blocking : CheckRule<'fact> -> CheckRule<'fact>` promotes `Severity` to `Blocking`.
  - `asking prompt : CheckRule<'fact> -> CheckRule<'fact>` sets `Tier = AgentReviewed` and
    `Question = Some prompt`. Because it targets `AgentReviewed` (which accepts any check),
    it is the natural way to author an agent rule over an `Opaque`/non-reified check and
    never trips FR-006.
  - `RuleRejection = OpaqueCannotBeDeterministic of RuleId` — the one refusal.
- **Rationale**: The guardrail is the headline safety property (US3) and the reason
  `Check.isReified` exists (F03). Putting it in the constructor (not in `toRule`) makes an
  opaque-Deterministic rule **unconstructable** rather than failing later at bridge time —
  the earliest, clearest place to enforce it. `Result` (not `option`) carries the reason
  and reads cleanly with `Result.map`/`Result.bind`; it needs no computation expression
  (Principle III). `blocking`/`asking` as post-construction modifiers keep the base `rule`
  small and let authoring read declaratively: `rule id AgentReviewed spec chk |> Result.map
  (asking "Is the tone professional?" >> blocking)`.
- **Alternatives considered**: `rule` returning `CheckRule<'fact> option` — rejected, loses
  the refusal reason that SC-001 / the V13 test assert on. Auto-promoting a Deterministic
  opaque rule to `AgentReviewed` silently — rejected: the spec says *refuse*, and a silent
  tier change hides an authoring mistake (the opposite of "opacity becomes a typed fact").
  A runtime `failwith` on a bad tier — rejected (totality FR-017; a `Result` is the honest
  total encoding).

## D4 — `cacheKey`: decision #1, a pure fold over its ingredients

- **Decision**: `cacheKey (judge: JudgeId) (checkHash: string) (artifactHashes: string
  list) (question: string option) : string` is a pure SHA-256 hex digest over a
  **prefix-free** pre-image combining, in fixed order: `judge.ModelId`, `judge.Version`,
  `checkHash`, the `artifactHashes` **de-duplicated and ordinal-sorted**, and the
  **reviewer-prompt hash** of `question` (the SHA-256 of the prompt string, or a fixed
  sentinel for `None`). Each component is itself hashed to fixed-width hex first and
  concatenated, the same prefix-free discipline F03's `hash` uses, so there is no
  delimiter-injection ambiguity. `toRule` assembles the ingredients:
  `checkHash = Check.hash rule.Check`; `artifactHashes = Check.reads rule.Check |>
  List.map (bridge.ArtifactHash facts)`; `question = rule.Question`.
- **Rationale**: This **locks decision #1** (roadmap §F04, issue #1): the key is
  `Check.hash` + artifact hashes **+ judge model id + judge version + reviewer-prompt hash**.
  Folding in the judge identity is what makes the **re-review-on-judge-change** policy
  correct and automatic (D5). Keeping `cacheKey` a **pure function of already-computed
  ingredients** (rather than of the rule + facts) is the testability decision: SC-002
  requires varying each of the five ingredients in isolation and observing the key change —
  trivial when each is a parameter, awkward if the function also has to fold a check and
  scan facts. De-duplicating + ordinal-sorting the artifact hashes is the **F04 cache-key
  policy** that F03's `reads` deliberately deferred (research F03 D7): the set of artifacts
  under review is order-independent, so a probe order or a duplicate read must not change
  the key (spec edge "Empty read set" and US2). Ordinal sort is culture-invariant (same
  discipline as F02 reason aggregation and F03 commutative-node hashing), giving the
  byte-for-byte-identical-across-machines guarantee. SHA-256 is `System.*` (zero new deps,
  SC-009), already used by F03.
- **Alternatives considered**: `cacheKey : JudgeId -> FactSet<'fact> -> CheckRule<'fact> ->
  string` (folding the check and reading facts internally) — rejected: harder to unit-test
  per-ingredient (SC-002), and it would duplicate the `Check.hash`/`Check.reads` calls
  `toRule` already makes. *Not* sorting the artifact hashes (keep `reads` order) — rejected:
  two rules reading the same artifacts in different probe orders would key differently and
  miss the cache spuriously (the exact Hazard-3-style confluence problem F03 closed for the
  check structure; the artifact half must close it too). A non-cryptographic combiner —
  rejected for the same reason as F03 D4 (`GetHashCode` is not stable across processes;
  a persisted cache key must be).

## D5 — `toRule`: the three-tier bridge, cache hit/miss, and re-review

- **Decision**: `toRule (bridge: Bridge<'fact>) (rule: CheckRule<'fact>) : Rule<'fact>`
  returns a kernel rule with `Id = rule.Id`, `Description = Check.render rule.Check`, and an
  `Apply` that, per tier:
  - **`Deterministic`** → `[ emit (Decided (rule.Id, Check.eval facts rule.Check)) ]` — the
    three-valued verdict is asserted verbatim, never coerced (FR-008, SC-005).
  - **`AgentReviewed`** → compute `key = CheckRule.cacheKey bridge.Judge (Check.hash
    rule.Check) (Check.reads rule.Check |> List.map (bridge.ArtifactHash facts))
    rule.Question`; then look for a recorded verdict:
    `facts |> List.tryPick (fun f -> match bridge.Project f.Value with Some (Reviewed r) when
    r.Key = key -> Some r.Verdict | _ -> None)`. On `Some v` (**cache hit**) →
    `[ emit (Decided (rule.Id, v)) ]`, no request, no agent call. On `None` (**cache miss**)
    → `[ emit (NeedsReview { Rule = rule.Id; Question = rule.Question; Key = key }) ]`,
    exactly one (FR-009, SC-003).
  - **`HumanOnly`** → `[ emit (Escalated rule.Id) ]`, regardless of severity (FR-010, SC-008).
  where `emit inputs outcome = { Id = <placeholder>; Value = bridge.Embed outcome;
  Provenance = [ { Rule = rule.Id; Inputs = inputs; Note = Check.render rule.Check } ] }`.
  The `Id` placeholder is overridden by the kernel's `identify` at evaluation time (F01
  `FixedPoint.evaluate` re-keys every produced fact — Kernel.fs step 3a), so the bridge does
  not own fact identity.
- **Provenance `Inputs`**: `ProvenanceStep.Inputs : FactId list`, but the domain-neutral
  `Bridge` cannot resolve an `ArtifactRef` (from `Check.reads`) to the `FactId` of the
  artifact-content fact carrying it — `ArtifactHash` returns a content-hash *string* and
  `Project` recognises only `RuleOutcome`s; letting the kernel scan/interpret adapter facts
  to recover those ids would violate FR-015. So `Inputs = []` for `Deterministic`,
  `HumanOnly`, and an `AgentReviewed` **miss**; on an `AgentReviewed` **hit** the lookup
  captures the matching `RecordedReview`'s `f.Id`, so `Inputs = [ f.Id ]` (the recorded
  input the rule actually consumed). This satisfies US1 AS1 (justification by rule identity,
  always present) without bloating the `Bridge`. Recording artifact-**read** provenance is
  **deferred** — a later feature MAY add a `Bridge` resolver (`ArtifactRef -> FactId list`)
  if F06 explanation needs it; F04 does not.
- **Alternative considered for `Inputs`**: add `ArtifactFacts : FactSet<'fact> -> ArtifactRef
  -> FactId list` to `Bridge` so artifact-read fact ids populate `Inputs` — **rejected** for
  F04: it forces every adapter (F09–F11) to implement a second fact-scanner, duplicates
  `ArtifactHash`'s logic, and grows the Tier-1 surface for a provenance field F04's
  acceptance does not require. Revisit only when a downstream feature needs it.
- **Rationale**: `Deterministic` = `Check.eval` and `HumanOnly` = escalate are direct from
  the design (rule-edsl.md). The `AgentReviewed` branch is the heart of the feature: the
  recorded-verdict lookup keyed by `cacheKey` makes a stochastic judge reproducible (US2),
  and because the key folds the judge identity (D4), a verdict frozen under an old judge has
  an old key and **misses** once the judge changes — the **re-review-on-judge-change policy
  falls out for free** (FR-013, SC-004), no extra bookkeeping. Emitting a `NeedsReview`
  *value* rather than calling an agent is the Principle IV "I/O as data" boundary: F04 is the
  pure functional core; F08 is the interpreter. `Description = Check.render` is the no-drift
  guarantee (FR-007, SC-006) — the contract text *is* the rendered selector. Attaching a
  `ProvenanceStep` naming the rule keeps the emitted governance fact explainable (F01's
  reason-maintenance contract); deferring `Id` to `identify` matches how every F01 rule
  already works (Kernel.fs overrides rule-supplied ids).
- **Alternatives considered**: having `toRule` itself perform the agent call — rejected, that
  is F08 and would make the kernel impure (FR-015, Principle IV). Emitting the recorded
  `Reviewed` fact unchanged on a hit instead of a fresh `Decided` — rejected: downstream
  routing (F07) consumes a uniform `Decided` verdict, and re-emitting `Reviewed` would
  conflate "the agent's recording" with "this rule's decision this run". Looking up the
  recorded verdict by `RuleId` alone (not by `key`) — rejected: it would reuse a stale
  verdict after the inputs or judge changed, defeating SC-004.

## D6 — `Bridge<'fact>`, `RuleOutcome`, and how governance facts embed in `'fact`

- **Decision**: The kernel owns the domain-neutral `RuleOutcome = Decided of RuleId *
  Verdict | NeedsReview of ReviewRequest | Reviewed of RecordedReview | Escalated of
  RuleId`, plus `ReviewRequest = { Rule; Question; Key }` and `RecordedReview = { Rule;
  Key; Verdict }`. `toRule` emits `Decided`/`NeedsReview`/`Escalated`; `Reviewed` is the
  F08-written record that `toRule` only reads (the cache-hit lookup). The adapter
  supplies a `Bridge<'fact> = { Judge: JudgeId; ArtifactHash: FactSet<'fact> -> ArtifactRef
  -> string; Embed: RuleOutcome -> 'fact; Project: 'fact -> RuleOutcome option }`. `toRule`
  uses `Embed` to lift each outcome into `'fact` and `Project` to recover a `RecordedReview`
  for the cache-hit lookup; `ArtifactHash` reads an artifact's content hash **from the
  facts** (an adapter asserts artifact-content facts).
- **Rationale**: The kernel `Rule<'fact>.Apply` returns `FactAssertion<'fact> list`, so a
  governance outcome must become a `'fact`. The kernel cannot know the adapter's fact union,
  so the embedding is **caller-supplied** — `Embed`/`Project` are the lift/lower pair (the
  same shape F09's adapter SPI uses with `contramapFacts` and single-case active patterns).
  This is the informed default recorded in the spec Assumptions, now pinned. Owning
  `RuleOutcome` *in the kernel* (rather than leaving it fully adapter-defined) gives F07/F08
  a single, domain-neutral governance vocabulary to route and dispatch on, while `Embed`/
  `Project` keep `'fact` opaque to the kernel (FR-015). Reading artifact content hashes from
  the facts (not via live I/O) is what keeps `toRule`/`cacheKey` **pure** (FR-015): the
  adapter's *edge* gathered the artifact bytes and asserted a content-hash fact; the bridge
  only folds it.
- **Alternatives considered**: four separate injection functions (one per outcome) instead of
  one `Embed: RuleOutcome -> 'fact` — rejected, a single union + one `Embed` is simpler and
  lets F07/F08 match exhaustively. A bespoke `RecordedVerdict: FactSet -> RuleId -> string ->
  Verdict option` in the `Bridge` instead of a general `Project` — rejected: `Project` is
  reused by F07/F08 to read *all* governance outcomes, not just recorded verdicts, so one
  projection covers the cache-hit lookup and downstream routing. Making `ArtifactHash` do
  live file I/O — rejected (impurity; the kernel never touches the filesystem — kernel.md
  "Pure core, effects at the edge").

## D7 — Test approach

- **Decision**: One new `CheckRuleTests.fs` reusing **Expecto + FsCheck** (F01 D5). A tiny
  in-test adapter `'fact` (e.g. a union `Gov of RuleOutcome | Artifact of string * string`)
  supplies a real `Bridge` (`Embed = Gov`; `Project = function Gov o -> Some o | _ -> None`;
  `ArtifactHash` looks up an `Artifact` fact by `ArtifactRef`). Headline checks: the
  reified-ness refusal (`rule id Deterministic spec opaqueCheck = Error …`, and `Ok` for a
  reified check or any other tier); **FsCheck** properties for `cacheKey` — identical
  ingredients → identical key, and changing each of {model id, version, check hash, an
  artifact hash, the prompt} → a different key (SC-002); cache **hit** (a `Reviewed` fact
  with the matching key present ⇒ `Decided`, no `NeedsReview`) vs **miss** (absent ⇒ exactly
  one `NeedsReview` carrying the key); **re-review** (mutate `JudgeId` ⇒ the previously
  matching recorded verdict no longer matches ⇒ a fresh `NeedsReview`); `Description =
  Check.render`; tier/severity orthogonality (`blocking` flips severity, leaves tier;
  `HumanOnly` escalates whether Advisory or Blocking); and totality (`toRule` + `Apply` over
  every tier, an empty fact set, and an unknown artifact never throw). Compile order in
  `.fsproj`: `CheckRule.fsi`/`CheckRule.fs` **after** `Check.*`; `CheckRuleTests.fs` before
  `Main.fs`. The reflective V11 surface-drift test extends to the `CheckRule` surface once
  re-blessed; V12 re-confirms BCL-only (SHA-256 is `System.*`) — **no new drift/hygiene test
  needed**.
- **Rationale**: Real `CheckRule`/`Bridge` values exercise the public surface a downstream
  adapter would use (Principle I/V); FsCheck is the natural tool for the cache-key
  reproducibility/sensitivity *properties* SC-002 is built around. The in-test adapter
  `'fact` is **real evidence**, not a mock — it is exactly the shape F09 will materialise —
  so no `// SYNTHETIC:` disclosure is needed.
- **Alternatives considered**: testing `toRule` only through `FixedPoint.evaluate`
  end-to-end — kept as one integration-style scenario (V18) but not the primary form;
  unit-testing each tier's `Apply` directly is clearer for hit/miss/re-review. Hand-rolled
  ingredient permutation instead of FsCheck for SC-002 — clumsier; rejected (mirrors F02
  D5 / F03 D7).

## Deferred / out of scope (confirmed, not unknowns)

- **The actual agent call, the verdict recording, reading artifact bytes** — **F08** (the
  MVU/effects edge). F04 emits `NeedsReview` as data and reads a `RecordedReview` the edge
  wrote; it never dispatches or records. **Decision #2** (single-sample judge noise —
  aggregate N runs / require a confidence threshold before freezing a verdict) is **noted
  for F08**: F04's per-key single recorded verdict and its cache-key shape are compatible
  with later aggregation (F08 can record a frozen verdict only after N samples agree),
  and F04 does not decide the policy.
- **The `contract` fold (`Rule list -> …`) and `Explanation` JSON serialization** — **F06**.
  F04 guarantees only that a bridged rule's `Description` is the rendered check, so F06's
  contract fold has a non-drifting source.
- **Routing on `Severity`, run modes, the escape hatch** — **F07** (depends on F04). F04
  records `Severity` and emits `RuleOutcome`s; F07 turns a failing `Blocking` verdict into a
  block and an `Uncertain`/`NeedsReview` into a review route.
- **Structured logging** (`TODO(STRUCTURED_LOGGING)`) — no I/O in F04; the bridge emits
  nothing to a log. Choice still deferred to an ADR before F08.
