# Contract: Lookup Decision & Record Semantics

The decision tables that fix `lookup` and `record` behavior. These are the precise rules the example and
property tests assert; the API signatures live in [verdict-store-api.md](./verdict-store-api.md).

Throughout: `matches` = `AgentReviewKey.matches`, `diff` = `AgentReviewKey.diff`, "same work" = the request and
an entry agree on the **check hash** (`Inputs.Check = request.Check`, the F029 `RuleHash` of the rule under
review — research D5; the F036 analogue of F030's `GateId`).

## `lookup request store` — the invalidation decision

Let `es` be the store's entries, newest-first.

```text
1. full match?   m = es |> List.tryFind (fun e -> matches request e.Inputs)
                 ├─ Some e  ⇒  Valid e.Verdict
                 └─ None    ⇒  go to step 2
2. same work?    g = es |> List.tryFind (fun e -> e.Inputs.Check = request.Check)
                 ├─ Some e  ⇒  Invalidated (InputsChanged (diff request e.Inputs))
                 └─ None    ⇒  Invalidated NoCachedVerdict
```

Properties this guarantees:

- Step 2 is reached only when **no** entry fully matches, so the `g` found there is necessarily a partial match
  ⇒ `diff request g.Inputs` is **non-empty**.
- Because `g` shares the check hash with the request, that `diff` **never** contains `CheckHashInput` — it lists
  exactly the non-work inputs that moved (any of the other six), each attributable via `inputGroup` to
  `JudgeIdentity` / `PromptIdentity` / `CheckArtifactIdentity`.
- `tryFind` is head-first and the store is newest-first ⇒ both the reused reference (step 1) and the explained
  prior entry (step 2) are the **most-recently-recorded** qualifying entry, deterministically.

### Worked decision table (store recorded in the order shown, so newest-first = bottom-up)

Base request `R` = a literal `AgentReviewInputs` (e.g. `Model=ModelId "claude"`, `ModelVersion="v1"`,
`PromptHash="p1"`, `Config="c1"`, `Check=RuleHash "chk:rule"`, `ReviewedArtifacts=[ArtifactHash "a1"]`,
`Question="q1"`). Entries paired with verdict references `V1`, `V2`, …:

| Store contents (newest-first) | `lookup R store` |
|---|---|
| `[]` | `Invalidated NoCachedVerdict` |
| `[ {R, V1} ]` | `Valid V1` |
| `[ {R with ModelVersion="v2", V2}; {R, V1} ]` | `Valid V1` (the second entry fully matches) |
| `[ {R with ModelVersion="v2", V2} ]` | `Invalidated (InputsChanged [ModelVersionInput])` (same work, judge moved) |
| `[ {R with PromptHash="p2", V3} ]` | `Invalidated (InputsChanged [PromptHashInput])` (prompt identity) |
| `[ {R with Question="q2", V3b} ]` | `Invalidated (InputsChanged [QuestionTextInput])` (prompt identity — NOT NoCachedVerdict) |
| `[ {R with ReviewedArtifacts=[ArtifactHash "a2"], V3c} ]` | `Invalidated (InputsChanged [ReviewedArtifactsInput])` (check/artifact identity) |
| `[ {R with Config="c2", ModelVersion="v2", V4} ]` | `Invalidated (InputsChanged [ModelVersionInput; ModelConfigInput])` |
| `[ {R with Check=RuleHash "chk:other", V5} ]` | `Invalidated NoCachedVerdict` (different work — check hash differs) |
| `[ {R with ModelVersion="v2", V2b}; {R with ModelVersion="v2", V2a} ]` | `Invalidated (InputsChanged [ModelVersionInput])` *(non-match; both share work; diff identical either way)* |

> The `InputsChanged` input order follows F035's fixed `diff` order
> (`ModelIdInput`, `ModelVersionInput`, `PromptHashInput`, `ModelConfigInput`, `CheckHashInput`,
> `ReviewedArtifactsInput`, `QuestionTextInput`) — so `[ModelVersionInput; ModelConfigInput]`, never the reverse.
> `CheckHashInput` never appears (it is the work key, equal for the chosen prior entry).

### Identity-group attribution (`inputGroup`, research D6)

For an `Invalidated (InputsChanged inputs)`, mapping each element through `inputGroup` yields the changed
identity groups:

| Differing input(s) | Groups via `inputGroup` |
|---|---|
| `[ModelIdInput]` / `[ModelVersionInput]` / `[ModelConfigInput]` | `JudgeIdentity` |
| `[PromptHashInput]` / `[QuestionTextInput]` | `PromptIdentity` |
| `[ReviewedArtifactsInput]` | `CheckArtifactIdentity` |
| `[ModelVersionInput; QuestionTextInput]` | `{ JudgeIdentity; PromptIdentity }` |

This is the observable face of the design's *"a judge or prompt change invalidates prior cached verdicts"*: a
judge change ⇒ at least one `JudgeIdentity` input; a prompt change ⇒ at least one `PromptIdentity` input (SC-002).

## `record inputs verdict store` — the de-duplicating insert

```text
let (VerdictStore es) = store
let kept = es |> List.filter (fun e -> not (matches inputs e.Inputs))   // drop superseded full-match
VerdictStore ({ Inputs = inputs; Verdict = verdict } :: kept)           // new entry at the head (newest)
```

Properties this guarantees:

- **No mutation** (FR-007): `store` is unchanged; a new value is returned.
- **At most one entry per matching-input class** (FR-008): any prior entry that `matches inputs` is dropped
  before the new one is consed. (Entries that merely share the work but differ in some input are *kept* — they
  are verdicts for a different judge/prompt/artifact world of the same rule.)
- **Most-recent-wins** (FR-008, FR-005): the just-recorded entry is at the head, so a later `lookup` for a
  matching request returns *its* reference.
- **Independence** (FR-005): recording under inputs that match nothing leaves every prior entry in place and
  individually reusable.

### Worked record table

| Start store | Operation | Resulting store (newest-first) | A later `lookup` of matching request |
|---|---|---|---|
| `empty` | `record R V1` | `[ {R,V1} ]` | `Valid V1` |
| `[ {R,V1} ]` | `record R V2` (same inputs) | `[ {R,V2} ]` (V1 superseded, no dup) | `Valid V2` |
| `[ {R,V1} ]` | `record (R with ModelVersion="v2") V2` | `[ {R/v2,V2}; {R,V1} ]` | `R ⇒ Valid V1`; `R/v2 ⇒ Valid V2` |

## Degenerate / edge behavior (all total, no error)

| Case | Behavior |
|---|---|
| Empty store | `lookup _ empty = Invalidated NoCachedVerdict`. |
| Empty / unusual `VerdictRef` string | Carried verbatim; never parsed, rejected, or read for content. |
| Multiple full-match entries in a hand-built store | `lookup` returns the head-most (most-recent) entry's reference, deterministically. |
| Request with reordered/duplicated `ReviewedArtifacts` | Same decision (inherited from F035 `matches`/`diff`). |
| Empty `ReviewedArtifacts` set | Ordinary value; an artifact change to/from empty is a `ReviewedArtifactsInput` diff. |
| Question-text-only change | `Invalidated (InputsChanged [QuestionTextInput])` — attributable to prompt identity, **not** `NoCachedVerdict` (research D5). |
| No entry shares the request's check | `Invalidated NoCachedVerdict` (distinct from `InputsChanged`). |
