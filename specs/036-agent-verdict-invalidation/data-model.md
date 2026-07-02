# Phase 1 Data Model: Agent-Reviewed Verdict Store & Invalidation Decision Core

All types live in `FS.GG.Governance.VerdictReuse.Model` (sole public declaration: `Model.fsi`). They are
product-neutral, comparable values carrying no raw bytes, host paths, clock readings, verdict content, or product
vocabulary. The F035 agent-review vocabulary (`AgentReviewInputs`, `ReviewInput`) is `open`ed from
`FS.GG.Governance.AgentReviewKey.Model`; nothing in `AgentReviewKey`/`FreshnessKey`/`Config` is modified
(FR-010). Names are the recommended spelling; minor identifier adjustments at implementation are allowed as long
as the contracts in `contracts/` hold.

## Reused vocabulary (from `AgentReviewKey.Model` / `AgentReviewKey`, F035 — verbatim)

| Type / operation | Form | Role in this feature |
|---|---|---|
| `AgentReviewInputs` | record (7 inputs) | The judge / prompt / check / artifact identity a verdict was produced under, and the new request's identity. Compared with F035 `matches`/`diff`. |
| `ReviewInput` | 7-case DU | The no-hide vocabulary returned inside `InvalidationCause.InputsChanged` (via F035 `diff`); grouped by `inputGroup`. |
| `AgentReviewKey.matches` | `AgentReviewInputs -> AgentReviewInputs -> bool` | The validity test: `Valid` iff some entry matches the request. |
| `AgentReviewKey.diff` | `AgentReviewInputs -> AgentReviewInputs -> ReviewInput list` | The differing-input list for the `InputsChanged` cause. |

This core defines **no new comparison** over `AgentReviewInputs`; `matches`/`diff` are consumed verbatim.

## New opaque newtype (this feature)

Single-case `of string`, opaque and comparable; the actual reference is minted at the edge and supplied as data
(FR-001). No validation, no parsing, no dereference, no content reading — an empty string is a literal value
(FR-012).

| Type | Form | Represents |
|---|---|---|
| `VerdictRef` | `VerdictRef of string` | A handle to an *already-cached* agent-reviewed verdict (e.g. a content-addressed pointer / recorded-verdict id). Carried back on `Valid`; never interpreted by this core, never read for advisory/blocking content. |

## Key entity — `CachedVerdict`

One cached entry: the F035 seven-input identity a verdict was produced under, paired with its opaque reference
(FR-001).

```text
CachedVerdict =
    { Inputs:  AgentReviewInputs   // the judge/prompt/check/artifact identity this verdict was produced under (F035)
      Verdict: VerdictRef }        // the opaque handle to that cached verdict
```

## Key entity — `VerdictStore`

The immutable collection of cached entries — the supplied, in-value "what has been cached so far" (FR-002). A
single-case DU over a list; **newest-first** by `record` convention (research D4). Not a live cache, connection,
or file.

```text
VerdictStore = VerdictStore of CachedVerdict list
```

- `empty : VerdictStore` — the `VerdictStore []` starting value.
- Invariant maintained by `record` (not enforced on hand-built values): **at most one entry per matching-input
  class** (FR-008). `lookup` is still total and deterministic even if this invariant is violated by a hand-built
  store (head-first scan ⇒ most-recent wins).

## Key entity — `IdentityGroup` + `inputGroup` (the attribution projection)

The three identity groups a changed input is attributed to, and the total projection from a `ReviewInput` to its
group (research D6). Lets US2 see a judge change and a prompt change *each as such*.

```text
IdentityGroup =
    | JudgeIdentity          // model id, model version, model configuration
    | PromptIdentity         // reviewer prompt hash, question text
    | CheckArtifactIdentity  // check hash, reviewed-artifact hashes

inputGroup : ReviewInput -> IdentityGroup     // total; the table below
```

| `ReviewInput` | `inputGroup` |
|---|---|
| `ModelIdInput` | `JudgeIdentity` |
| `ModelVersionInput` | `JudgeIdentity` |
| `ModelConfigInput` | `JudgeIdentity` |
| `PromptHashInput` | `PromptIdentity` |
| `QuestionTextInput` | `PromptIdentity` |
| `CheckHashInput` | `CheckArtifactIdentity` |
| `ReviewedArtifactsInput` | `CheckArtifactIdentity` |

> `inputGroup` is total over all seven cases even though `CheckHashInput` cannot appear inside an `InputsChanged`
> diff (it is the work key, equal by construction for the chosen prior entry — research D5).

