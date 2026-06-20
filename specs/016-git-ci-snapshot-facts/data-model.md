# Phase 1 Data Model: Git/CI Snapshot Facts

This is the typed shape of the repository snapshot and the raw sensing it is assembled from. It is the
authority the three `.fsi` contracts implement. All types are product-neutral and carry no raw git wire
text in the deterministic facts (FR-010). Paths are repo-relative `GovernedPath`s from
`FS.GG.Governance.Config.Model` (research D6/D7).

## 1. Public snapshot model (`Snapshot.Model`)

### Refs & range

- **`GitRef`** = `GitRef of string` — a caller-named reference (branch, tag, or commit-ish). Opaque;
  the snapshot never invents one.
- **`CommitId`** = `CommitId of string` — a git-resolved commit identity (the `rev-parse` output). The
  deterministic facts carry resolved `CommitId`s, not raw command text.
- **`SnapshotOptions`** — the caller's loose range inputs (US3, FR-004):
  - `Since: GitRef option` — `--since <rev>`: base is `<rev>`, head is the current working position.
  - `Base: GitRef option` — `--base <ref>`.
  - `Head: GitRef option` — `--head <ref>`.
  - (All `None` ⇒ the documented default plan; see `planResolution` / git-sensing.md.)
- **`DiffRange`** — the resolved range recorded in the snapshot (FR-001):
  - `Base: CommitId`
  - `Head: CommitId`
  - `MergeBase: CommitId` — the three-dot base the committed diff was computed against (research D8).

### Paths & changes

- **`ChangeKind`** (closed) — `Added | Modified | Deleted | Renamed | Copied | TypeChanged`. Mapped from
  the git `--name-status` letter (A/M/D/R/C/T); an unrecognized letter is a parse diagnostic, never a
  silent drop (git-sensing.md).
- **`ChangedPath`** — one committed change between base and head (FR-001, FR-012):
  - `Path: GovernedPath` — the new/current path, normalized (the rename *destination* for `Renamed`).
  - `Kind: ChangeKind`
  - `OldPath: GovernedPath option` — `Some` only for `Renamed`/`Copied` (the source), else `None`.
- **`WorkingTreeState`** — uncommitted state (US2, FR-003):
  - `Dirty: GovernedPath list` — tracked-but-modified (and staged) paths, normalized, deterministically
    ordered.
  - `Untracked: GovernedPath list` — paths git is not tracking (`??`), normalized, ordered.
  - Two distinct planes (FR-003, SC-003): the committed `Changed` plane and the working-tree plane.
    Within the working-tree plane a path is in at most one of `Dirty` / `Untracked`; the committed
    `Changed` plane is reported separately and MAY also contain a path that was modified again after
    being committed.

### CI / PR context (optional, never fabricated)

- **`CiEnvironment`** (closed) — `LocalShell | Ci | Unknown` — a closed classification, not free-form
  text (US4 scenario 3, FR-005).
- **`CiContext`** — optional runner-supplied metadata (FR-005):
  - `Environment: CiEnvironment`
  - `PrLabels: string list` — deterministically ordered; `[]` when none supplied.
  - `RequiredStatusChecks: string list` — required status-check identities; ordered; `[]` when none.
  - Present only when the runner supplies it; otherwise the whole `CiContext` is `None` on the snapshot.

### Provenance & diagnostics

- **`CommandRunDigest`** — deterministic provenance of one sensed git command (FR-010), kept **separate**
  from the path facts: `Command: string` (the closed `GitCommand` rendered to a stable token, e.g.
  `"diff-name-status"`), `Digest: string` (a stable hash of the normalized output). Carries **no** raw
  output, timing, pid, or absolute path.
- **`SensingDiagnosticId`** (closed) — one per failure class (FR-008, SC-005):
  `NotARepository | UnknownRef | GitUnavailable | GitCommandFailed | UnreadableWorkingTree |
  UnparsableGitOutput`.
- **`SensingDiagnostic`** — a stable-id, located, explained failure (FR-008, mirrors F014 `Diagnostic`
  style): `Id: SensingDiagnosticId`, `Operation: string` (the failed `GitCommand` token), `Message:
  string` (with a fix hint). Carries no raw stderr dump.

### The aggregate

