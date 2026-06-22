# Phase 1 Data Model: Reviewer-Prompt Isolation — Governed-Artifact-as-Data Core

The complete typed vocabulary of `FS.GG.Governance.PromptIsolation`. Two reused types arrive verbatim
(`QuestionText` from F035, `ArtifactHash` from F029, research D2); six new types are introduced. The `.fsi` files
are the sole public-surface declaration (Principle II); the signatures + laws are in
[contracts/prompt-isolation-api.md](./contracts/prompt-isolation-api.md) and the render grammar is in
[contracts/render-format.md](./contracts/render-format.md).

## Reused types (verbatim, not redefined)

| Type | Owner | Role here |
|---|---|---|
| `QuestionText = QuestionText of string` | F035 `FS.GG.Governance.AgentReviewKey.Model` | The **reviewer-instruction channel** — the trusted authored question / rubric. Opaque, comparable, edge-supplied. Used directly (project reference). |
| `ArtifactHash = ArtifactHash of string` | F029 `FS.GG.Governance.FreshnessKey.Model` | The **digest** of a digest-only artifact — the supplied content hash, carrying no bytes. Opaque token; never parsed, validated, or computed here. Available transitively through F035. |

## New types (this feature)

### `SizeBound` — the declared excerpt bound

```fsharp
/// The declared maximum size of a bounded excerpt, in CHARACTERS (UTF-16 code units / .NET String.Length).
/// A supplied value; a negative bound is clamped to 0 by `excerpt` so capture stays total (research D4).
type SizeBound = SizeBound of int
```

- **Unit**: characters (UTF-16 code units), distinct from the render's UTF-8 byte-length prefix (D4/D5).
- **Validation**: none at construction (opaque newtype). `excerpt` clamps a negative bound to `0`.

### `Truncation` — the truncation marker

```fsharp
/// Whether a bounded excerpt was truncated to its declared bound. Never silent (Principle VI, FR-003).
type Truncation =
    | Whole        // content was at or under the bound — carried in full
    | Truncated    // content exceeded the bound — deterministically truncated to it
```

### `BoundedExcerpt` — artifact content captured within a bound (ABSTRACT)

```fsharp
/// Artifact content captured within a declared SizeBound, with its truncation status. ABSTRACT: the
/// representation is hidden, so the ONLY way to obtain one is the `excerpt` smart constructor — which is what
/// makes "no excerpt exceeds its bound" and "no form carries raw, unbounded content" hold BY CONSTRUCTION
/// (FR-002, FR-003, research D3). Read with the accessors below.
[<Sealed>] type BoundedExcerpt
```

Companion surface (in `Model`, where the representation is visible):

```fsharp
/// Capture supplied content into the declared bound. TOTAL. If content.Length <= bound, carries the content
/// whole and marks it `Whole`; otherwise carries `content.Substring(0, bound)` and marks it `Truncated`. A
/// negative bound is clamped to 0. Reads no file, computes no hash (FR-003, FR-006, research D4).
val excerpt: bound: SizeBound -> content: string -> BoundedExcerpt

/// The captured content (already within the bound). TOTAL.
val excerptContent: excerpt: BoundedExcerpt -> string
/// The declared bound the content was captured into. TOTAL.
val excerptBound: excerpt: BoundedExcerpt -> SizeBound
/// Whether the content was truncated to the bound. TOTAL.
val excerptTruncation: excerpt: BoundedExcerpt -> Truncation
```

**Invariants (enforced by abstraction).**
- `(excerptContent (excerpt b c)).Length ≤ max 0 (let (SizeBound n) = b in n)` — never over-bound.
- `excerptTruncation (excerpt b c) = Whole` ⇔ `c.Length ≤ bound` (else `Truncated`).
- `excerpt b c` truncated ⇒ `excerptContent` is exactly the `bound`-character prefix of `c`.

### `ArtifactPayload` — the closed two-form carrier (the data-channel unit)

```fsharp
/// The closed two-form carrier of ONE governed artifact in the data channel. There is no third, unbounded
/// form (FR-002, research D2).
type ArtifactPayload =
    | Excerpt of BoundedExcerpt    // content carried as data, within bound (BoundedExcerpt is abstract ⇒ always bounded)
    | DigestOnly of ArtifactHash   // the supplied digest only — no content bytes
```

