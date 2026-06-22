# Phase 0 Research: Reviewer-Prompt Isolation — Governed-Artifact-as-Data Core

All Technical Context unknowns are resolved below. There were **no** `NEEDS CLARIFICATION` markers: the spec
fixes the observable contract (structural channel separation, bounded-or-digest capture, injective deterministic
render, purity over supplied values) and explicitly defers a closed set of *shaping* decisions to planning. Each
decision is recorded as **Decision / Rationale / Alternatives considered**, anchored to the established
F029 / F032 / F035 / F036 precedent.

---

## D1 — A new pure-core library with a single sibling reference

**Decision.** Add one new packable pure-core library, **`FS.GG.Governance.PromptIsolation`**, compiled
`Model → PromptIsolation`, with a **single** `ProjectReference` to `FS.GG.Governance.AgentReviewKey` (F035). No
other project reference; no new third-party `PackageReference`.

**Rationale.** Every Phase-12 row so far landed exactly one new minimal pure core before any edge consumed it
(F035, F036). A single sibling reference is the F036 shape: F036 referenced only `AgentReviewKey` and obtained
F029's `RuleHash`/`ArtifactHash` transitively through it. Here the same single reference yields both reused
vocabularies — `QuestionText` (defined in `AgentReviewKey.Model`, used directly) and `ArtifactHash` (defined in
`FreshnessKey.Model`, available transitively because F035 references F029). The dependency direction stays
one-way and acyclic: `PromptIsolation → AgentReviewKey → FreshnessKey → Config`. Every merged core/host stays
untouched.

**Alternatives considered.**
- *Reference `FreshnessKey` directly as well (two references).* Honest about the direct use of `ArtifactHash`,
  but heavier than the F036 precedent and unnecessary — transitive project references flow as compile references
  by default, so `open FS.GG.Governance.FreshnessKey.Model` resolves through the F035 reference. Rejected for the
  minimal single-reference rhythm; the scope-guard test still names `FreshnessKey` as an allowed (transitive)
  reference.
- *Extend an existing core (e.g. `AgentReviewKey`).* Rejected — it would modify a merged surface/baseline
  (violating SC-006 and the additive-only constraint) and mix *cache identity* with *prompt shape*, two distinct
  concerns. The spec Assumptions favour a new minimal core.

---

## D2 — Reuse `QuestionText` (instructions) and `ArtifactHash` (digest) verbatim; introduce four new types

**Decision.** Reuse **F035 `QuestionText`** verbatim for the **reviewer-instruction channel**, and **F029
`ArtifactHash`** verbatim for the **digest-only** artifact form. Introduce only the minimal new vocabulary the
row needs: `SizeBound`, `Truncation`, the abstract `BoundedExcerpt` (with `excerpt` + accessors),
`ArtifactPayload`, `ReviewRequest`, and `RenderedPrompt`.

**Rationale.** FR-007 names both reuses concretely — "the established artifact-hash vocabulary for a digest-only
artifact's hash and … the established question-text vocabulary for the reviewer instructions." `QuestionText`
("the question text the reviewer was asked") maps cleanly to the trusted authored question / rubric, and
`ArtifactHash` is the exact digest-as-supplied-token already used by F029/F035. Reusing them keeps merged
vocabulary single-sourced and avoids a near-duplicate newtype. The four genuinely new types have no existing
home: there is no bounded-excerpt value, no truncation marker / size bound, no artifact-payload two-form, and no
review-request value anywhere in the merged cores.

