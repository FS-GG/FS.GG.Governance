# Phase 0 Research: Agent-Reviewed Verdict Store & Invalidation Decision Core

The spec left four home decisions to planning ("new core vs. extend F035", "opaque verdict-reference
representation", "verdict-store representation", and "which prior-entry diff to surface / how to attribute the
cause to an identity group"). This file records those and the supporting facts. There were **no `NEEDS
CLARIFICATION` markers** in the Technical Context; the decisions below close the deferred home questions. The
whole row is the **F030 `EvidenceReuse` design re-applied to F035 `AgentReviewKey`**, so each decision cites its
F030 precedent.

## D1 — New pure core, AgentReviewKey-only dependency (Tier 1)

**Decision**: Add a new packable library `src/FS.GG.Governance.VerdictReuse` that references **only**
`FS.GG.Governance.AgentReviewKey` (F035). It reuses F035's `AgentReviewInputs`, `ReviewInput`,
`AgentReviewKey.matches`, and `AgentReviewKey.diff` verbatim. The F029 `RuleHash`/`ArtifactHash` and the F014
typed facts arrive transitively *through* F035 (modern .NET SDK project references flow transitively for
compile); this core names no `FreshnessKey` or `Config` type beyond reading `request.Check` (an F029 `RuleHash`)
for the work-key equality test, and adds no direct `FreshnessKey`/`Config` project reference.

**Rationale**: This is the literal F030 decision (research D1 there) re-applied one phase later. The Constitution
Engineering Constraint keeps the helper cores minimal and free of git/filesystem scanning; F035 already exposes
the exact `AgentReviewInputs` shape and the `matches`/`diff` operations this decision needs, so F035 is the
smallest sufficient dependency. A new core (rather than extending F035) keeps F035's surface frozen — F035 is
merged, its baseline committed — and keeps each pure core single-purpose (compute-the-key vs. decide-reuse),
matching the F029→F030 split and the F015–F035 one-core-per-row rhythm.

**Alternatives considered**:
- *Extend F035 with `lookup`/`record`.* Rejected: mutates a merged core's surface/baseline (avoidable Tier-1
  churn) and conflates "fingerprint a verdict's identity" with "decide reuse over a store" — two concerns the
  repo has consistently split into separate cores (the F029/F030 precedent).
- *Reference the kernel verdict/finding model.* Rejected: couples this minimal decision core to a heavier model
  and to verdict *content*, which this row deliberately does not inspect (FR-001, FR-011).

## D2 — Validity is exactly F035 `matches`; the explanation is exactly F035 `diff`

**Decision**: `lookup` returns `Valid ref` **iff** some cached entry `AgentReviewKey.matches` the request (i.e.
their F035 keys are byte-equal). The `InputsChanged` cause carries `AgentReviewKey.diff request priorEntry`. No
new notion of partial, weighted, or fuzzy match is introduced.

**Rationale**: The spec is explicit (Assumptions): *"Invalidate cached verdicts when judge identity or prompt
identity changes" is implemented as "Valid iff some cached entry `matches` the request, else Invalidated,"
reusing F035 verbatim.* Because `matches` is true iff **all seven** inputs are equal, any judge / prompt / check
/ artifact change necessarily invalidates — satisfying the design's named guarantee with no bespoke comparison
that could drift from F035's set-semantics for reviewed artifacts. This is F030 research D2 verbatim, one phase
up.

**Alternatives considered**: A bespoke field-by-field comparison inside this core — rejected as duplicated logic
that could drift from F035's reviewed-artifact set-semantics (FR-004 is already guaranteed by `matches`/`diff`).

## D3 — Opaque verdict reference newtype

**Decision**: `type VerdictRef = VerdictRef of string` — a thin, opaque, comparable single-case newtype. The
core carries it back on `Valid` and never parses, validates, produces, or dereferences it, and never reads
whether the verdict is advisory or blocking. An empty or unusual string is a literal value, not an error.

**Rationale**: Mirrors F030's `EvidenceRef` (research D3 there) and, transitively, F029's `Revision`: an
edge-minted token the pure core treats as data. It keeps the "what verdict" payload opaque so this row stays the
*decision* (reuse-or-invalidate), not the *production / persistence / content-reading* of a verdict — those are
the actual-review host edge and the later Phase-12 rows. The spec's reasonable default ("a thin new opaque-string
newtype mirroring F030's `EvidenceRef`") is adopted unchanged.

**Alternatives considered**:
- *Reference a kernel `Verdict`/`Finding` model.* Rejected: couples this minimal decision core to verdict
  *content* (advisory/blocking), which this row must not read (FR-001, FR-011).
- *Make the core generic over the payload type.* Rejected (Principle III): a concrete opaque newtype is the
  plainest thing that works; generics buy nothing for a carried-through token and complicate the surface.

## D4 — The verdict store is an ordered list, newest-first

**Decision**: `type VerdictStore = VerdictStore of CachedVerdict list`, with
`CachedVerdict = { Inputs: AgentReviewInputs; Verdict: VerdictRef }`. `record` **prepends** the new entry after
filtering out any prior entry that `AgentReviewKey.matches` the new inputs (so the head is most-recent and there
is at most one entry per matching-input class). `lookup` scans **head-first** (`List.tryFind`), so when several
entries could match, the most-recently-recorded one wins, deterministically — even for a hand-built store that
happens to contain duplicates.

**Rationale**: Determinism, not lookup latency, is the contract (the store is small — a handful of verdicts per
check; spec Assumptions: *"Determinism is the contract, not performance"*). A list is the plainest representation
(Principle III), keeps the store trivially inspectable (`entries`), and avoids forcing F035 `CacheKey`
computation into the store layer. Prepend + head-first scan gives an obvious, total "most-recent-wins". This is
F030 research D4 verbatim.

**Alternatives considered**:
- *`Map<CacheKey, CachedVerdict>` keyed by `AgentReviewKey.compute`.* The spec calls this "the natural default."
  Rejected here for the same reason F030 rejected the `Map`-keyed-by-`Key` store: it forces key computation into
  the store layer, makes "most-recent-wins" implicit in map-replace semantics, and is an optimization the scale
  doesn't need. (A later persistence row may choose an indexed representation; this pure decision core does not.)
- *Append (oldest-first) + `List.tryFindBack`.* Equivalent in behavior; prepend + `tryFind` reads more directly
  as "newest first".

## D5 — "A cached verdict for the request's work" = same **check hash** (the work key)

**Decision**: When no entry fully matches, the `InvalidationCause` is selected as:
- if some entry has **`Inputs.Check = request.Check`** (same F029 `RuleHash` — the rule/check under review), take
  the most-recent such entry `e` and return `InputsChanged (AgentReviewKey.diff request e.Inputs)`;
- otherwise return `NoCachedVerdict`.

Because this branch is reached only when **no** entry fully matches, the surfaced `diff` is always **non-empty**;
and because the chosen `e` shares the request's `Check`, the `diff` **never** contains `CheckHashInput` — it names
exactly the non-work inputs that changed (e.g. `[ModelVersionInput]`, `[PromptHashInput]`,
`[ReviewedArtifactsInput]`, `[QuestionTextInput]`).

**Rationale**: This resolves the spec's two deferred questions at once — "which prior entry's diff to surface" and
how the two invalidation causes are kept crisply distinct — using the **check hash** as the "same work" identity.
It is the precise F036 analogue of F030 D5 (where "same work" = the F018 `GateId` = Check + Domain): F035 has a
single check hash and no separate domain, so the rule's check hash *is* the work key. `CheckHashInput` is to F036
what `CheckIdentity`/`DomainIdentity` are to F030 — the work-identity key, equal by construction for the chosen
prior entry and therefore never appearing inside an `InputsChanged` diff. The no-hide requirement (FR-006) is
satisfied: every invalidation has a located, non-ambiguous cause — `NoCachedVerdict` ("never cached a verdict for
this rule") vs `InputsChanged` ("cached a verdict for this rule, but an identity input moved").

**The `check / question` wording is resolved in favour of the work key being `Check` alone (NOT `Check +
Question`).** The spec's Edge Case says *"No entry shares the request's check / question … no cached verdict for
this work,"* but its **testable** Story 2 AS#2 requires that an entry differing **only in question text** be
`Invalidated` with the cause **attributable to prompt identity** — i.e. `InputsChanged [QuestionTextInput]`, not
`NoCachedVerdict`. Folding `Question` into the work key would force a question-only change to `NoCachedVerdict`
and violate AS#2. Therefore `Question` is a *prompt-identity diff within the same work*, not a different work; the
work key is the **check hash only**. (Symmetrically, a reviewed-artifact change is a *check/artifact diff within
the same work*, surfaced as `[ReviewedArtifactsInput]`, not a different work.)

**Alternatives considered**:
- *Use the full seven-input identity as "same work".* Rejected: judge / prompt / artifact inputs legitimately
  change between reviews of the same rule; folding them into "same work" would misclassify a model-version bump
  as "no cached verdict" and defeat the entire invalidation-attribution story (US2).
- *Use `Check + Question` (the literal "check / question" reading).* Rejected: breaks Story 2 AS#2 as shown
  above; a question-text change must attribute to prompt identity, so `Question` cannot gate "same work".
- *Report the diff against every near-miss entry.* Rejected: noisier than needed; the most-recent same-check
  entry is the relevant "last cached verdict for this rule" (FR-006 requires *a* located cause, not all of them).
- *Return `InputsChanged []` when no same-check entry exists.* Rejected: an empty list is the "they match" signal
  (`diff = [] ⇔ matches`); reusing it for "nothing cached" would be ambiguous. A distinct `NoCachedVerdict` case
  keeps the negative answer honest (Principle VI).

## D6 — Identity-group attribution via a total `inputGroup` projection

**Decision**: Add `type IdentityGroup = JudgeIdentity | PromptIdentity | CheckArtifactIdentity` and a total
`inputGroup : ReviewInput -> IdentityGroup`:

| `ReviewInput` | `IdentityGroup` |
|---|---|
| `ModelIdInput`, `ModelVersionInput`, `ModelConfigInput` | `JudgeIdentity` |
| `PromptHashInput`, `QuestionTextInput` | `PromptIdentity` |
| `CheckHashInput`, `ReviewedArtifactsInput` | `CheckArtifactIdentity` |

The `InvalidationCause.InputsChanged` payload stays expressed in F035's `ReviewInput` vocabulary (FR-006); the
grouping is a separate pure projection an auditor / test applies to attribute a change to judge, prompt, or
check/artifact identity.

**Rationale**: SC-002 and US2 require that a judge change and a prompt change each be *visible as such*. Keeping
the cause in F035's `ReviewInput` vocabulary honours FR-006's "expressed using F035's `diff` input vocabulary"
while making attribution a first-class, total, tested function — the exact analogue of F035's own
`inputToken : ReviewInput -> string`. The grouping table is the spec's Key-Entities grouping verbatim (judge =
model id/version/config; prompt = prompt hash/question; check-artifact = check hash/reviewed artifacts).
`inputGroup` is total over all seven cases even though `CheckHashInput` cannot appear inside an `InputsChanged`
diff (D5) — a total projection over the closed enumeration is plainer than a partial one.

**Alternatives considered**:
- *Bake the group into the cause DU (e.g. `InputsChanged of (ReviewInput * IdentityGroup) list`).* Rejected:
  redundant — the group is a pure function of the input — and it would diverge the cause vocabulary from F035's
  `diff` (FR-006 wants the F035 vocabulary). A separate `inputGroup` keeps the cause minimal and the mapping
  reusable.
- *Emit a `JudgeOrPromptChanged` boolean.* Rejected: lossy and less auditable than naming the exact inputs and
  letting `inputGroup` attribute each; an auditor wants "which input", not just "judge-or-prompt: yes".

## Supporting facts

- **F035 surface consumed** (verbatim, from `surface/FS.GG.Governance.AgentReviewKey.surface.txt`):
  `AgentReviewInputs` (record, seven inputs), `ReviewInput` (7-case DU),
  `AgentReviewKey.matches : AgentReviewInputs -> AgentReviewInputs -> bool`,
  `AgentReviewKey.diff : AgentReviewInputs -> AgentReviewInputs -> ReviewInput list`, and `Model.inputToken`.
  `compute`/`CacheKey`/`value` are available but not required by this core (the decision uses `matches`/`diff`
  directly, exactly as F030 used F029's `matches`/`diff` and not `compute`).
- **Set semantics inherited**: because validity uses `matches` and the explanation uses `diff`, reviewed-artifact
  order and duplication never affect a decision — F035 already proves this (FR-004); SC-004 here is a thin
  re-assertion at the decision layer.
- **Totality**: `lookup` and `record` are total — `List.tryFind`/`List.filter`/cons never throw, and every
  `AgentReviewInputs`/`VerdictRef`/`VerdictStore` is a valid input (FR-003, FR-012). The empty store and an
  empty/unusual `VerdictRef` are ordinary values.
- **Purity / scope guard**: the assembly references only `FSharp.Core`, `FS.GG.Governance.AgentReviewKey`,
  `FS.GG.Governance.FreshnessKey` (transitive), `FS.GG.Governance.Config` (transitive), and the BCL — pinned by
  the `SurfaceDrift` scope-hygiene test (the F029/F030/F035 precedent), which also forbids
  `Gates`/`Snapshot`/`Route`/`Routing`/`Findings`/host/adapters/CLI.
- **No new third-party dependency** (FR-014): the decision is plain list/`option` handling + `FSharp.Core`; the
  transitive `YamlDotNet` arriving via `Config` (through F035→F029) is unused.
