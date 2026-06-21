# Phase 1 Data Model: Agent-Review Verdict Cache-Key Core (F035)

All types live in `FS.GG.Governance.AgentReviewKey.Model` (sole public declaration: `Model.fsi`). They are
product-neutral, comparable values carrying no raw bytes, host paths, clock readings, or product vocabulary.
Reused F029 newtypes are `open`ed from `FS.GG.Governance.FreshnessKey.Model`; nothing in `FreshnessKey`/`Config`
is modified (FR-008). Names are the recommended spelling; minor identifier adjustments at implementation are
allowed as long as the contracts in `contracts/` hold.

## Reused vocabulary (from `FreshnessKey.Model`, F029 — verbatim)

| Type | Form | Role in this feature |
|---|---|---|
| `RuleHash` | `RuleHash of string` | The supplied **check hash** — a digest of the agent-reviewed check definition (research D2). |
| `ArtifactHash` | `ArtifactHash of string` | The supplied digest of **one reviewed artifact**; the key fingerprints the **set** of these (research D2). |

These are exactly the opaque, comparable, edge-supplied digest newtypes F029 introduced. This core consumes them
as data; it computes neither digest. (The `open` also brings F029's `Key`/`InputCategory`/`categoryToken` into
scope; this core deliberately does **not** reuse those names — research D3.)

## New opaque newtypes (this feature)

Each is single-case `of string`, opaque and comparable; the actual id/version/hash/config/text is formed at the
edge and supplied as data (FR-001, the F029 opaque-token discipline). No validation, no parsing — an empty string
is a literal value (FR-003 / Edge cases).

| Type | Form | Represents |
|---|---|---|
| `ModelId` | `ModelId of string` | Which model answered (its id). |
| `ModelVersion` | `ModelVersion of string` | The model's version. |
| `ReviewerPromptHash` | `ReviewerPromptHash of string` | A supplied digest of the reviewer prompt. |
| `ModelConfig` | `ModelConfig of string` | The relevant model configuration (e.g. a canonical settings token). |
| `QuestionText` | `QuestionText of string` | The question text the reviewer was asked. |

## Key entity — `AgentReviewInputs`

The closed set of the **seven** inputs an agent-reviewed verdict depends on (FR-001). A record so every input is
named and type-checked; reviewed artifacts are a list compared as a **set** (FR-006).

```text
type AgentReviewInputs =
    { // ── judge identity ──
      Model: ModelId
      ModelVersion: ModelVersion
      Config: ModelConfig
      // ── prompt / question identity ──
      PromptHash: ReviewerPromptHash
      Question: QuestionText
      // ── check / reviewed-artifact identity (reused F029 vocabulary) ──
      Check: RuleHash                       // the CHECK hash; type reused verbatim from F029 (research D2)
      ReviewedArtifacts: ArtifactHash list } // compared as a SET: order + duplication ignored (FR-006)
```

Notes:
- All seven inputs are **required** — there is no `option` field (research D4). An empty token (e.g.
  `QuestionText ""`) is a valid literal value, not an absence.
- A `ReviewedArtifacts = []` value is valid (a review over zero artifacts; Edge case) and keys to a distinct,
  unambiguous form — never treated as "absent" and never colliding with a one-artifact set.
- The record stores the **flat seven** inputs; the *judge / prompt / check* groupings the design names are
  derivable from `diff` and are **not** modeled as sub-records (research D5). The field grouping comments above
  are documentation only.
- **Record field order is grouping-for-readability only — it is NOT the encoding/`diff` order.** The declaration
  above groups by judge / prompt / check identity, but the canonical key encoding and the `diff` result use the
  **fixed encoding order** (model id, model version, prompt hash, model config, check hash, reviewed artifacts,
  question text) defined in [contracts/agent-review-key-format.md](./contracts/agent-review-key-format.md) and the
  `ReviewInput` DU above. `compute` and `diff` MUST emit in that encoding order, never in record-declaration order
  — do not derive `diff` from naive record-field iteration. (Pinned by the fixed-order assertion in DiffTests.)

## Key entity — `CacheKey`

The deterministic, byte-stable, comparable cache key produced from `AgentReviewInputs`.

```text
type CacheKey = CacheKey of string   // the canonical encoding (contracts/agent-review-key-format.md)
```

> **Naming note (avoid confusion).** This computed-key type is `CacheKey` — **not** `Key`. F029's
> `FreshnessKey.Model` already exports a `Key` type, and this core `open`s that module (for
> `RuleHash`/`ArtifactHash`). Naming this core's key `CacheKey` keeps the two unambiguous and leaves F029's `Key`
> untouched (research D3).

- Equal `CacheKey`s ⇒ "same judge / prompt / check / artifact identity, the cached verdict is reusable";
  different ⇒ "some identity input changed, the verdict is not reusable."
- The wrapped string is the canonical tagged, length-prefixed rendering (research D4), so equality is exact byte
  equality and the value is portable across runs/machines (what the later verdict store keys on).
- Inspectable: the structure is parseable, and `diff` (below) explains a non-match over the seven inputs.

## Key entity — `ReviewInput`

The closed enumeration of comparable inputs, returned by `diff` to name what changed (FR-005, the no-hide
requirement). One case per input, in the fixed key-encoding order.

```text
type ReviewInput =
    | ModelIdInput
    | ModelVersionInput
    | PromptHashInput
    | ModelConfigInput
    | CheckHashInput
    | ReviewedArtifactsInput
    | QuestionTextInput
```

A stable token function (`inputToken : ReviewInput -> string`) renders each for `diff` output and messages. It
returns the **human-readable** vocabulary (`ModelIdInput → "modelId"`, `CheckHashInput → "checkHash"`,
`ReviewedArtifactsInput → "reviewedArtifacts"`, …), which is deliberately **distinct** from the terse internal
key-encoding tags (`mid`, `chk`, `art`, …) in
[contracts/agent-review-key-format.md](./contracts/agent-review-key-format.md). The committed token table is in
[contracts/agent-review-key-api.md](./contracts/agent-review-key-api.md).

> **Note on `CheckHashInput`.** The readable token is `"checkHash"` even though the underlying field type is the
> reused `RuleHash` (research D2): the readable vocabulary names the *role* (a check hash), decoupled from the
> reused type, exactly as F029 decouples `categoryToken` from its encoding tags.

## Relationships & invariants

- `compute : AgentReviewInputs -> CacheKey` is **total** and **deterministic**: defined for every value;
  identical inputs (reviewed artifacts compared as a set) always yield byte-identical keys (FR-002/FR-006/FR-007).
- `matches a b  ⇔  compute a = compute b` — bound by definition so the predicate and the key never disagree
  (FR-004).
- `diff a b = []  ⇔  matches a b` — the explainer is exhaustive and consistent with the predicate (FR-005,
  SC-003). When non-empty, `diff` lists exactly the inputs whose values differ, in a fixed order.
- **Injective across inputs** (FR-003): for any two input sets that place the same opaque string in different
  inputs, `compute` yields different keys (guaranteed by the length-prefixed tagged encoding).
- **Set semantics** (FR-006): reordering or duplicating `ReviewedArtifacts` leaves `compute`, `matches`, and
  `diff` unchanged.
- **No I/O / no clock / no model** (FR-007): the only data read is the argument; the result is independent of
  time, cwd, environment, filesystem, git state, and any model invocation (SC-006).

## State transitions

None. All four operations are pure value transforms; there is no mutable state, lifecycle, or workflow (Principle
IV N/A).