- **`RepoSnapshot`** — the deterministic, product-neutral result (FR-001, FR-015):
  - `Range: DiffRange option` — `None` only when range resolution itself failed (a diagnostic explains);
    `Some` otherwise, even for an empty diff.
  - `Changed: ChangedPath list` — empty-but-present for a genuine empty diff (FR-011); ordered by `Path`.
  - `WorkingTree: WorkingTreeState`
  - `Branch: BranchName option` — the current branch, `None` under detached HEAD (never fabricated).
  - `Ci: CiContext option`
  - `Digests: CommandRunDigest list` — ordered by command token.
  - `Diagnostics: SensingDiagnostic list` — ordered by `(id, operation)`; non-empty iff some sensing
    operation failed. A failure is **never** conflated with an empty success (FR-011).

> A snapshot with a non-empty `Diagnostics` and the operation that failed is the safe-failure outcome;
> a snapshot with empty `Diagnostics`, `Some Range`, and empty `Changed`/`Dirty`/`Untracked` is the
> genuine "nothing changed" outcome. The two are structurally distinct (FR-011).

## 2. Raw sensing intermediate (`Snapshot`, pure boundary input)

The edge fills this; the pure `assemble` consumes it. It carries the *raw* command results so all
parsing stays pure (research D4).

- **`ResolutionPlan`** — output of pure `planResolution`: which `GitRef`s to resolve as base/head and
  whether a merge base is needed (the pure half of D8). Records the chosen option form for diagnostics.
- **`RawSensing`** — every sensed result as `Result<string,string>` (raw stdout or an error reason):
  `BaseResolved`, `HeadResolved`, `MergeBaseResolved` (`Result<CommitId,string>` after the edge runs
  `rev-parse`/`merge-base`), `DiffRaw`, `StatusRaw` (raw `-z` text), `BranchRaw`, plus `RepoOk: bool`
  (from `RepoCheck`), the optional `RawCi`, and the `CommandRunDigest list` the edge accumulated. Each
  `Error` becomes the matching `SensingDiagnostic` in `assemble`.

## 3. Validation & invariants (enforced by `assemble`, asserted by tests)

| Invariant | Source | Test |
|---|---|---|
| Every path is a normalized repo-relative `GovernedPath` (via the Config normalizer) | FR-002, D7 | `AssembleTests`, `RoutingFeedTests` (SC-001) |
| `Dirty`/`Untracked` mutually exclusive (working-tree plane); committed `Changed` is a separate plane | FR-003 | `AssembleTests` (SC-003) |
| All collections deterministically ordered (paths by value; digests/diagnostics by token) | FR-009 | `DeterminismTests` (SC-002) |
| Re-assembling permuted raw entries yields an identical snapshot | FR-009 | `DeterminismTests` FsCheck (SC-003) |
| Empty diff ⇒ empty `Changed` + empty `Diagnostics` + `Some Range`; failure ⇒ `Diagnostics` non-empty | FR-011 | `AssembleTests`, `SensingTests` (SC-005) |
| No raw output / timing / abs path in any deterministic field | FR-010 | `AssembleTests`, surface review (SC-006) |
| Rename carries `OldPath = Some src`, `Path = dest`; delete carries the deleted path with `Deleted` | FR-012 | `ParseTests` |
| Detached HEAD ⇒ `Branch = None`; no fabrication | FR-005 | `SensingTests` |
| Unknown ref / not-a-repo / git-missing ⇒ the matching stable diagnostic, no throw | FR-008 | `SensingTests` (SC-005) |
| Read-only: fixture repo byte-identical before/after sensing | FR-006 | `SensingTests` (SC-005) |

## 4. Consumed F014/F015 types (no redefinition)

- `FS.GG.Governance.Config.Model.GovernedPath` — the path form; the snapshot emits these directly.
- The new public `Config` normalization `val` (research D7) — single source of the normalized form.
- `FS.GG.Governance.Config.Model.ProjectFacts.GovernedRoot` — carried through for downstream
  convenience; **not** used by the snapshot to drop/classify paths (research D6).
- `FS.GG.Governance.Routing.Routing.route` — *test-only* consumer in `RoutingFeedTests` proving the
  snapshot's `Changed` paths route with no re-normalization (SC-001). The production Snapshot library
  does **not** reference Routing.