### `ReviewRequest` — the assembled separation

```fsharp
/// The assembled review request: the trusted instruction channel paired with the ORDERED data channel. The two
/// channels are different SHAPES (QuestionText vs ArtifactPayload list); there is no constructor placing
/// artifact content into Instructions — separation BY CONSTRUCTION (FR-001). Artifacts preserve supplied order
/// and duplicates (research D6).
type ReviewRequest =
    { Instructions: QuestionText          // the one channel an artifact may never enter
      Artifacts: ArtifactPayload list }   // the data channel — ordered, duplicate-preserving
```

### `RenderedPrompt` — the deterministic, injective serialization

```fsharp
/// The deterministic, byte-stable, INJECTIVE serialization of a ReviewRequest, with an explicit, unspoofable
/// fence between the instruction channel and the data channel (the F029/F032/F035 tagged, length-prefixed
/// discipline — contracts/render-format.md). The auditable face of prompt isolation (FR-005). Equality is exact
/// byte equality; the value is portable across runs and machines.
type RenderedPrompt = RenderedPrompt of string
```

## Operations (module `PromptIsolation`)

```fsharp
/// Assemble a review request from trusted instructions and an ordered artifact sequence. TOTAL over all supplied
/// values, including an empty sequence and empty/boundary-length content (FR-004). Performs no reordering,
/// de-duplication, capture, hashing, or I/O — it pairs the two already-formed channels (research D6).
val assemble: instructions: QuestionText -> artifacts: ArtifactPayload list -> ReviewRequest

/// Render a review request to its canonical RenderedPrompt. PURE, TOTAL, DETERMINISTIC, INJECTIVE (FR-005,
/// FR-006): reads no clock/filesystem/git/environment/network, invokes no model, hashes no bytes; identical
/// requests render byte-identically; no artifact content can break the fence (contracts/render-format.md).
val render: request: ReviewRequest -> RenderedPrompt

/// Unwrap a RenderedPrompt to its canonical string (for handoff, messages, tests). TOTAL.
val renderedValue: prompt: RenderedPrompt -> string
```

## Entity relationships

```text
ReviewRequest
├── Instructions : QuestionText            ── reused F035 (trusted channel; artifact content can never enter)
└── Artifacts    : ArtifactPayload list    ── ordered data channel (D6)
                     │
                     ├── Excerpt    of BoundedExcerpt   ── abstract; built only by `excerpt` (D3)
                     │                    ├── content    : string      (≤ bound chars)
                     │                    ├── bound      : SizeBound    (characters, D4)
                     │                    └── truncation : Truncation   (Whole | Truncated)
                     └── DigestOnly of ArtifactHash     ── reused F029 (supplied token; no bytes)

render : ReviewRequest -> RenderedPrompt   ── injective, length-prefixed (D5)
```

## Mapping to functional requirements

| Requirement | Carried by |
|---|---|
| FR-001 two structurally distinct channels, separation by construction | `ReviewRequest` (`QuestionText` vs `ArtifactPayload list`); no cross constructor |
| FR-002 exactly two closed forms, no unbounded form | `ArtifactPayload` (`Excerpt` / `DigestOnly`); abstract `BoundedExcerpt` |
| FR-003 deterministic truncation, explicit marker, no over-bound | `excerpt` + `Truncation` + `SizeBound` (D3/D4) |
| FR-004 total assembly | `assemble` |
| FR-005 total, deterministic, injective render | `render` + `RenderedPrompt` (D5) |
| FR-006 pure / total over supplied data | all operations (no clock/fs/git/env/net; no model; no hash) |
| FR-007 reuse `QuestionText` / `ArtifactHash`; minimal new vocabulary | reused types + six new types (D2) |
| FR-008 no verdict / cache key / store / record / promotion / calibration / CLI | absent by construction — surface is only the types above + 3 functions |
| FR-009 Tier-1 surface governed by `.fsi` + baseline | two `.fsi` + `surface/FS.GG.Governance.PromptIsolation.surface.txt` |
| FR-010 no new third-party dependency | BCL `System.Text` + `FSharp.Core` only |
