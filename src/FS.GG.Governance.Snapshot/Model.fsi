// Curated public signature contract for the typed repository-snapshot model (F016).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Model.fs carries NO `private`/`internal`/`public` modifiers
// on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Model.fs body exists (Principle I). It is the product-neutral, deterministic, git-wire-free
// typed result of sensing a repository's change boundary: the resolved diff range, the
// committed changed-path set, the working-tree dirty/untracked sets, the branch, optional
// runner-supplied CI/PR context, command-run provenance digests, and any sensing diagnostics.
// Every path is a repo-relative `GovernedPath` (Config.Model) in F014's normalized form, so
// the `Changed` set feeds straight into `Routing.route` (F015) with no re-normalization
// (FR-002, SC-001). NO field carries raw git output, timing, process ids, or absolute host
// paths (FR-010). Every collection is emitted in deterministic order (FR-009, SC-002). This
// module performs NO I/O — sensing lives at the edge (Interpreter.fsi).

namespace FS.GG.Governance.Snapshot

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── Refs & range ──

    /// A caller-named reference (branch, tag, or commit-ish). Opaque; the snapshot never
    /// invents one (FR-004).
    type GitRef = GitRef of string

    /// A git-resolved commit identity (the `rev-parse` output). The deterministic facts carry
    /// resolved `CommitId`s, never raw command text (FR-010).
    type CommitId = CommitId of string

    /// The current branch name, when one exists. `None` under detached HEAD — never fabricated
    /// (FR-005).
    type BranchName = BranchName of string

    /// The caller's loose range inputs (US3, FR-004). All `None` ⇒ the documented default plan
    /// (Snapshot.planResolution / contracts/git-sensing.md).
    type SnapshotOptions =
        { Since: GitRef option
          Base: GitRef option
          Head: GitRef option }

    /// The resolved diff range recorded in the snapshot (FR-001). `MergeBase` is the three-dot
    /// base the committed diff was computed against, so unrelated upstream commits on a stale
    /// base branch are not reported (research D8).
    type DiffRange =
        { Base: CommitId
          Head: CommitId
          MergeBase: CommitId }

    // ── Paths & changes ──

    /// The kind of a committed change, mapped from the git `--name-status` letter. Closed; an
    /// unrecognized letter is a parse diagnostic, never a silent drop (git-sensing.md).
    type ChangeKind =
        | Added
        | Modified
        | Deleted
        | Renamed
        | Copied
        | TypeChanged

    /// One committed change between base and head (FR-001, FR-012). `Path` is the new/current
    /// path (the rename DESTINATION for `Renamed`/`Copied`); `OldPath` is `Some` only for those.
    /// Both are repo-relative `GovernedPath`s in F014's normalized form (FR-002).
    type ChangedPath =
        { Path: GovernedPath
          Kind: ChangeKind
          OldPath: GovernedPath option }

    /// Uncommitted working-tree state (US2, FR-003). The committed `Changed` set and this
    /// working-tree state are TWO DISTINCT PLANES: within this plane a path is in at most one of
    /// `Dirty` / `Untracked`; the committed `Changed` plane is reported separately and MAY also
    /// list a path modified again after commit (SC-003). Each list is normalized and
    /// deterministically ordered.
    type WorkingTreeState =
        { Dirty: GovernedPath list
          Untracked: GovernedPath list }

    // ── CI / PR context (optional, never fabricated) ──

    /// A CLOSED CI environment classification — not free-form environment text (US4, FR-005).
    type CiEnvironment =
        | LocalShell
        | Ci
        | Unknown

    /// Optional runner-supplied context (FR-005). Present only when the runner supplies it;
    /// otherwise the whole value is `None` on the snapshot. Read from runner ENVIRONMENT only —
    /// NEVER a hosting-provider API call (research D9, SC-007). Lists are deterministically
    /// ordered and `[]` (not fabricated) when absent.
    type CiContext =
        { Environment: CiEnvironment
          PrLabels: string list
          RequiredStatusChecks: string list }

    // ── Provenance & diagnostics ──

    /// Deterministic provenance of one sensed git command (FR-010), kept SEPARATE from the path
    /// facts. `Command` is the stable token of the closed `GitCommand` (e.g. "diff-name-status");
    /// `Digest` is a stable hash of the normalized output. Carries NO raw output, timing, pid, or
    /// absolute path. A later phase (Phase 11 freshness keys) consumes these.
    type CommandRunDigest =
        { Command: string
          Digest: string }

    /// The CLOSED set of stable sensing-failure ids — one per failure class (FR-008, SC-005).
    type SensingDiagnosticId =
        | NotARepository
        | UnknownRef
        | GitUnavailable
        | GitCommandFailed
        | UnreadableWorkingTree
        | UnparsableGitOutput

    /// A stable-id, located, explained sensing failure (FR-008; mirrors F014's `Diagnostic`
    /// style). `Operation` is the failed `GitCommand` token; `Message` carries a fix hint. NO raw
    /// stderr dump.
    type SensingDiagnostic =
        { Id: SensingDiagnosticId
          Operation: string
          Message: string }

    // ── The aggregate ──

    /// The deterministic, product-neutral snapshot of a change boundary (FR-001, FR-015).
    ///
    /// SAFE-FAILURE vs EMPTY (FR-011): a non-empty `Diagnostics` (with `Range = None` when range
    /// resolution itself failed) is the failure outcome; empty `Diagnostics` + `Some Range` +
    /// empty `Changed`/working-tree is the genuine "nothing changed" outcome. The two are
    /// structurally distinct and never conflated.
    type RepoSnapshot =
        { Range: DiffRange option
          Changed: ChangedPath list
          WorkingTree: WorkingTreeState
          Branch: BranchName option
          Ci: CiContext option
          Digests: CommandRunDigest list
          Diagnostics: SensingDiagnostic list }

    // ── Stable rendering (for messages, tests, and any later JSON) ──

    /// The stable wire token for a `SensingDiagnosticId` (e.g. `UnknownRef` → `"unknownRef"`).
    /// Deterministic and total.
    val sensingDiagnosticIdToken: id: SensingDiagnosticId -> string

    /// The stable wire token for a `ChangeKind` (e.g. `Renamed` → `"renamed"`). Deterministic
    /// and total.
    val changeKindToken: kind: ChangeKind -> string
