# Contract: Canonical Provenance-Identity Encoding (F033)

The byte-level format of the `ProvenanceIdentity` string produced by `Provenance.canonicalId` (research D5). It
is the stable, comparable, **injective**, inspectable rendering of a provenance's **reproducible** facts. This
is the **same tagged, length-prefixed discipline** as the F029 freshness key
(`specs/029-*/contracts/freshness-key-format.md`) and the F032 command identity
(`specs/032-*/contracts/command-record-identity-format.md`); the laws below restate it for the provenance
fields.

Identical reproducible facts (artifact digests compared as a set) MUST produce a byte-identical identity; any
single differing reproducible fact MUST produce a different identity; the same opaque string placed in two
different fields MUST produce different identities. The embedded command records' **durations are never
rendered** (they are sensed — FR-005): each record contributes `CommandRecord.canonicalId record`, which is
itself duration-free (F032).

## Segment encoding (identical to F029/F032)

Every scalar field is rendered as one **tagged, length-prefixed segment**:

```text
<tag> "=" <presence> <payload>
```

- `<tag>` — a fixed lowercase ASCII tag, unique per field (table below).
- **Required string value** `s` (UTF-8): `<presence>` is `1`, `<payload>` is `<byteLen> ":" s`, where
  `<byteLen>` is the decimal UTF-8 byte length of `s`. Example: source commit `"c0ffee"` → `src=16:c0ffee`
  (the `16` is the presence digit `1` immediately followed by the byte length `6`, then `:` begins the 6-byte
  payload). Every provenance scalar field is a **required** value — there is no optional/absent scalar at the
  provenance level (the optional wall-clock timestamp is not carried — D3).

The length prefix guarantees **injectivity**: a reader knows exactly how many bytes a value occupies, so no
value can contain a character (`:`, `=`, `\n`, `;`, a tag) that lets it masquerade as another field or bleed
across a boundary. Because every segment is self-delimiting by length, the `'\n'` joiner between segments is a
readability convention, not the delimiter — a `'\n'` *inside* a payload (as occurs inside an embedded command
id) is read by length and is harmless.

`EnvironmentClass` is rendered by a fixed total token, then encoded as a required string value:

| `EnvironmentClass` | token | segment |
|---|---|---|
| `Local` | `local` | `env=15:local` |
| `Ci` | `ci` | `env=12:ci` |
| `LocalOrCi` | `localOrCi` | `env=19:localOrCi` |
| `Release` | `release` | `env=17:release` |

These are the **same four tokens** F029's internal `environmentToken` uses (research D5); F029's helper is not
public, so this core replicates the four-case total match locally rather than calling it (F029 stays untouched).

## Field order and tags

The reproducible facts are joined by `'\n'` (no trailing newline) in this fixed order:

| # | Field | Tag | Encoding |
|---|---|---|---|
| 1 | `SourceCommit` (`Revision`) | `src` | required string |
| 2 | `Base` (`Revision`) | `base` | required string |
| 3 | `Head` (`Revision`) | `head` | required string |
| 4 | `RuleHash` | `rule` | required string |
| 5 | `GeneratorVersion` | `gen` | required string |
| 6 | `ArtifactDigests` | `art` | **set segment** (below) |
| 7 | `CommandRecords` | `cmds` | **ordered list segment** (below) |
| 8 | `Environment` | `env` | required string (token above) |
| 9 | `Builder` (`BuilderIdentity`) | `bld` | required string |

The three revisions share the `Revision` type but occupy distinct tags (`src` / `base` / `head`), so the same
revision string in two of them yields different segments (injective across fields).

## Artifact digests — set segment

The artifact digests are **order-independent and duplicate-collapsing** (FR-008; the F029/F032 set discipline).
Each `ArtifactHash` is unwrapped to its string; the strings are **deduplicated and ordinal-sorted**, then
encoded as a counted list:

```text
art=<count>;<len1>:<a1>;<len2>:<a2>;…      // sorted/deduped digest strings
```

