# Phase 1 Data Model: CheckTier & Rule Bridge (F04 · `004-checktier-rule-bridge`)

The full typed shapes are the public contract in
[`contracts/CheckRule.fsi`](./contracts/CheckRule.fsi). This document records each
entity's meaning, the `rule`/`cacheKey`/`toRule` rules, and the invariants the
implementation and semantic tests must uphold. Entities map directly to the spec's Key
Entities and `docs/governance-design/rule-edsl.md` / `kernel.md`.

## Entities

### `CheckTier` — `Deterministic | AgentReviewed | HumanOnly`
WHO is competent to decide a rule (kernel.md §CheckTier). Orthogonal to `Severity`.
`Deterministic` requires a fully-reified check (enforced by `rule`, FR-006);
`AgentReviewed`/`HumanOnly` accept any check (FR-001).

### `Severity` — `Advisory | Blocking`
HOW BADLY a failure matters. Orthogonal to `CheckTier`; defaults to `Advisory` (FR-002).
A routing concern (F07), not a decision concern — `HumanOnly` escalates regardless (FR-010).

### `SpecSource` — `{ Document: string; Section: string }`
A stable, structural handle to the authoritative requirement a rule enforces (FR-003).
Renderable/hashable; the algebra never interprets its content. Feeds provenance and the
F06 contract fold. Exact fields are a design detail (research D2).

### `JudgeId` — `{ ModelId: string; Version: string }`
The stochastic judge's identity, folded into the cache key (decision #1, FR-011). The
reviewer **prompt** is *not* here — it is the rule's `Question`, so the prompt half of the
key is per-rule while the model identity is per-run config (research D2/D4).

### `ReviewRequest` — `{ Rule: RuleId; Question: string option; Key: string }`
Emitted on an `AgentReviewed` cache MISS: a typed request for F08 to dispatch (FR-009,
FR-014). The bridge produces it but never acts on it (Principle IV: I/O as data).

### `RecordedReview` — `{ Rule: RuleId; Key: string; Verdict: Verdict }`
A frozen agent verdict recorded by F08 against its cache `Key`. `toRule` recognises one
(via `Bridge.Project`) to short-circuit a cache HIT (FR-009, FR-014).

### `RuleOutcome` — `Decided of RuleId * Verdict | NeedsReview of ReviewRequest | Reviewed of RecordedReview | Escalated of RuleId`
The domain-neutral governance payload a bridged rule asserts each run (FR-014, FR-015).
An adapter embeds it into its `'fact` (`Bridge.Embed`) and projects it back
(`Bridge.Project`); F07/F08 match it to route and dispatch. `toRule` **emits**
`Decided`/`NeedsReview`/`Escalated`; `Reviewed` is written by the F08 edge and only
**read** by `toRule`'s cache-hit lookup (matched on `Key`), never emitted by it.

### `CheckRule<'fact>` — `{ Id; Tier; Spec; Severity; Check: Check<'fact>; Question: string option }`
The authored governance rule (FR-004). DISTINCT from the kernel's executable `Rule<'fact>`;
`toRule` translates this into that. Named `CheckRule` to avoid the clash with the
already-shipped `Rule<'fact>` (research D1). Built via `rule`/`blocking`/`asking`, not by
hand, so the FR-006 guardrail cannot be bypassed.

### `RuleRejection` — `OpaqueCannotBeDeterministic of RuleId`
Why authoring was refused. The only refusal is the reified-ness guardrail (FR-006, SC-001).

### `Bridge<'fact>` — `{ Judge; ArtifactHash; Embed; Project }`
The caller-supplied bridge between the domain-neutral `RuleOutcome` and an adapter's `'fact`
vocabulary, plus the judge identity and the artifact-content lookup (research D6, FR-015).
`ArtifactHash : FactSet<'fact> -> ArtifactRef -> string` reads a content hash FROM the facts
(no live I/O); `Embed : RuleOutcome -> 'fact` lifts; `Project : 'fact -> RuleOutcome option`
lowers.

