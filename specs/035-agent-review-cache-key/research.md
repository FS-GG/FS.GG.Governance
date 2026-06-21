# Phase 0 Research: Agent-Review Verdict Cache-Key Core (F035)

This row had **no `NEEDS CLARIFICATION`** in the Technical Context — the stack (F#/.NET `net10.0`, Expecto +
FsCheck, BCL-only), the architecture (a pure, total core with two `.fsi` files + a surface baseline), and the
behavior (the seven keyed inputs, the byte-stable injective key, `compute`/`matches`/`diff`, determinism,
set-semantics for the artifact hashes) are all fixed by the spec and the F015–F034 precedent — and most sharply
by F029 `FreshnessKey`, the direct analogue. Research therefore consolidates the **planning decisions the spec
deferred to `/speckit-plan`** (Spec Assumptions) into the form: Decision / Rationale / Alternatives.

## D1 — Module home: a new pure core referencing exactly one sibling core (`FreshnessKey`)

**Decision.** Land a new packable library `src/FS.GG.Governance.AgentReviewKey` (`Model.fsi/fs` +
`AgentReviewKey.fsi/fs`), referencing **only `FS.GG.Governance.FreshnessKey`** to reuse F029's `RuleHash` (check
hash) and `ArtifactHash` (reviewed-artifact hashes) verbatim. No new third-party `PackageReference`; BCL +
`FSharp.Core` only.

