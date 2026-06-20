# Glob Syntax and Precedence Contract (F015)

This is the authoring/behaviour contract the `Glob` and `Routing` signatures realize. It is
the human-readable companion to [`Glob.fsi`](./Glob.fsi) and [`Routing.fsi`](./Routing.fsi),
and it is the source the precedence and matcher tests assert against.

All globs and candidate paths are **F014-normalized `GovernedPath` values**: separators unified
to `/`, `.`/`..` resolved, kept relative to the governed root, case preserved. Routing performs
no further normalization (FR-003, research D8).

## 1. Supported glob syntax (closed MVP set, FR-002)

A glob is a `/`-separated sequence of segments. Within the supported set:

| Construct | Meaning | Crosses `/`? |
|---|---|---|
| literal text | matches exactly those characters | no |
| `?` | matches exactly one character | no |
| `*` | matches zero or more characters within one segment | no |
| `**` (a whole segment) | matches zero or more whole segments | yes |

Rules:

- `*` is a single-segment wildcard: `src/*.fs` matches `src/Eval.fs` but **not** `src/a/b.fs`.
- `**` is only meaningful as a **whole segment**. `src/**` matches `src/a.fs` and `src/a/b/c.fs`
  (zero-or-more segments after `src/`). `**` does **not** make `src/**` match the bare `src`
  (the `src/` prefix is required).
- `?` matches one character and never a `/`.
- Any character that is not `*`, `?`, or `/` is literal — **except** the reserved set below.

### Reserved-but-unimplemented characters (FR-010)

The characters `[ ] { } ! ( )` belong to richer glob dialects (character classes, brace
expansion, negation, groups) that the MVP does **not** implement. A glob containing any of them
is reported as `UnsupportedGlobSyntax` and excluded from matching — it is **never** treated as a
literal that silently matches nothing (`Glob.checkSyntax` → `Error`; research D6).

## 2. Matching (FR-002)

`Glob.matches glob path` splits both on `/` and matches segment-by-segment. A `**` segment is a
zero-or-more-segment wildcard resolved by backtracking; non-`**` segments match position-for-
position, with `*`/`?` resolved within the segment. Matching is ordinal/case-sensitive.

## 3. Precedence — selecting one winner when several globs match (FR-005)

When more than one path-map glob matches a candidate path, exactly one wins. Precedence is a
**total order computed from the glob strings alone** (never from the path), so a glob's rank is
constant across every path it matches. Globs are compared by this ascending key
(`Glob.specificity`), smaller = higher precedence:

1. **Exact-literal** — a wildcard-free glob equal to the path ranks above every wildcard glob.
2. **Literal-segment count** (more ranks higher) — segments containing no `*`/`?`/`**`.
   `src/Adapters/**` (literal `{src, Adapters}`) beats `src/**` (literal `{src}`).
3. **`**` count** (fewer ranks higher) — `src/*` beats `src/**` for `src/a`: a single-segment
   `*` is more specific than a cross-segment `**`.

Two globs that are **equal** on (1)–(3) are *co-specific* — `Glob.specificity` is exactly this
3-rung key. The final, total tiebreak (`Glob.compare`) is:

4. **Ordinal glob string** — the lexicographically-first glob wins.

Literal-character length is **not** a precedence rung: an earlier draft made it rung 4, which both
contradicted the worked ambiguity example below and made `AmbiguousRoute` nearly impossible to
trigger. The 3-rung key matches spec FR-005 verbatim.

A path matching ≥1 glob therefore **always** has a unique winner; nothing is left unrouted.

### Precedence reason (FR-013, explainability)

The winner records why it won (`Model.PrecedenceReason`): `OnlyMatch` (sole match),
`ExactLiteral` (rung 1), `MoreSpecific` (rungs 2–3), or `LexicographicTiebreak` (co-specific —
separated only by rung 4).

## 4. Ambiguity (FR-006)

When the two top competitors are co-specific (`Glob.isAmbiguousPair` — equal under rungs 1–3,
separated only by the ordinal tiebreak), routing:

- **still resolves** the path to the ordinal-first glob (downstream stays total), with reason
  `LexicographicTiebreak`; and
- **also emits** an `AmbiguousRoute` diagnostic naming the path and both competing globs/domains
  with a fix hint to disambiguate.

It is never a silent arbitrary pick.

## 5. Catalog-shape diagnostics

- **`ConflictingGlobBinding` (FR-009)** — two path-map entries that normalize to the **same**
  glob string but bind **different** capability domains. Reported deterministically (naming both
  domains) rather than resolved by authored order. Such a glob is excluded from routing.
- **`UnsupportedGlobSyntax` (FR-010)** — see §1; the glob is excluded from matching.

## 6. Governed-root scoping (FR-007/FR-008)

A candidate path is **in root** iff it equals, or is a segment-prefixed descendant of,
`ProjectFacts.GovernedRoot`. An in-root path matching no glob is `UnmatchedInRoot`; a path not
under the governed root is `OutOfScope` (never routed, never an ambiguity). This is a pure
segment-prefix test — no I/O.

## 7. Worked examples

Path map (glob → domain): `src/**` → `core`, `src/Adapters/**` → `adapters`,
`src/Kernel/Eval.fs` → `kernel-eval`, `docs/**` → `docs`. Governed root: `.` (repo root).

| Candidate path | Matches | Winner | Reason |
|---|---|---|---|
| `src/Kernel/Eval.fs` | `src/**`, `src/Kernel/Eval.fs` | `kernel-eval` | `ExactLiteral` |
| `src/Adapters/SpecKit.fs` | `src/**`, `src/Adapters/**` | `adapters` | `MoreSpecific` (rung 2) |
| `src/Cli/Host.fs` | `src/**` | `core` | `OnlyMatch` |
| `docs/guide.md` | `docs/**` | `docs` | `OnlyMatch` |
| `README.md` | (none; in root) | — | `UnmatchedInRoot` |
| `/etc/passwd`-style path outside root | (n/a) | — | `OutOfScope` |

Single- vs cross-segment example (path map `src/*` → `a`, `src/**` → `b`; path `src/x`):
both match; `src/*` wins (rung 3, fewer `**`), reason `MoreSpecific`.

Ambiguity example (path map `src/*/Eval.fs` → `a`, `src/Kernel/*.fs` → `b`; path
`src/Kernel/Eval.fs`): both match and are co-specific — each has the same wildcard-free flag, two
literal segments (`{src, Eval.fs}` vs `{src, Kernel}`), and zero `**`, so they tie on rungs 1–3.
Routing resolves to the ordinal-first glob (`src/*/Eval.fs`, since `*` < `K`) with reason
`LexicographicTiebreak` **and** emits an `AmbiguousRoute` diagnostic naming both.