## Constructor & function rules (behavioural contract)

### `rule id tier spec check : Result<CheckRule<'fact>, RuleRejection>` — FR-006
Sets `Severity = Advisory`, `Question = None`. Refusal table:

| Tier | `Check.isReified check` | Result |
|---|---|---|
| `Deterministic` | `false` (has `Opaque`) | `Error (OpaqueCannotBeDeterministic id)` |
| `Deterministic` | `true` | `Ok { … Tier = Deterministic … }` |
| `AgentReviewed` | any | `Ok { … }` |
| `HumanOnly` | any | `Ok { … }` |

### `blocking r : CheckRule<'fact>` / `asking prompt r : CheckRule<'fact>` — FR-005
`blocking` sets `Severity = Blocking` (tier unchanged). `asking prompt` sets
`Tier = AgentReviewed` and `Question = Some prompt` (so it accepts any check, never trips
FR-006). Both are post-construction modifiers, composing under `Result.map`.

### `cacheKey judge checkHash artifactHashes question : string` — FR-011/FR-012 (decision #1)
Pure SHA-256 hex over a **prefix-free** pre-image (each component hashed to fixed-width hex
first), combining in fixed order:

| Position | Component | Treatment |
|---|---|---|
| 1 | `judge.ModelId` | verbatim |
| 2 | `judge.Version` | verbatim |
| 3 | `checkHash` (`Check.hash`) | verbatim |
| 4 | `artifactHashes` | **de-duplicated + ordinal-sorted** (order-independent — F04 policy) |
| 5 | `question` (reviewer prompt) | hash of the string; fixed sentinel for `None` |

Identical ingredients → identical key; any one of the five changing → a different key
(SC-002). Ordinal sort is culture-invariant (same discipline as F02/F03).

### `toRule bridge rule : Rule<'fact>` — FR-007 … FR-010
Returns `{ Id = rule.Id; Description = Check.render rule.Check; Apply = … }`. `Apply facts`
by tier:

| Tier | Emits |
|---|---|
| `Deterministic` | `[ embed [] (Decided (rule.Id, Check.eval facts rule.Check)) ]` (verdict not coerced) |
| `AgentReviewed`, cache **hit** | `[ embed [fid] (Decided (rule.Id, v)) ]` where `(fid, v)` is the recorded review's fact id and verdict for `key`; no request |
| `AgentReviewed`, cache **miss** | `[ embed [] (NeedsReview { Rule = rule.Id; Question = rule.Question; Key = key }) ]` (exactly one) |
| `HumanOnly` | `[ embed [] (Escalated rule.Id) ]` (regardless of severity) |

where `key = cacheKey bridge.Judge (Check.hash rule.Check) (Check.reads rule.Check |>
List.map (bridge.ArtifactHash facts)) rule.Question`; the cache-hit verdict **and the
recorded review's fact id** are found together by
`facts |> List.tryPick (fun f -> match bridge.Project f.Value with Some (Reviewed r) when
r.Key = key -> Some (f.Id, r.Verdict) | _ -> None)`; and `embed inputs outcome = { Id =
<placeholder overridden by identify>; Value = bridge.Embed outcome; Provenance = [ { Rule =
rule.Id; Inputs = inputs; Note = Check.render rule.Check } ] }`.

**Provenance `Inputs` (FactId list).** `ProvenanceStep.Inputs` is a `FactId list`, but the
domain-neutral `Bridge` cannot resolve an `ArtifactRef` to the `FactId` of the
artifact-content fact that carries it — `ArtifactHash` yields a content-hash *string* (not
an identity) and `Project` recognises only `RuleOutcome`s. Having the kernel itself scan and
interpret an adapter's artifact facts to recover their ids would defeat FR-015's
domain-neutrality. So:

| Tier / case | `Inputs` |
|---|---|
| `Deterministic` | `[]` (artifact-read fact ids are not resolvable through the `Bridge`) |
| `AgentReviewed`, cache **hit** | `[ f.Id ]` — the `FactId` of the matching `RecordedReview`, captured in the lookup above |
| `AgentReviewed`, cache **miss** | `[]` |
| `HumanOnly` | `[]` |