**Alternatives considered.**
- *Introduce a dedicated `ReviewerInstructions` newtype instead of reusing `QuestionText`.* Decouples this core
  from F035 and would let it reference only F029 (mirroring F035's own shape). Rejected because FR-007 names the
  question-text vocabulary **concretely** and the reuse reduces vocabulary; the conceptual map (authored question
  / rubric ↔ question text) is faithful. Recorded here as the considered alternative the spec's "where it maps"
  qualifier permits.
- *Model the digest as a fresh string newtype.* Rejected — FR-007 mandates the established `ArtifactHash`;
  re-minting it would fork the artifact-hash vocabulary.

---

## D3 — `BoundedExcerpt` is an abstract type with a smart constructor (bounding by construction)

**Decision.** Declare `BoundedExcerpt` as an **abstract type** in `Model.fsi` (no public representation). Its
*only* constructor is `Model.excerpt : SizeBound -> string -> BoundedExcerpt`, which captures supplied content
into the bound and records its truncation status. Expose read-only accessors `excerptContent`, `excerptBound`,
and `excerptTruncation`. The `ArtifactPayload.Excerpt` case wraps this abstract value.

**Rationale.** FR-002/FR-003 require that *no excerpt exceeds its declared bound* and that *no form carries raw,
unbounded content* — and the spec frames separation/bounding as holding **by construction**, not by convention.
A public record (`{ Content; Bound; Truncation }`) would let a caller construct
`{ Content = hugeString; Bound = SizeBound 5; Truncation = Whole }`, breaking the invariant. Hiding the
representation behind `excerpt` makes the invariant unforgeable: the only way to obtain a `BoundedExcerpt` is to
run the bounded capture, so every excerpt is within its bound and its truncation status is honest. This is the
minimal idiomatic F# mechanism for an enforced-invariant value (Principle II: visibility lives in the `.fsi`).

**Alternatives considered.**
- *Public record with a documented invariant.* Rejected — convention, not construction; fails the FR-002/FR-003
  "by construction" requirement and SC-002's 100%-of-cases guarantee.
- *A single-case DU `BoundedExcerpt of string * SizeBound * Truncation`.* A DU still exposes its constructor, so
  it has the same forgeability problem as a record. Rejected for the same reason.

---

## D4 — The size bound is measured in characters (UTF-16 code units); truncation is a prefix; negative clamps to zero

**Decision.** `SizeBound` wraps an `int` measured in **characters** = .NET `String.Length` (UTF-16 code units).
`excerpt bound content` returns the content whole + `Whole` when `content.Length ≤ bound`; otherwise it returns
`content.Substring(0, bound)` + `Truncated`. A negative bound is clamped to `0` so capture is total. (The render's
length **prefix** is a separate measure — UTF-8 bytes — see D5.)

**Rationale.** The spec leaves the unit to planning ("Whether the size bound is expressed in characters or
bytes … are planning details; the fixed contract is that every excerpt is within its bound and its truncation
status is explicit"). Characters via `String.Length` + `Substring` is the simplest **total, deterministic**
operation: at/under is carried whole, over is a deterministic prefix, exactly-at-bound is whole (boundary cases in
the spec are exact). It reads nothing and computes no hash. Clamping a negative bound keeps `excerpt` total over
all `int` values (Principle VI — no exception). The character measure is independent of the UTF-8 byte length the
render uses for injectivity, and both are documented so the two are never conflated.

**Alternatives considered.**
- *Bound in UTF-8 bytes with code-point-safe truncation.* Consistent with the render's byte-length prefix, but
  truncating to the largest valid UTF-8 prefix not exceeding the bound is more machinery for no contract gain;
  the spec's "length" language and boundary cases read most naturally as a character count. Rejected for
  simplicity (Principle III).
- *Reject (throw on) a negative bound.* Rejected — it would make `excerpt` partial, violating totality (FR-004,
  Principle VI). Clamping to zero is total and the zero-bound behaviour (any non-empty content ⇒ empty excerpt
  marked truncated) is already a fixed edge case.

---

## D5 — The rendered fence: tagged, length-prefixed, injective — the F035 discipline applied per channel and payload

**Decision.** `render` serializes a `ReviewRequest` to a `RenderedPrompt` string as two segments joined by `\n`
(no trailing newline), in the F029/F032/F035 tagged, length-prefixed discipline (full grammar in
[contracts/render-format.md](./contracts/render-format.md)):

```text
instr=<utf8ByteLen>:<instructions>
art=<count>;<payload-1>;<payload-2>;…
```

where each payload is length-prefixed by its UTF-8 byte count:
- bounded excerpt: `exc=<w|t>,<utf8ByteLen>:<content>` (`w` = whole, `t` = truncated)
- digest only: `dig=<utf8ByteLen>:<hash>`

The artifact count precedes the payloads (removing the empty-vs-one-empty-element ambiguity, exactly as F035's
`art=<count>;…`). The empty data channel renders `art=0;`; an empty excerpt renders `exc=w,0:`; a digest with an
empty supplied hash renders `dig=0:` — three distinct, unambiguous forms.

**Rationale.** Injectivity is what makes the isolation trustworthy (FR-005, SC-003): because every value
(instructions, each excerpt's content, each digest) is preceded by its exact UTF-8 byte length, a reader consumes
each value *by length*, so artifact content containing the fence markers (`instr=`, `art=`, `exc=`, `dig=`), the
channel separator (`\n`), the payload separator (`;`), the tag characters (`=`, `:`, `,`), or a line imitating an
instruction is read **as data** and cannot terminate the data channel, forge a field boundary, open or alter the
instruction channel, or bleed across a boundary. This is precisely the F035 `seg`/`artSegment` encoding, re-used
verbatim in idiom, applied to the *prompt's* two channels and its payload forms rather than to a cache key's seven
fields. Joining by `\n` and prefixing by UTF-8 byte length matches F035 byte-for-byte in technique, so the
determinism and injectivity arguments transfer directly.

**Alternatives considered.**
- *A delimiter-only fence (e.g. a sentinel line `---ARTIFACT---`).* Rejected — any sentinel can be emitted by
  artifact content, so the fence would be spoofable; the spec demands an **injective** fence read by length, not
  by delimiter.
- *Escaping artifact content (quoting the markers).* Rejected — escaping is error-prone and non-injective in
  general; length-prefixing is the established, provably-injective discipline already used across F029–F035.
- *Rendering artifacts as an order-insensitive set (the F035 `art` set).* Rejected — see D6; presentation order
  is significant for a reviewer, so the data channel preserves order and duplicates.

---

## D6 — The data channel is an ordered sequence (order- and duplicate-preserving), not a set

**Decision.** `ReviewRequest.Artifacts` is an `ArtifactPayload list` rendered **in the supplied order with
duplicates preserved**. `render` performs no sort and no de-duplication of payloads.

**Rationale.** The spec Assumptions draw the explicit contrast with F035: F035 keys the same review *identically*
regardless of artifact order (its `ReviewedArtifacts` is compared as a set, because identity is what is keyed),
but the artifacts *presented to a reviewer* are an ordered sequence the reviewer reads in order. Two artifacts
with identical content, or the same reference appearing twice, are each carried as their own payload (Edge Cases).
Order- and duplicate-preserving rendering is therefore the fixed contract here, and it is also the simplest
(no canonicalisation step), keeping the render deterministic and total.

**Alternatives considered.**
- *Canonicalise the data channel as a set (mirror F035).* Rejected — it would drop or reorder payloads a reviewer
  is meant to see in sequence, contradicting the spec's explicit order-significance assumption.

---

## Resolved Technical Context summary

| Unknown | Resolution |
|---|---|
| Module/assembly home and name | New core `FS.GG.Governance.PromptIsolation`, `Model → PromptIsolation` (D1) |
| Which existing types are reused | `QuestionText` (F035) for instructions; `ArtifactHash` (F029) for digests (D2) |
| Which types are new | `SizeBound`, `Truncation`, abstract `BoundedExcerpt`, `ArtifactPayload`, `ReviewRequest`, `RenderedPrompt` (D2) |
| How bounding is enforced | Abstract `BoundedExcerpt` + smart constructor `excerpt` — by construction (D3) |
| Size-bound unit + truncation | Characters (`String.Length`); prefix truncation; negative clamps to zero (D4) |
| Rendered-fence format | Tagged, UTF-8-length-prefixed, injective, `\n`-joined two-segment render (D5) |
| Data-channel ordering | Ordered sequence, order- and duplicate-preserving (D6) |
| New third-party dependency | None — BCL `System.Text` + `FSharp.Core` only (FR-010) |
| MVU boundary | N/A — pure total functions over supplied values (Principle IV) |
