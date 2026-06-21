# Contract: Canonical Freshness Key Encoding

The byte-level format of the `Key` string produced by `FreshnessKey.compute` (research D2). This is the
stable, comparable, **injective**, inspectable rendering of `FreshnessInputs`. Identical inputs (covered
artifacts compared as a set) MUST produce a byte-identical key; any single differing input category MUST
produce a different key; the same opaque string placed in two different categories MUST produce different
keys.

## Segment encoding

Every field is rendered as one **tagged, length-prefixed segment**:

```text
<tag> "=" <presence> <payload>
```

- `<tag>` — a fixed lowercase ASCII tag, unique per category (table below).
- For a **required string value** `s` (UTF-8): `<presence>` is `1` and `<payload>` is `<byteLen> ":" s`,
  where `<byteLen>` is the decimal UTF-8 byte length of `s`. Example: rule hash `"abc"` →
  `rule=13:abc`  (presence `1`, length `3`).  *(Read as `rule` `=` `1` `3:abc`.)*
- For an **optional value** that is `None`: `<presence>` is `0` and there is no payload. Example: absent
  command → `cmd=0`.
- For an **optional value** that is `Some s`: identical to a required value (`<presence>` `1`, then
  `<byteLen> ":" s`).

The length prefix is what guarantees **injectivity**: because the reader knows exactly how many bytes the
value occupies, no value can contain a character (including the `:` , `=`, `\n`, or the tag text of another
field) that lets it masquerade as a different field or bleed across the segment boundary. The leading
presence digit (`0`/`1`) keeps `None` distinct from `Some ""` (`cmd=0` vs `cmd=10:`).

## Environment class token

`EnvironmentClass` is rendered via a fixed total token, then encoded as a required string value:

| Case | Token |
|---|---|
| `Local` | `local` |
| `Ci` | `ci` |
| `LocalOrCi` | `localOrCi` |
| `Release` | `release` |

## Covered-artifact set encoding

`CoveredArtifacts` is rendered as a **set** (FR-004):

1. Unwrap each `ArtifactHash` to its string.
2. **Deduplicate** (distinct values).
3. **Sort** by ordinal (culture-invariant) byte order.
4. Render the count, then each element as a length-prefixed string value, all under the `art` tag:
   `art=<count>;<len1>:<v1>;<len2>:<v2>;…` (count first removes the empty-vs-one-empty-element ambiguity;
   each element is still length-prefixed for injectivity). The empty set renders `art=0;`.

Order and duplication of the input list therefore never change the output (Edge cases; SC-002).

## Field order and joining

Segments are emitted in this **fixed order**, joined by a single newline `\n`, with **no trailing
newline**, UTF-8, no BOM:

| # | Field | Tag |
|---|---|---|
| 1 | `Check` | `check` |
| 2 | `Domain` | `domain` |
| 3 | `Command` (option) | `cmd` |
| 4 | `Environment` (token) | `env` |
| 5 | `RuleHash` | `rule` |
| 6 | `CoveredArtifacts` (set) | `art` |
| 7 | `CommandVersion` (option) | `cmdv` |
| 8 | `GeneratorVersion` | `genv` |
| 9 | `Base` | `base` |
| 10 | `Head` | `head` |

These terse tags are **internal to the key encoding**. They are deliberately **distinct** from the
human-readable `categoryToken` vocabulary used by `diff` output and messages (e.g. the encoding tag is
`rule` / `art` / `cmdv`, while `categoryToken` returns `ruleHash` / `coveredArtifacts` / `commandVersion`).
The two vocabularies are decoupled on purpose: the key optimizes for compact injective bytes, `diff` for
readability. See the `categoryToken` table in [freshness-key-api.md](./freshness-key-api.md).

## Worked example

For inputs `Check="build:tests"`, `Domain="build"`, `Command=Some "dotnet"`, `Environment=Local`,
`RuleHash="r1"`, `CoveredArtifacts=[ "h2"; "h1"; "h1" ]`, `CommandVersion=Some "8.0"`,
`GeneratorVersion="g1"`, `Base="aaa"`, `Head="bbb"` the key is:

```text
check=111:build:tests
domain=15:build
cmd=16:dotnet
env=15:local
rule=12:r1
art=2;2:h1;2:h2
cmdv=13:8.0
genv=12:g1
base=13:aaa
head=13:bbb
```

*(Each `=` is followed by the presence digit `1`, then `<byteLen>:<value>`; e.g. `check=1` then `11:build:tests`
— the value `build:tests` is 11 bytes. The artifact set is deduped to `{h1,h2}` and ordinally sorted.)*

## Stability rules (the drift contract)

- The format is **append-only by extension**: adding a future input category appends a new segment, which
  necessarily changes every key — correctly forbidding reuse of evidence recorded before the category
  existed (Edge case "future input growth"). Such a change is Tier 1 (new surface + re-blessed baseline).
- Tags, the token table, the field order, the length-prefix scheme, and `\n` joining are the committed
  contract; changing any of them is a breaking change requiring a new spec.
- No clock, host path, environment read, or enumeration-order influence enters the key (FR-008).