**Rationale.** The spec Assumptions endorse a new minimal core (*"the established rhythm suggests a new minimal
core"*), continuing the F015–F034 one-new-core-per-row cadence and mirroring how F029 (`FreshnessKey`) landed the
deterministic-evidence key as its own core before F030 consumed it. FR-008 mandates reusing the established
**artifact-hash** vocabulary verbatim for the reviewed-artifact hashes; that type (`ArtifactHash`) lives in
`FreshnessKey.Model`, so the new core references that one project to consume it without redefining. The **check
hash** also reuses F029's `RuleHash` (D2), so the **same single** reference covers both reused facts.
`FreshnessKey` is itself a pure vocab core (it references only `Config`), so nothing impure (no Snapshot, no git,
no host, no filesystem) is transitively pulled in; dependency direction stays one-way (`AgentReviewKey →
FreshnessKey → Config`), and every merged core / host stays untouched. The transitive `YamlDotNet` arriving via
`Config` is unused here.

**Alternatives considered.**
- *Extend an existing core (e.g. add the agent-review key to `FreshnessKey` itself).* Rejected — it would
  entangle two distinct key surfaces (deterministic-evidence reuse vs agent-reviewed-verdict caching) under one
  baseline and blur the phase boundary; the established rhythm is one new minimal core per row.
- *Define `ArtifactHash`/`RuleHash` locally (a thin alias) so the core has zero sibling references.* Rejected —
  FR-008 says reuse the established vocabulary **verbatim**; a local alias would duplicate vocabulary the
  requirement says to reuse, and `FreshnessKey` is pure so referencing it costs nothing impure.

## D2 — Reuse F029's `RuleHash` (check hash) and `ArtifactHash` (reviewed artifacts) verbatim; five new tokens

**Decision.** The **check hash** reuses **F029's `RuleHash`** (`RuleHash of string`) and the **reviewed-artifact
hashes** reuse **F029's `ArtifactHash`** (`ArtifactHash of string`), both opened from
`FS.GG.Governance.FreshnessKey.Model` — never redefined. The **only genuinely new vocabulary** is the five
identity tokens for which no type exists yet — `ModelId`, `ModelVersion`, `ReviewerPromptHash`, `ModelConfig`,
`QuestionText` (each `of string`) — plus the vocabulary this row owns: the `AgentReviewInputs` record, the
`CacheKey of string` newtype, and the `ReviewInput` enum with its `inputToken`.

**Rationale.** FR-008 names the established **artifact-hash** vocabulary as the concrete reuse for the
reviewed-artifact hashes and offers the **rule/check hash** vocabulary for the check hash *"if it maps."* It
maps: F029's `RuleHash` is precisely *"a supplied digest of the [rule/check] that produced the verdict"* — an
opaque, comparable, edge-supplied digest of the check definition, exactly the role Phase 12's *check hash* plays.
Because the core already references `FreshnessKey` for `ArtifactHash` (which FR-008 mandates), reusing `RuleHash`
for the check hash from the **same** open is the minimal-new-vocabulary choice and adds no further reference. The
remaining five inputs (model id, model version, reviewer prompt hash, model configuration, question text) have no
existing type — F029 carries none of them, F032/F033 carry none — so each is a minimal opaque `of string` token
in the F029 opaque-token discipline (no validation, no parsing; an empty string is a literal value).

**Alternatives considered.**
- *Introduce a fresh `CheckHash of string` instead of reusing `RuleHash`.* Rejected — FR-008 says reuse existing
  typed facts verbatim *where one maps*, and `RuleHash` maps (a supplied check/rule digest); a parallel
  `CheckHash` would duplicate that vocabulary. The semantic-name concern (the field is a *check* hash, the type
  is named `RuleHash`) is handled at the **readable** layer: the `diff` category is `CheckHashInput` with token
  `"checkHash"`, decoupled from the underlying reused type exactly as F029 decouples its readable `categoryToken`
  from its terse encoding tags. The `Model.fsi` field doc states the mapping explicitly.
- *Carry the check hash and artifact hashes as bare strings.* Rejected — it would discard the typed-fact
  discipline FR-008 requires and lose cross-input injectivity guarantees the newtypes give for free.

## D3 — The computed key is `CacheKey` (not `Key`); the comparable enum is `ReviewInput` (not `InputCategory`)

**Decision.** Name the computed-fingerprint type **`CacheKey`** (`CacheKey of string`) and the comparable-input
enumeration **`ReviewInput`** (seven cases), with `inputToken : ReviewInput -> string`. Both differ from the
names `Key`, `InputCategory`, and `categoryToken` that F029's `FreshnessKey.Model` exports.

**Rationale.** This core `open`s `FS.GG.Governance.FreshnessKey.Model` to consume `RuleHash` and `ArtifactHash`
(D2). That open also brings F029's `Key`, `InputCategory`, and `categoryToken` into scope. Naming this core's
computed key `Key` and its enum `InputCategory` would shadow the opened names — a needless source of confusion
for a reader and a latent ambiguity for the compiler. `CacheKey` / `ReviewInput` are unambiguous, read naturally
in this domain (*the cache key for an agent-reviewed verdict*; *which review input changed*), and leave the F029
names untouched and unused. The `inputToken` readable vocabulary (`"modelId"` / `"checkHash"` / …) is
deliberately distinct from the terse internal encoding tags (`mid` / `chk` / …) — the same decoupling F029 keeps
between `categoryToken` and its key-encoding tags.

**Alternatives considered.**
- *Reuse the names `Key` / `InputCategory`.* Rejected — shadowing the opened F029 names invites the exact
  "which key is meant?" confusion F029's own `Model.fsi` naming note warns against.
- *Avoid the open and fully-qualify `FreshnessKey.Model.ArtifactHash` everywhere.* Rejected — verbose at every
  field and use site; a single `open` plus distinct local names is cleaner and is the established style.

## D4 — Encoding: the F029/F032/F033 tagged, length-prefixed, injective discipline; all seven required

**Decision.** `compute` renders each input as one **tagged, length-prefixed segment** `<tag>=<byteLen>:<value>`,
joined by a single `\n`, no trailing newline, UTF-8, no BOM. All seven inputs are **required** (no `option`), so
no presence digit is needed (contrast F029, whose optional command/version fields carry a `0`/`1` presence
digit). The reviewed-artifact hashes are keyed as a **set**: unwrap, **deduplicate**, **ordinal-sort**, then
render `art=<count>;<len1>:<v1>;<len2>:<v2>;…` (count-first removes the empty-vs-one-empty-element ambiguity; the
empty set renders `art=0;`). Fixed field order: model id, model version, reviewer prompt hash, model
configuration, check hash, reviewed artifacts (set), question text. Full byte spec + worked example in
[contracts/agent-review-key-format.md](./contracts/agent-review-key-format.md).

**Rationale.** The length prefix is what guarantees **injectivity** (FR-003): because the reader knows exactly
how many bytes each value occupies, no token — including one containing `:`, `=`, `\n`, `;`, or another field's
tag text, or one equal to another input's text — can masquerade as a different field or bleed across a boundary.
Distinct tags per input keep the same opaque string in two different roles producing different keys
(cross-input injectivity). This is the identical discipline F029/F032/F033 committed; reusing it keeps every
Phase-11/12 key inspectable and consistent. Dropping the presence digit is sound because every input here is
mandatory — there is no `None`/`Some ""` distinction to preserve, so an empty token is simply `tag=0:`.

**Alternatives considered.**
- *A free-form `"modelId=…;checkHash=…"` string.* Rejected — spoofable by data (a value containing the
  separator) and inconsistent with the established injective encoding.
- *Hash the concatenation into a fixed-width digest.* Rejected — this core computes **no** hash from raw bytes
  (FR-007); the key is the inspectable canonical rendering, exactly as F029's `Key` is.
- *Keep F029's presence digit on all fields for uniformity.* Rejected — no field is optional here; the digit
  would be dead weight `1` on every segment. The format contract documents the deliberate difference.

## D5 — The key models the flat seven inputs; the judge/prompt/check groupings are derivable, not modeled

**Decision.** `AgentReviewInputs` carries the **flat seven** inputs; `compute` keys over all seven and `diff`
reports **per-input** differences. The conceptual groupings the design names — *judge identity* (model id, model
version, model configuration), *prompt / question identity* (reviewer prompt hash, question text), and *check /
artifact identity* (check hash, artifact hashes) — are **derivable** from the per-input `diff`, not modeled as
explicit sub-records.

**Rationale.** The Spec Assumptions state the groupings are *"derivable"* and that whether to model them
explicitly is *"a planning detail."* Phase 12's **second** row (*"Invalidate cached verdicts when judge identity
or prompt identity changes"*) is the consumer that reads those groupings; this row's job is only to key over the
flat inputs and explain per-input drift. Keeping the record flat (mirroring F029's flat `FreshnessInputs`) is the
simpler shape (Principle III) and leaves the grouping policy to the row that owns invalidation, avoiding
premature structure this row would not exercise.

**Alternatives considered.**
- *Model three explicit sub-records (judge / prompt / check identity).* Rejected — it adds structure this row
  never reads, and the invalidation row can group the flat `ReviewInput` cases itself; premature (Principle III).

## D6 — `matches` is defined as key equality; `diff` is exhaustive and consistent with it

**Decision.** `matches a b` is **defined as** `compute a = compute b` (FR-004), and `diff a b` returns exactly
the `ReviewInput`s whose values differ, in fixed encoding order, with reviewed artifacts compared as a **set**;
`diff a b = []` **iff** `matches a b` (FR-005).

**Rationale.** Binding `matches` to key equality makes it impossible for the predicate and the key to disagree —
the cache-hit decision is, by construction, "the keys are byte-identical." Making `diff` exhaustive and
empty-iff-matches gives the no-hide guarantee (FR-005, the observable face of *"a judge or prompt change
invalidates prior cached verdicts"*): every cache miss is explainable as a named input change, and no equal input
is ever reported. Comparing reviewed artifacts as a set in `diff` (the same canonical dedup+sort `compute` uses)
ensures a reorder/duplicate is never reported as a difference — consistent with the key. This mirrors F029's
`matches`/`diff` laws exactly.

**Alternatives considered.**
- *Compute `matches` by independent field comparison.* Rejected — two code paths that could drift; defining it
  as key equality guarantees agreement (F029 precedent).

## Cross-cutting facts (no open questions)

- **Purity / totality (FR-007).** Every operation reads only its arguments: no clock, filesystem, git,
  environment, network; no model invoked, no bytes hashed, no elapsed time measured, no process spawned. Every
  `AgentReviewInputs` value (including an empty artifact set, empty-string tokens, and a token equal to another
  input's text) yields a `CacheKey` with no exception. Verified by FsCheck purity/totality properties.
- **Scope hygiene (Principle II / SC-007).** The assembly references only `FSharp.Core`,
  `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config` (transitive), and the BCL — never `Gates`,
  `Snapshot`, `Route`, any `Adapters.*`, `Host`, `Cli`, or any host/edge assembly. Guarded by the `SurfaceDrift`
  scope test (the F029–F034 precedent).
- **No new dependency (FR-011).** The keying is plain BCL `string`/`System.Text.Encoding.UTF8` building; no new
  `PackageReference`.
