# Contract: read-only git command set, porcelain parsing, and range resolution (F016)

This is the behavioral contract the three `.fsi` files implement. It is the authority for what the
edge runs, how the pure core parses it, and how loose options resolve to a concrete range. It exists so
the read-only guarantee, the parsing rules, and the resolution contract are fixed **before**
implementation (Constitution Principle I).

## 1. The closed, read-only git command set (FR-006, research D5)

The edge may issue ONLY these invocations. Read-only is guaranteed by construction — the `GitCommand`
DU contains no mutating subcommand. Every invocation runs with the repository working directory as the
process working directory and inherits no write intent.

| `GitCommand` | argv (after `git`) | reads | maps to |
|---|---|---|---|
| `RepoCheck` | `rev-parse --is-inside-work-tree` | repo presence | `NotARepository` when `false`/error |
| `RevParse r` | `rev-parse --verify <r>^{commit}` | a ref → commit id | `UnknownRef` on failure |
| `MergeBase (a,b)` | `merge-base <a> <b>` | three-dot base | `GitCommandFailed` on failure |
| `DiffNameStatus (b,h)` | `diff --name-status -z -M <b> <h>` | committed changes | `ChangedPath list` |
| `StatusPorcelain` | `status --porcelain=v1 -z` | dirty + untracked | `WorkingTreeState` |
| `CurrentBranch` | `rev-parse --abbrev-ref HEAD` | branch / detached | `Branch` (`"HEAD"` ⇒ `None`) |

Notes:
- All `-z` forms produce NUL-terminated, **unquoted** records, eliminating git's path-quoting
  ambiguity at the source (FR-012, research D4).
- `-M` enables rename detection so `R` records carry an old→new pair.
- The edge captures, per command, a `CommandRunDigest { Command = cmd.Token; Digest = hash(normalized
  stdout) }`. The raw stdout, exit timing, and pid are **never** placed in the snapshot facts (FR-010).
- A thrown exception (e.g. `git` not on `PATH`) is caught at the edge and becomes `GitUnavailable`
  (for `RepoCheck`/process-start failures) or `GitCommandFailed`; the edge never throws (FR-008).

## 2. Parsing `diff --name-status -z -M` (pure, research D4)

Records are NUL-delimited. For a non-rename/copy entry the record is `<X>\t<path>`; for rename/copy
(`R`/`C`, optionally with a similarity score like `R096`) the entry spans **three** NUL fields:
`<X>`, `<oldPath>`, `<newPath>`.

Mapping the status letter `X` → `ChangeKind`:

| letter | `ChangeKind` | `ChangedPath` shape |
|---|---|---|
| `A` | `Added` | `Path = new`, `OldPath = None` |
| `M` | `Modified` | `Path = new`, `OldPath = None` |
| `D` | `Deleted` | `Path = the deleted path`, `OldPath = None` |
| `R` (`Rxx`) | `Renamed` | `Path = newPath`, `OldPath = Some oldPath` |
| `C` (`Cxx`) | `Copied` | `Path = newPath`, `OldPath = Some oldPath` |
| `T` | `TypeChanged` | `Path = new`, `OldPath = None` |
| anything else | — | a single `UnparsableGitOutput` diagnostic; the record is not silently dropped |

Each `path`/`oldPath` is normalized to a repo-relative `GovernedPath` via the public `Config`
normalizer (research D7) — the SAME function F014 uses, so the result is byte-identical to what
`Routing.route` consumes (SC-001).

## 3. Parsing `status --porcelain=v1 -z` (pure)

Records are NUL-delimited; each is `XY <path>` (two status columns + a space + path), except renames in
the index which add a second NUL field for the original path. Categorization (FR-003, mutually
exclusive):

- `??` → **Untracked**.
- Any other non-space `X` or `Y` (e.g. ` M`, `M `, `MM`, `A `, ` D`, `R `) → **Dirty** (the path is
  tracked-but-modified or staged). For a rename record, the **current** (new) path is the dirty path.
- `!!` (ignored) is not emitted (`--porcelain` does not list ignored files without `--ignored`, which
  is not requested).

A path that appears in the committed `Changed` set is still listed in working-tree `Dirty`/`Untracked`
only if git's status reports it there; the snapshot keeps the two **planes** (committed vs working tree)
distinct — "exclusive" in FR-003/SC-003 means a path is not double-listed *within* the working-tree
plane (not both Dirty and Untracked), and the committed plane is reported separately by design.

## 4. Range resolution (US3, FR-004, research D8)

Pure `planResolution : SnapshotOptions -> ResolutionPlan` by this total contract:

| options | `RangeForm` | base resolved from | head resolved from | merge base |
|---|---|---|---|---|
| `Since = Some r` (base/head ignored) | `Since r` | `r` | current working position (`HEAD`) | yes |
| `Base = Some b, Head = Some h` | `BaseHead (b,h)` | `b` | `h` | yes |
| `Base = Some b` only | `BaseHead (b, HEAD)` | `b` | `HEAD` | yes |
| `Head = Some h` only | `BaseHead (defaultBase, h)` | the documented default base | `h` | yes |
| none | `Default` | the documented default base | current working position (`HEAD`) | yes |

- **Documented default base**: `HEAD` (i.e. the default range compares the working position against
  `HEAD`, surfacing only uncommitted work via the working-tree sets and an empty committed diff). The
  later `route`/`ship` commands MAY override the default (e.g. against a protected branch); choosing
  that policy is their row, not this feature's (FR-013). This feature fixes only the resolution
  *contract* and its default.
- The committed diff is always computed against the **merge base** (three-dot semantics), so commits
  that landed on the base branch after the branch point are not reported as this change's work
  (research D8). The edge runs `MergeBase (base, head)` and then `DiffNameStatus (mergeBase, head)`.
- `Since`/`Base`/`Head` precedence: `Since` wins when present; otherwise explicit `Base`/`Head`;
  otherwise the default. Identical options always resolve to the identical plan (no git involved),
  which is what makes US3 unit-testable without a repository.

## 5. Determinism & safe failure (FR-008..FR-011, SC-002/SC-005)

- Every snapshot collection is sorted deterministically: `Changed` by `Path`, `Dirty`/`Untracked` by
  value, `Digests` by command token, `Diagnostics` by `(id, operation)`.
- **Empty vs failure** is structural: empty `Changed`/working tree with empty `Diagnostics` and `Some
  Range` is "nothing changed"; any sensing failure yields a non-empty `Diagnostics` (and `Range = None`
  when range resolution failed). They are never conflated.
- **Read-only** is asserted both by construction (the command set) and empirically: a `SensingTests`
  case captures the fixture repo's state (a recursive content+ref hash) before and after `senseSnapshot`
  and asserts byte-identity (SC-005).
- **No network** is asserted by the injected `CiPort` (tests supply a deterministic context) and by the
  absence of any provider-API call anywhere in the library (SC-007).
