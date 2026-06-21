# Contract: Canonical Command-Identity Encoding (F032)

The byte-level format of the `CommandIdentity` string produced by `CommandRecord.canonicalId` (research D6).
It is the stable, comparable, **injective**, inspectable rendering of a record's `ReproducibleFacts`. This is
the **same tagged, length-prefixed discipline** as the F029 freshness key
(`specs/029-*/contracts/freshness-key-format.md`); the laws below restate it for the command-record fields.

Identical reproducible facts (environment-delta classes compared as sets) MUST produce a byte-identical
identity; any single differing reproducible fact MUST produce a different identity; the same opaque string
placed in two different fields MUST produce different identities. The **duration is never rendered** (it is
sensed — FR-005).

## Segment encoding

Every scalar field is rendered as one **tagged, length-prefixed segment**:

```text
<tag> "=" <presence> <payload>
```

- `<tag>` — a fixed lowercase ASCII tag, unique per field (table below).
- **Required string value** `s` (UTF-8): `<presence>` is `1`, `<payload>` is `<byteLen> ":" s`, where
  `<byteLen>` is the decimal UTF-8 byte length of `s`. Example: executable `"gcc"` → `exe=13:gcc`.
  **Read carefully**: the `13` here is the presence digit `1` immediately followed by the byte length `3`
  (not the number "thirteen"); the `:` after it begins the 3-byte payload `gcc`.
- **Absent** (the `NoCapturedOutput` case): `<presence>` is `0`, no payload. Example: `cap=0`.
- **Present** optional value `Some s` (the `CapturedAt` case): identical to a required value (`1`, then
  `<byteLen> ":" s`). So `NoCapturedOutput` → `cap=0` is distinct from `CapturedAt (CapturedOutputPath "")` →
  `cap=10:` (presence `1`, length `0`).

The length prefix guarantees **injectivity**: because a reader knows exactly how many bytes a value occupies,
no value can contain a character (`:`, `=`, `\n`, a tag, a list separator) that lets it masquerade as another
field or bleed across a boundary. The presence digit keeps absence distinct from an empty present value.

`ExitCode` and `TimeoutLimit` are rendered by their decimal integer text, then encoded as a required string
value (e.g. exit code `0` → `exit=11:0`; timeout `30` → `to=12:30`). `SensedDuration` is **not rendered**.

## Field order and tags

The reproducible facts are joined by `'\n'` (no trailing newline) in this fixed order:

| # | Field | Tag | Encoding |
|---|---|---|---|
| 1 | `Executable` | `exe` | required string |
| 2 | `Arguments` | `args` | **ordered list segment** (below) |
| 3 | `WorkingDirectory` | `cwd` | required string |
| 4 | `Environment` (added) | `env+` | **set segment** (below) |
| 5 | `Environment` (changed) | `env~` | **set segment** (below) |
| 6 | `Environment` (removed) | `env-` | **set segment** (below) |
| 7 | `Timeout` | `to` | required string (decimal seconds) |
| 8 | `ExitCode` | `exit` | required string (decimal) |
| 9 | `StdoutDigest` | `out` | required string |
| 10 | `StderrDigest` | `err` | required string |
| 11 | `CapturedOutput` | `cap` | optional string (`0` ⇒ `NoCapturedOutput`; present ⇒ the path) |

## Arguments — ordered list segment

Arguments are **order-significant** (FR-006 sensitivity; D6): they are rendered in their given order, **not**
sorted or deduplicated.

```text
args=<count>;<len1>:<a1>;<len2>:<a2>;…
```

`<count>` is the decimal element count; each element is its UTF-8 byte length, `:`, and the argument value.
Empty list ⇒ `args=0;`. Reordering arguments changes this segment (and the identity); a repeated argument is
kept (it is a real repeat on the command line).

## Environment-delta classes — set segments

Each of the three classes (`Added`, `Changed`, `Removed`) is **order-independent and duplicate-collapsing**
(FR-007; the F029 set discipline). Each entry is first rendered to a canonical per-entry string, then the
class is **deduplicated and ordinal-sorted**, then encoded as a counted list:

```text
env+=<count>;<len1>:<e1>;<len2>:<e2>;…      // Added, sorted/deduped entry strings
env~=<count>;<len1>:<e1>;…                  // Changed
env-=<count>;<len1>:<e1>;…                  // Removed
```

Per-entry canonical strings (themselves length-prefixed so name/value boundaries are injective):

- **Added** `{ Name = n; Value = v }` → `n:<byteLen n>:<n>|v:<byteLen v>:<v>`
- **Changed** `{ Name = n; Old = o; New = w }` → `n:<byteLen n>:<n>|o:<byteLen o>:<o>|w:<byteLen w>:<w>`
- **Removed** `{ Name = n; Old = o }` → `n:<byteLen n>:<n>|o:<byteLen o>:<o>`

Empty class ⇒ e.g. `env+=0;`. Because the three classes have distinct tags (`env+`/`env~`/`env-`) and a
changed entry carries both `o` and `w`, a changed variable can **never** encode the same bytes as an `Added` +
`Removed` pair (FR-002). Supplying a class's entries in a different order, or with duplicates, yields the same
sorted/deduped segment — hence the same identity (FR-007).

## Worked example

A record whose reproducible facts are: executable `gcc`; arguments `["-c"; "main.c"]`; cwd `/work`; env delta
`{ Added = [ {Name="CI"; Value="1"} ]; Changed = []; Removed = [] }`; timeout `30`; exit code `0`; stdout
digest `sha-out`; stderr digest `sha-err`; captured output `NoCapturedOutput`:

```text
exe=13:gcc
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
```

The whole block (joined by `'\n'`, no trailing newline) is wrapped as `CommandIdentity`. Changing the duration
does not touch this block (the duration is not rendered) — so two runs differing only in duration share the
identity (FR-005/FR-006). Changing any field above — including reordering the two arguments — changes the
block, hence the identity (FR-006). Adding a duplicate `CI` entry to the env delta, or reordering env entries,
leaves the `env+` segment (and the identity) unchanged (FR-007).

## Properties (restated)

- **Deterministic & byte-stable** — identical reproducible facts (env classes as sets) ⇒ identical bytes.
- **Injective across fields** — length prefixes + unique tags ⇒ no field can masquerade as another (FR-006).
- **Order-independent over the env delta, order-significant over arguments** (FR-007 vs FR-006/D6).
- **Duration-free** — the sensed measure is never encoded (FR-005).
- **No hashing** — the canonical string *is* the identity; no digest is computed from bytes (FR-010).
