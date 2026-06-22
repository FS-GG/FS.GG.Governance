# Contract: Canonical Record-Identity Encoding (F038)

The byte-level format of the `RecordIdentity` string produced by `ReviewRecord.canonicalId` (research D5). It is
the stable, comparable, **injective**, inspectable rendering of a record's `ReproducibleFacts`. It is the **same
tagged, length-prefixed discipline** as the F029 freshness key, the F032 command identity, and the F035 cache key
(`AgentReviewKey.fs` `seg`/`artSegment`, reused verbatim in idiom); the laws below restate it for the review-record
fields.

Identical reproducible facts (artifact digests compared as a set) MUST produce a byte-identical identity; any
single differing reproducible fact MUST produce a different identity; the same opaque string placed in two
different fields MUST produce different identities. The **sensed metadata is never rendered** — it is held
structurally apart (`record.Sensed`) and excluded from identity (FR-004, D6).

## Length measure

Every `<utf8ByteLen>` below is the **UTF-8 byte count** of the value that follows the `:` —
`System.Text.Encoding.UTF8.GetByteCount value` (the F035 `byteLen` helper, verbatim). A reader consumes exactly
that many bytes for the value, so no value can masquerade as another field or as a structural marker — this is what
makes the encoding injective.

## Segment encoding

Every scalar field is rendered as one **tagged, length-prefixed segment** (the F035/F037 plain form — every
reproducible field is required, so there is **no** presence digit, unlike F032's optional `CapturedOutput`):

```text
<tag> "=" <utf8ByteLen> ":" <value>
```

`<tag>` is a fixed lowercase ASCII tag, unique per field. Example: model id `"gpt"` → `mid=3:gpt`.

## Field order and tags

The reproducible facts are joined by `'\n'` (LF, no trailing newline) in this fixed order:

| # | Field | Tag | Encoding |
|---|---|---|---|
| 1 | `Request` | `req` | required string — see below |
| 2 | `Model` | `mid` | required string |
| 3 | `ModelVersion` | `mver` | required string |
| 4 | `PromptHash` | `pph` | required string |
| 5 | `ReviewedArtifacts` | `art` | **set segment** (below) |
| 6 | `ResponseDigest` | `resp` | required string |
| 7 | `Verdict` | `vdt` | required string |

`Model`, `ModelVersion`, `PromptHash`, `ResponseDigest`, and `Verdict` are each the unwrapped string of their
newtype, encoded as a required string.

## `req` — the embedded request as its F037 rendering

The request segment carries the **F037 canonical rendering** of the embedded `ReviewRequest`:

```text
req=<utf8ByteLen>:<renderedPrompt>
```

where `<renderedPrompt>` is `PromptIsolation.renderedValue (PromptIsolation.render record.Reproducible.Request)` —
the deterministic, byte-stable, injective serialization F037 already guarantees (the instruction channel + ordered
data channel with its unspoofable length-prefixed fence). Because the whole rendering is itself length-prefixed by
its UTF-8 byte count, the request contributes injectively: any difference in the request that changes its F037
rendering (instructions, an artifact payload, an excerpt's content/bound/truncation, a digest, payload
order/count) changes the `req` segment and thus the identity; and the rendering's own `\n` (between its two
channels) is consumed *inside* the `req` payload by length, so it cannot break the record-identity line structure.
This is the direct F033 analogue (each embedded `CommandRecord` contributes `CommandRecord.canonicalId record`).

## `art` — reviewed-artifact set segment

The reviewed-artifact digests are **order-independent and duplicate-collapsing** (D4; the F035 set discipline).
Each `ArtifactHash` is unwrapped, the list is **deduplicated and ordinal-sorted**, then encoded as a counted list —
the F035 `artSegment`, reused verbatim:

```text
art=<count>;<len1>:<h1>;<len2>:<h2>;…
```

`<count>` is the decimal element count of the **deduped** set; each element is its UTF-8 byte length, `:`, and the
hash value; elements are joined by `;` with no trailing `;`. Empty set ⇒ `art=0;`. Supplying the same digests in a
different order, or with duplicates, yields the same sorted/deduped segment — hence the same identity (D4).

## Worked example

A record whose reproducible facts are: request = `assemble (QuestionText "Explain the API?") [ DigestOnly
(ArtifactHash "sha:a") ]`; model `gpt`; version `2026-06`; prompt hash `ph1`; reviewed artifacts `[ ArtifactHash
"sha:a" ]`; response digest `sha:resp`; verdict `pass`; sensed `[ markTimestamp (SensedLabel "at") (SensedTimestamp
"2026-06-22T10:00:00Z") ]`.

The F037 rendering of the request is (its own two segments, joined by `\n`):

```text
instr=16:Explain the API?
art=1;dig=5:sha:a
```

— call this string `R` (its UTF-8 byte length is `byteLen R`). The record identity is then (joined by `'\n'`, no
trailing newline):

```text
req=<byteLen R>:instr=16:Explain the API?
art=1;dig=5:sha:a
mid=3:gpt
mver=7:2026-06
pph=3:ph1
art=1;5:sha:a
resp=8:sha:resp
vdt=4:pass
```

(The `req` value spans the two physical lines of `R`, consumed by its declared byte length — the embedded `\n` is
data, not a record-identity line break.) The sensed timestamp does **not** appear anywhere in the block: changing
or removing it leaves the identity byte-identical (FR-004, D6). Changing any reproducible field above — including
any change that alters the request's F037 rendering — changes the block, hence the identity (FR-003). Reordering or
duplicating the `art` digests leaves the `art` segment (and the identity) unchanged (D4).

## Properties (restated)

- **Deterministic & byte-stable** — identical reproducible facts (artifacts as a set) ⇒ identical bytes
  (SC-002/SC-005).
- **Injective across fields** — length prefixes + unique tags ⇒ no field can masquerade as another; the same
  string in two fields yields different identities (FR-003).
- **Request-faithful** — the `req` segment is F037's own injective rendering, so request differences propagate to
  identity, while two identically-rendering requests differing in model/prompt identity still differ via the
  separate `mid`/`mver`/`pph` segments (Edge Cases, L-I7).
- **Order-independent over the artifact set, order-significant inside the request** — `art` is set-compared (D4);
  the request's data channel keeps order inside the `req` rendering (F037 D6).
- **Sensed-free** — `record.Sensed` is never encoded (FR-004, D6).
- **No hashing** — the canonical string *is* the identity; no digest is computed from bytes (FR-009).
