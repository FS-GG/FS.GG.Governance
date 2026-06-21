# Contract: Sensed-Metadata Flagged-Rendering Encoding (F034)

The byte-level format of the `SensedRendering` strings produced by `SensedMetadata.render` (one metadatum) and
`SensedMetadata.renderSection` (a group). It is the stable, **injective**, inspectable, **unmistakably-flagged**
rendering of a sensed value, for inclusion in a deterministic report apart from its reproducible bytes. This is
the **same tagged, length-prefixed discipline** as the F029 freshness key
(`specs/029-*/contracts/freshness-key-format.md`), the F032 command identity
(`specs/032-*/contracts/command-record-identity-format.md`), and the F033 provenance identity
(`specs/033-*/contracts/provenance-identity-format.md`) — adapted here to carry an explicit **sensed marker**.

Identical sensed metadata MUST produce a byte-identical rendering; any difference in kind, label, or value MUST
produce a different rendering; and no supplied label or value may masquerade as the marker, as another field, or
as the absence of a value (FR-003/FR-004).

## The reserved sensed marker

Two reserved marker tags name the sensed partition:

| Marker | Used by | Meaning |
|---|---|---|
| `!sensed!` | `render` | this is ONE sensed / non-deterministic value |
| `!sensed-section!` | `renderSection` | this is a section of sensed / non-deterministic values |

The **`!…!` form is reserved**. Every reproducible field tag in the repo (F029/F032/F033) is a lowercase-letter
token (`src`, `base`, `head`, `rule`, `gen`, `art`, `cmds`, `env`, `bld`, `exe`, `args`, `cwd`, …) immediately
before `=`; **none begins with `!`**. Therefore a sensed rendering is **unmistakably distinguishable** from a
reproducible field by its leading bytes alone (FR-003): a reader (and any byte-stable comparison) can always tell
a sensed value from a reproducible one.

## Kind token

A fixed, total two-case map (the readable `kindToken`):

| `SensedKind` | token |
|---|---|
| `TimestampKind` | `timestamp` |
| `DurationKind` | `duration` |

## Length-prefixed scalar encoding

Each label and value is rendered as `<byteLen> ":" <bytes>`, where `<byteLen>` is the **decimal UTF-8 byte
length** of `<bytes>`. The length prefix guarantees **injectivity**: a reader consumes exactly `<byteLen>` bytes,
so no value can contain a character (`!`, `;`, `:`, `=`, `\n`) that lets it masquerade as the marker, as another
field, or bleed across a boundary (FR-004). An **empty** string renders as `0:` — a distinct, unambiguous form
that never collides with a missing value or with the marker (Edge cases).

## Single metadatum — `render`

```text
!sensed!=<kindToken>;<labelByteLen>:<label>;<valueByteLen>:<value>
```

- `<kindToken>` — `timestamp` or `duration` (table above).
- `<label>` — the `SensedLabel` string, length-prefixed.
- `<value>` — the carried value, length-prefixed:
  - `DurationValue (SensedDuration ns)` → the **decimal of the `int64` `ns`** (e.g. `0`, `1830000000`,
    `-1` if supplied negative) — verbatim, never rounded/re-scaled (D6).
  - `TimestampValue (SensedTimestamp s)` → the string `s` **verbatim** (D6).

The `;` separators are a readability convention; because every label/value is length-prefixed, the rendering is
self-delimiting regardless of `;`/`:` inside the data.

### Single-metadatum examples

A timestamp labelled `at`, value `2026-06-21T12:00:00Z` (20 UTF-8 bytes):

```text
!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z
```

A duration labelled `elapsed`, value `SensedDuration 1830000000L`:

```text
!sensed!=duration;7:elapsed;10:1830000000
```

A zero-length duration labelled with the empty label:

```text
!sensed!=duration;0:;1:0
```

A timestamp whose label text is itself `!sensed!` (4 chars after… here 8 bytes) — the length prefix neutralizes
the spoof; the inner `!sensed!` is read as 8 label bytes, not as a marker:

```text
!sensed!=timestamp;8:!sensed!;20:2026-06-21T12:00:00Z
```

## A group — `renderSection`

The metadata are **order-significant** (rendered in given order — not sorted or deduped: a report decides its own
order, and a repeated value is a real repeat). Each entry is the **full `render` string** of that metadatum,
length-prefixed so its embedded `!sensed!`/`;`/`:` are read by length:

```text
!sensed-section!=<count>;<len1>:<r1>;<len2>:<r2>;…
```

- `<count>` — the decimal element count.
- each `<rK>` — the UTF-8 byte length of `render mK`, `:`, then `render mK` itself.
- empty list ⇒ `!sensed-section!=0;` (an ordinary value, not an error — Edge cases).

### Section worked example

The two single-metadatum examples above, in order (timestamp then duration). Their `render` strings are 47 and
41 bytes respectively:

- `r1 = !sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z`  (47 bytes)
- `r2 = !sensed!=duration;7:elapsed;10:1830000000`        (41 bytes)

```text
!sensed-section!=2;47:!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z;41:!sensed!=duration;7:elapsed;10:1830000000
```

This block is the byte-exact fixture the F034 tests pin (`RenderingTests`), alongside the FsCheck
injectivity/determinism/unspoofability properties.

## Why this is identity-neutral (D5, FR-006)

The sensed rendering is a **standalone** value carrying its own reserved marker. A report places it in a sensed
section apart from its reproducible bytes; because the marker form `!…!` never appears as a reproducible field
tag, and because every reproducible identity (F029/F032/F033) is computed **only** over reproducible facts, a
sensed rendering can be added to or removed from a report's sensed section without changing any reproducible
identity. This core computes no identity itself — it only supplies the cleanly-separable sensed partition.
