# Contract: Canonical Agent-Review Cache-Key Encoding

The byte-level format of the `CacheKey` string produced by `AgentReviewKey.compute` (research D4). This is the
stable, comparable, **injective**, inspectable rendering of `AgentReviewInputs`. Identical inputs (reviewed
artifacts compared as a set) MUST produce a byte-identical key; any single differing input MUST produce a
different key; the same opaque string placed in two different inputs MUST produce different keys.

## Segment encoding

Every input is rendered as one **tagged, length-prefixed segment**:

```text
<tag> "=" <byteLen> ":" <value>
```

- `<tag>` — a fixed lowercase ASCII tag, unique per input (table below).
- `<value>` — the unwrapped opaque string, verbatim, UTF-8.
- `<byteLen>` — the decimal UTF-8 **byte** length of `<value>`.

Example: model id `"claude-opus-4"` → `mid=13:claude-opus-4`. An empty token (e.g. empty question text) →
`q=0:`.

All seven inputs are **required**, so — unlike F029, whose optional fields carry a `0`/`1` presence digit —
there is **no presence digit** here; every segment is `<tag>=<byteLen>:<value>` (research D4). The length prefix
is what guarantees **injectivity**: because the reader knows exactly how many bytes the value occupies, no value
can contain a character (including `:`, `=`, `\n`, `;`, or the tag text of another field) that lets it masquerade
as a different field or bleed across the segment boundary.

## Reviewed-artifact set encoding

`ReviewedArtifacts` is rendered as a **set** (FR-006):

1. Unwrap each `ArtifactHash` to its string.
2. **Deduplicate** (distinct values).
3. **Sort** by ordinal (culture-invariant) byte order.
4. Render the count, then each element as a length-prefixed string value, all under the `art` tag:
   `art=<count>;<len1>:<v1>;<len2>:<v2>;…` (count first removes the empty-vs-one-empty-element ambiguity; each
   element is still length-prefixed for injectivity). The empty set renders `art=0;`.

Order and duplication of the input list therefore never change the output (Edge cases; SC-002).

## Field order and joining

Segments are emitted in this **fixed order**, joined by a single newline `\n`, with **no trailing newline**,
UTF-8, no BOM:

| # | Input | Tag |
|---|---|---|
| 1 | `Model` (model id) | `mid` |
| 2 | `ModelVersion` | `mver` |
| 3 | `PromptHash` (reviewer prompt hash) | `prompt` |
| 4 | `Config` (model configuration) | `cfg` |
| 5 | `Check` (check hash) | `chk` |
| 6 | `ReviewedArtifacts` (set) | `art` |
| 7 | `Question` (question text) | `q` |

These terse tags are **internal to the key encoding**. They are deliberately **distinct** from the
human-readable `inputToken` vocabulary used by `diff` output and messages (e.g. the encoding tag is `mid` / `chk`
/ `art`, while `inputToken` returns `modelId` / `checkHash` / `reviewedArtifacts`). The two vocabularies are
decoupled on purpose: the key optimizes for compact injective bytes, `diff` for readability. See the `inputToken`
table in [agent-review-key-api.md](./agent-review-key-api.md).

## Worked example

For inputs `Model="claude-opus-4"`, `ModelVersion="20260101"`, `PromptHash="p1"`, `Config="temp=0"`,
`Check="c1"`, `ReviewedArtifacts=[ "h2"; "h1"; "h1" ]`, `Question="explains API?"` the key is:

```text
mid=13:claude-opus-4
mver=8:20260101
prompt=2:p1
cfg=6:temp=0
chk=2:c1
art=2;2:h1;2:h2
q=13:explains API?
```

*(Each `=` is followed by `<byteLen>:<value>`; e.g. `cfg=6:temp=0` — the value `temp=0` is 6 bytes, and the `=`
inside it is read by length, never as a field boundary. The artifact set is deduped to `{h1,h2}` and ordinally
sorted; the empty set would render `art=0;`.)*

## Stability rules (the drift contract)

- The format is **append-only by extension**: adding a future input appends a new segment, which necessarily
  changes every key — correctly forbidding reuse of a verdict cached before the input existed. Such a change is
  Tier 1 (new surface + re-blessed baseline).
- Tags, the field order, the length-prefix scheme, the set encoding, and `\n` joining are the committed
  contract; changing any of them is a breaking change requiring a new spec.
- No clock, host path, environment read, model invocation, or enumeration-order influence enters the key
  (FR-007).