This satisfies US1 AS1 ("justified by the rule's identity" = `Provenance.Rule = rule.Id`,
which always holds) and the cache-hit case additionally records the recorded-review input it
actually consumed. Recording artifact-**read** provenance is **deferred**: a later feature
MAY add a `Bridge` resolver (`ArtifactRef -> FactId list`) if downstream explanation (F06)
needs it; F04 does not, and keeps the `Bridge` surface minimal (SC-009, Principle III).

## Invariants checked by semantic tests

| # | Invariant | Spec ref |
|---|-----------|----------|
| INV-1 | `rule id Deterministic spec c = Error (OpaqueCannotBeDeterministic id)` ⟺ `not (Check.isReified c)`; every other tier and reified-Deterministic ⇒ `Ok` | FR-006, SC-001 |
| INV-2 | `cacheKey` identical for identical ingredients; differs when ANY of {model id, version, check hash, an artifact hash, prompt} differs | FR-011/012, SC-002 |
| INV-3 | `cacheKey` artifact half is order- and duplicate-independent (permuting/duplicating `artifactHashes` does not change the key) | FR-012, US2 edge |
| INV-4 | `AgentReviewed` `Apply` with a matching `RecordedReview` present ⇒ emits `Decided`, zero `NeedsReview`, zero agent calls | FR-009, SC-003 |
| INV-5 | `AgentReviewed` `Apply` with no match ⇒ emits exactly one `NeedsReview` carrying `key` | FR-009, SC-003 |
| INV-6 | changing `JudgeId` (model id/version) or the prompt ⇒ a previously-matching recorded verdict no longer matches ⇒ fresh `NeedsReview` (re-review) | FR-013, SC-004 |
| INV-7 | `Deterministic` `Apply` emits `Decided (id, v)` where `v = Check.eval facts check`, `v` never coerced (incl. `Uncertain`) | FR-008, SC-005 |
| INV-8 | `(toRule bridge r).Description = Check.render r.Check` for every rule | FR-007, SC-006 |
| INV-9 | `HumanOnly` `Apply` emits `Escalated id` whether `Severity` is `Advisory` or `Blocking`; `blocking` flips severity, leaves tier; severity & tier vary independently | FR-002/010, SC-008 |
| INV-10 | `toRule` and every bridged `Apply` are **total** — no rule (any tier, any check incl. empty `All`/`Any`), no fact set (incl. empty), no unknown artifact throws or returns a partial | FR-017, SC-007 |
| INV-11 | `toRule` performs no agent call and no I/O; the artifact hashes come only from `bridge.ArtifactHash` over the supplied facts | FR-015 |

## Edge-case mapping (from spec)

| Edge case | Behaviour |
|-----------|-----------|
| Deterministic check evaluating to `Uncertain` | `Apply` emits `Decided (id, Uncertain r)` — not coerced (INV-7) |
| Empty read set | `Check.reads = []` ⇒ `artifactHashes = []`; `cacheKey` still stable, still varies with check hash + judge (INV-2) |
| `HumanOnly` is severity-independent | `Escalated` emitted for Advisory and Blocking alike (INV-9) |
| Re-review on judge change | a `RecordedReview` under an old `JudgeId`/prompt has an old `Key` ⇒ no match ⇒ fresh `NeedsReview` (INV-6) |
| Cache hit short-circuits the agent | on a hit, only `Decided` is emitted; no `NeedsReview`, so F08 dispatches nothing (INV-4) |
| Bridging is total | any tier, any check (incl. empty combinators), any facts ⇒ `Apply` returns facts, never throws (INV-10) |
| Single-sample judge noise | OUT OF SCOPE — F04 records one verdict per key; aggregation/confidence is F08 (decision #2, research deferred) |
| Unknown artifact in `Check.reads` | `bridge.ArtifactHash` returns its sentinel (e.g. `""`); `cacheKey` stays total (INV-10) |