`<count>` is the decimal element count *after* dedup; each element is its UTF-8 byte length, `:`, and the
digest string. Empty set ⇒ `art=0;`. Supplying the digests in a different order, or with duplicates, yields the
same sorted/deduped segment — hence the same identity (FR-008).

## Command records — ordered list segment

The command records are **order-significant** (research D4): they are rendered in their given order, **not**
sorted or deduplicated (the order of runs is reproducible provenance; a repeated run is a real repeat). Each
entry is the **full F032 canonical-id string** of that record — `CommandRecord.identityValue
(CommandRecord.canonicalId record)` — which is duration-free (F032), then length-prefixed:

```text
cmds=<count>;<len1>:<id1>;<len2>:<id2>;…    // ids in given order, each the full F032 canonical block
```

`<count>` is the decimal record count; each entry is the UTF-8 byte length of the embedded id, `:`, and the id
itself. Because an F032 canonical id **contains** `'\n'`, `:`, `;`, and `=`, the outer length prefix is what
keeps the entry injective — the reader consumes exactly `<lenK>` bytes regardless of their content. Empty list ⇒
`cmds=0;`. Reordering the command records changes this segment (and the identity); changing only an embedded
record's duration leaves every embedded id unchanged (durations are not in `CommandRecord.canonicalId`), so the
segment — and the provenance identity — is unchanged (FR-005).

## Worked example

A provenance whose reproducible facts are: source commit `c0ffee`; base `base1`; head `head2`; rule hash
`rule-x`; generator version `gen-1`; artifact digests `[ArtifactHash "a1"; ArtifactHash "a2"]`; **one** command
record — the F032 worked-example record (executable `gcc`; arguments `["-c"; "main.c"]`; cwd `/work`; env delta
`{ Added = [{Name="CI"; Value="1"}]; Changed = []; Removed = [] }`; timeout `30`; exit `0`; stdout `sha-out`;
stderr `sha-err`; captured output `NoCapturedOutput`; *any* duration), whose F032 canonical id is the 135-byte
block beginning `exe=13:gcc`; environment class `Local`; builder identity `ci-runner`:

```text
src=16:c0ffee
base=15:base1
head=15:head2
rule=16:rule-x
gen=15:gen-1
art=2;2:a1;2:a2
cmds=1;135:exe=13:gcc
args=2;2:-c;6:main.c
cwd=15:/work
env+=1;n:2:CI|v:1:1
env~=0;
env-=0;
to=12:30
exit=11:0
out=17:sha-out
err=17:sha-err
cap=0
env=15:local
bld=19:ci-runner
```

**Reading the `cmds` segment.** Line 7 begins `cmds=1;135:` — one record, then a `135`-byte payload that *is*
the F032 canonical-id block verbatim. That block contains its own newlines, so it visually spans lines 7–17
(`exe=13:gcc` … `cap=0`); it is read by its `135` length prefix, not by line boundaries. Lines 18 (`env=15:local`)
and 19 (`bld=19:ci-runner`) are the 8th and 9th provenance segments.

The whole block (joined by `'\n'`, no trailing newline) is wrapped as `ProvenanceIdentity`. Changing only the
command record's duration leaves the embedded F032 id (and the whole block) untouched — so two builds differing
only in command durations share the identity (FR-005/FR-006). Changing any reproducible field above — a
revision, the rule hash, the generator version, adding an artifact digest, reordering the command records, the
environment class, or the builder identity — changes the block, hence the identity (FR-006). Reordering or
duplicating the artifact digests leaves the `art` segment (and the identity) unchanged (FR-008).

## Properties (restated)

- **Deterministic & byte-stable** — identical reproducible facts (artifact digests as a set) ⇒ identical bytes
  (SC-005/SC-006).
- **Injective across fields** — length prefixes + unique tags ⇒ no field can masquerade as another, including
  the three same-typed revisions (FR-006).
- **Order-independent over the artifact digests, order-significant over the command records** (FR-008 vs D4).
- **Duration-free** — embedded durations are never encoded; each record contributes its duration-free F032 id
  (FR-005).
- **No hashing** — the canonical string *is* the identity; no digest is computed from bytes (FR-011).
