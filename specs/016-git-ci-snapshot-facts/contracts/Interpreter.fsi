// Curated public signature contract for the EDGE of repository-snapshot sensing (F016).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Interpreter.fs carries NO `private`/`internal`/`public` modifiers
// on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Interpreter.fs body exists (Principle I).
//
// This is the EDGE side of the I/O boundary (Constitution Principle IV): the ONLY impure code
// in the feature. It runs the closed, READ-ONLY `GitCommand` set against an injected `GitPort`
// (the real one drives `git` via BCL `System.Diagnostics.Process`), reads optional CI context
// through an injected `CiPort` (BCL `System.Environment` — NEVER a hosting-provider API,
// research D9), gathers everything into a `RawSensing`, and applies the pure `Snapshot.assemble`.
// It NEVER throws out of itself: a thrown `Process` error or a nonzero git exit becomes the
// matching `Error` and then a `SensingDiagnostic` (FR-008, mirrors the F08 Host interpreter).
// READ-ONLY is guaranteed by CONSTRUCTION (FR-006): the `GitCommand` DU contains only read
// subcommands — there is no mutating command to issue (research D5). No new dependency.

namespace FS.GG.Governance.Snapshot

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot.Model

/// The CLOSED set of READ-ONLY git invocations the edge may issue (research D5). Modeling the
/// command set as a DU makes "read-only" a TYPE-LEVEL guarantee: no `add`/`commit`/`checkout`/
/// `reset`/`config --set` is representable. See contracts/git-sensing.md for the exact argv.
type GitCommand =
    /// `git rev-parse --is-inside-work-tree` — not-a-repo detection (→ `NotARepository`).
    | RepoCheck
    /// `git rev-parse --verify <ref>^{commit}` — resolve a ref to a commit (unknown ⇒ Error).
    | RevParse of GitRef
    /// `git merge-base <a> <b>` — the three-dot base for the diff range.
    | MergeBase of a: CommitId * b: CommitId
    /// `git diff --name-status -z -M <base> <head>` — committed changes (rename-aware).
    | DiffNameStatus of baseId: CommitId * headId: CommitId
    /// `git status --porcelain=v1 -z` — dirty + untracked in one read.
    | StatusPorcelain
    /// `git rev-parse --abbrev-ref HEAD` — current branch (`"HEAD"` ⇒ detached).
    | CurrentBranch

    /// The stable token for a command (e.g. `DiffNameStatus _` → "diff-name-status"), used for
    /// `CommandRunDigest.Command` and `SensingDiagnostic.Operation`. Deterministic; carries no argv
    /// values that would leak nondeterministic data.
    member Token: string

/// The injected SENSING port: run one read-only `GitCommand` and return its raw stdout (`Ok`) or
/// a failure reason (`Error`). The real port runs `git` via `System.Diagnostics.Process` in the
/// repository working directory; the interpreter ALSO guards against a thrown exception (git
/// missing, process failure), converting either to the matching `SensingDiagnostic` (FR-008).
/// Tests back it with a REAL temp git fixture repository (Principle V).
type GitPort = GitCommand -> Result<string, string>

/// The injected CI-context port: read optional runner-supplied context from the ENVIRONMENT, or
/// `None` when unavailable (research D9, FR-005). NEVER performs network I/O. Injected so tests
/// supply a deterministic context and the production port reads BCL `System.Environment`.
type CiPort = unit -> CiContext option

/// The bundle of injected edge ports — everything impure the sensing touches (FR-007). Wholly
/// faked/realised in tests (a real-git `Git` over a temp repo, a deterministic `Ci`) so no
/// hosting-provider API is ever reached (SC-007).
type Ports =
    { Git: GitPort
      Ci: CiPort }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// Build the REAL ports for a repository working directory: a `GitPort` that runs the
    /// closed read-only command set via `System.Diagnostics.Process` against `repoDir`, and a
    /// `CiPort` that reads runner context from `System.Environment`. This is the ONLY place the
    /// feature starts a process or touches the environment; it performs ONLY read-only git
    /// (FR-006) and reaches NO network (research D9).
    val realPorts: repoDir: string -> Ports

    /// Sense a `RepoSnapshot` for the given options against the injected ports — the single
    /// composition of edge I/O + the pure core (mirrors `Config.Loader.loadAndValidate`).
    ///
    /// It (1) `planResolution options`; (2) runs `RepoCheck`, the planned `RevParse`s, `MergeBase`,
    /// `DiffNameStatus`, `StatusPorcelain`, and `CurrentBranch` through `ports.Git`, accumulating a
    /// `CommandRunDigest` per command (FR-010); (3) reads `ports.Ci ()`; (4) bundles a `RawSensing`
    /// and returns `Snapshot.assemble raw`.
    ///
    /// TOTAL and SAFE (FR-008, SC-005): every port `Error` and every thrown exception is caught and
    /// reified — `senseSnapshot` NEVER throws and NEVER returns an empty-looking success for a
    /// failure (a failed operation yields a `SensingDiagnostic`, FR-011). READ-ONLY (FR-006): only
    /// `GitCommand`s are issued, all read subcommands. Reaches NO network (research D9, SC-007).
    val senseSnapshot: ports: Ports -> options: SnapshotOptions -> RepoSnapshot
