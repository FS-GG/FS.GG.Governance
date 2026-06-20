// Typed-fact model for the repository snapshot (F016). Visibility lives entirely in
// Model.fsi (Principle II): no top-level binding here carries an access modifier.
// These are plain records/DUs (Principle III) — the product-neutral, deterministic,
// git-wire-free values the edge senses and later route/ship features consume (FR-001, FR-015).
// Every path reuses the F014 `GovernedPath` (it is not redefined here), so the snapshot's
// `Changed` set feeds straight into F015 routing with no re-normalization (FR-002, SC-001).

namespace FS.GG.Governance.Snapshot

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── Refs & range ──

    type GitRef = GitRef of string

    type CommitId = CommitId of string

    type BranchName = BranchName of string

    type SnapshotOptions =
        { Since: GitRef option
          Base: GitRef option
          Head: GitRef option }

    type DiffRange =
        { Base: CommitId
          Head: CommitId
          MergeBase: CommitId }

    // ── Paths & changes ──

    type ChangeKind =
        | Added
        | Modified
        | Deleted
        | Renamed
        | Copied
        | TypeChanged

    type ChangedPath =
        { Path: GovernedPath
          Kind: ChangeKind
          OldPath: GovernedPath option }

    type WorkingTreeState =
        { Dirty: GovernedPath list
          Untracked: GovernedPath list }

    // ── CI / PR context (optional, never fabricated) ──

    type CiEnvironment =
        | LocalShell
        | Ci
        | Unknown

    type CiContext =
        { Environment: CiEnvironment
          PrLabels: string list
          RequiredStatusChecks: string list }

    // ── Provenance & diagnostics ──

    type CommandRunDigest =
        { Command: string
          Digest: string }

    type SensingDiagnosticId =
        | NotARepository
        | UnknownRef
        | GitUnavailable
        | GitCommandFailed
        | UnreadableWorkingTree
        | UnparsableGitOutput

    type SensingDiagnostic =
        { Id: SensingDiagnosticId
          Operation: string
          Message: string }

    // ── The aggregate ──

    type RepoSnapshot =
        { Range: DiffRange option
          Changed: ChangedPath list
          WorkingTree: WorkingTreeState
          Branch: BranchName option
          Ci: CiContext option
          Digests: CommandRunDigest list
          Diagnostics: SensingDiagnostic list }

    // ── Stable rendering (for messages, tests, and any later JSON) ──

    // Total, deterministic: every case maps to its lowerCamelCase wire token. A new
    // SensingDiagnosticId case is a compile error here until it gets a token (closed set).
    let sensingDiagnosticIdToken (id: SensingDiagnosticId) : string =
        match id with
        | NotARepository -> "notARepository"
        | UnknownRef -> "unknownRef"
        | GitUnavailable -> "gitUnavailable"
        | GitCommandFailed -> "gitCommandFailed"
        | UnreadableWorkingTree -> "unreadableWorkingTree"
        | UnparsableGitOutput -> "unparsableGitOutput"

    // Total, deterministic: every committed-change kind maps to its stable token.
    let changeKindToken (kind: ChangeKind) : string =
        match kind with
        | Added -> "added"
        | Modified -> "modified"
        | Deleted -> "deleted"
        | Renamed -> "renamed"
        | Copied -> "copied"
        | TypeChanged -> "typeChanged"