> **Implementation note (M-ADPT-2, 2026-07-02 review).** `ReviewedArtifactsInput` is only real if the F04
> in-run cache key (`CheckRule.cacheKey`, whose artifact half is `Check.reads check |> map ArtifactHash`)
> actually carries the reviewed artifacts — otherwise a changed `plan.md` leaves both the F04 key AND this
> `CheckArtifactIdentity` group unmoved, and a stale verdict is reused. An `AgentReviewed` rule over a bare
> `Opaque` judgement declares **no** reads, so its artifact half was empty. The SpecKit adapter now closes the
> split: `plan-satisfies-spec` / `tasks-complete-ordered` wrap their `Opaque` in `SpecKit.reviewing [...]`,
> declaring the reviewed artifacts (`plan`↔`spec`, `tasks`↔`plan`) as `Check.reads`; the Host loop senses
> their content, and the composition-root bridge (`Cli/Project.fs`) folds each content hash in — so "reviewed
> artifact changed" is now the *same* event in both the F04 key and this F036 group. The DesignSystem adapter
> is converted the same way via `DesignSystem.reviewing [...]`: its judgement rules name design *qualities*
> rather than one artifact, so — the design language being a flat surface — each reviews `RenderedCapture`
> (`rendered-capture.json`, the common subject) plus the relevant spec for the structural checks
> (`rendered-matches-intent` → `InteractionStateSpec`, `page-pattern` → `PagePatternSpec`). Adding
> per-quality artifacts later would only refine that mapping, not change the mechanism. The remaining honesty
> gap is the adapter-local `Bridge.ArtifactHash = ""` stubs, which are dead on the CLI path (superseded by the
> composition-root bridge) and would need a content-bearing fact case to be made real — tracked under review
> item #50 / M-ADPT-2.

## Key entity — `InvalidationCause` (the no-hide explanation)

Why no cached verdict served — always present and locatable (FR-006, Principle VI).

```text
InvalidationCause =
    | NoCachedVerdict                    // no entry shares the request's check hash (the work key)
    | InputsChanged of ReviewInput list  // a cached verdict for this check exists; these inputs changed
```

- `InputsChanged` always carries a **non-empty** list (it is produced only when no entry fully matches), and it
  **never** contains `CheckHashInput` (the work key, equal for the chosen prior entry by construction — research
  D5). It may carry any of the other six inputs, attributable via `inputGroup`.
- The two cases are crisply distinct: `NoCachedVerdict` ("never cached a verdict for this rule") vs
  `InputsChanged` ("cached one, but an identity input moved"). The empty list is deliberately *not* a valid
  `InputsChanged` payload — an all-inputs-agree situation is a `Valid`, never an `Invalidated`.

## Key entity — `LookupDecision`

The total result of `lookup` (FR-003).

```text
LookupDecision =
    | Valid of VerdictRef             // some entry matched on every input — reuse this verdict
    | Invalidated of InvalidationCause // no entry matched — here is why
```

## Relationships / data flow

```text
request: AgentReviewInputs ─┐
                            ├─ VerdictReuse.lookup ─▶ Valid VerdictRef
store:   VerdictStore ──────┘                       └ Invalidated (NoCachedVerdict | InputsChanged [..])

inputs:  AgentReviewInputs ─┐
verdict: VerdictRef ────────┼─ VerdictReuse.record ─▶ VerdictStore'  (prior full-match removed, new entry at head)
store:   VerdictStore ──────┘
```

- `lookup` uses `AgentReviewKey.matches` for the validity test and `AgentReviewKey.diff` for the `InputsChanged`
  payload; the `NoCachedVerdict` vs `InputsChanged` split keys off `Check` (the F029 `RuleHash`) equality
  (research D5). The `InputsChanged` inputs are attributed to identity groups via `inputGroup` (research D6).
- `record` uses `AgentReviewKey.matches` to drop a superseded entry, then conses the new entry (research D4).
- Both are pure over their supplied values: no clock, filesystem, git, environment, or network (FR-009).

## Validation / totality rules

| Rule | Source |
|---|---|
| Every `AgentReviewInputs`, `VerdictRef`, and `VerdictStore` is a valid input; no value throws. | FR-003, FR-012 |
| `lookup` is `Valid` iff some entry `matches` the request on every input; else `Invalidated`. | FR-004 |
| On `Valid`, the carried `VerdictRef` is from a matching entry; with duplicates, the most-recent (head-first). | FR-005 |
| Every `Invalidated` carries a located cause (`NoCachedVerdict` or non-empty `InputsChanged`). | FR-006 |
| `InputsChanged` never contains `CheckHashInput`; each element is attributable via `inputGroup`. | FR-006, D5/D6 |
| `record` does not mutate its input store; result holds ≤1 entry per matching-input class. | FR-007, FR-008 |
| Reviewed-artifact order/duplication never changes a decision (inherited from F035 `matches`/`diff`). | FR-004, SC-004 |
