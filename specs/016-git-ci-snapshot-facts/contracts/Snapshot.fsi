// Curated public signature contract for the PURE core of repository-snapshot sensing (F016).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Snapshot.fs carries NO `private`/`internal`/`public` modifiers on
// top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Snapshot.fs body exists (Principle I). This is the PURE side of the I/O boundary
// (Constitution Principle IV, the lighter port/effect algebra — research D3, as F014's Loader):
// `planResolution` decides which refs to resolve from the options (pure), and `assemble` parses
// the raw sensed text, normalizes paths, categorizes, orders, and diagnoses them into a
// `RepoSnapshot`. Both are PURE and TOTAL — no I/O, no git, no clock, never throw, byte-for-byte
// identical for identical input (FR-009/FR-011, SC-002/SC-003). The actual git/CI sensing that
// fills `RawSensing` is the edge `Interpreter`'s job (Interpreter.fsi). Path normalization is
// single-sourced from the public `Config` normalizer (research D7), so the emitted `GovernedPath`s
// are byte-identical to what `Routing.route` consumes (SC-001).

namespace FS.GG.Governance.Snapshot

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Snapshot =

    // ── Pure range planning (the pure half of D8) ──

    /// Which option form the caller supplied — recorded for diagnostics and chosen by
    /// `planResolution` (US3). Closed.
    type RangeForm =
        /// `--since <rev>`: base = `<rev>`, head = the current working position.
        | Since of GitRef
        /// explicit `--base <ref> --head <ref>`.
        | BaseHead of baseRef: GitRef * headRef: GitRef
        /// no options: the documented default (git-sensing.md §Range resolution).
        | Default

    /// The pure plan of which refs to resolve and whether a merge base is needed before any git
    /// runs. The edge consults this to know which read-only resolutions to perform (research D8).
    type ResolutionPlan =
        { Form: RangeForm
          /// the ref to resolve as the diff base (None for `Since` head-side / `Default` until the
          /// documented default ref is applied — see git-sensing.md).
          BaseRef: GitRef option
          /// the ref to resolve as the diff head; None ⇒ the current working position.
          HeadRef: GitRef option
          /// whether the committed diff is computed against the merge base (the three-dot default).
          UseMergeBase: bool }

    /// PURE, TOTAL: map the caller's loose `SnapshotOptions` to a concrete `ResolutionPlan` by the
    /// documented contract (US3, FR-004). Identical options ⇒ identical plan, with no git
    /// involved — so the option-form contract is unit-tested without a repository (research D8).
    val planResolution: options: SnapshotOptions -> ResolutionPlan

    // ── Raw sensing intermediate (filled by the edge, consumed here) ──

    /// Every sensed git result as raw text or an error reason — the pure boundary input the edge
    /// (`Interpreter.senseSnapshot`) fills and `assemble` consumes (research D4). Keeping the raw
    /// `-z` output here (not pre-parsed) is what makes porcelain parsing a PURE, literal-fixture-
    /// tested function rather than repo-only impure logic.
    type RawSensing =
        { /// `false` ⇒ `RepoCheck` said this is not a work tree ⇒ `NotARepository` (FR-008).
          RepoOk: bool
          /// resolved base/head/merge-base commit ids, or an error reason (unknown ref, etc.).
          BaseResolved: Result<CommitId, string>
          HeadResolved: Result<CommitId, string>
          MergeBaseResolved: Result<CommitId, string>
          /// raw `git diff --name-status -z -M <base> <head>` stdout, or an error reason.
          DiffRaw: Result<string, string>
          /// raw `git status --porcelain=v1 -z` stdout (dirty + untracked), or an error reason.
          StatusRaw: Result<string, string>
          /// raw `git rev-parse --abbrev-ref HEAD` stdout (`"HEAD"` ⇒ detached), or an error.
          BranchRaw: Result<string, string>
          /// runner-supplied CI context, already read from the environment by the edge (D9).
          RawCi: CiContext option
          /// the provenance digests the edge accumulated, in command order (FR-010).
          Digests: CommandRunDigest list
          /// which plan the edge resolved against (echoed for assembly + diagnostics).
          Plan: ResolutionPlan }

    // ── Pure assembly (the heart of the feature) ──

    /// PURE, TOTAL: parse, normalize, categorize, order, and diagnose `RawSensing` into a
    /// `RepoSnapshot`. Performs NO I/O and NEVER throws; identical input ⇒ byte-identical output
    /// (FR-009, SC-002/SC-003).
    ///
    /// Behavior (FR-001..FR-012):
    ///   • `RepoOk = false` ⇒ a `NotARepository` diagnostic; `Range = None`; empty path sets.
    ///   • Each `Error` in `BaseResolved`/`HeadResolved`/`MergeBaseResolved` ⇒ the matching
    ///     `UnknownRef`/`GitCommandFailed` diagnostic and `Range = None` (FR-008); all three `Ok`
    ///     ⇒ `Some { Base; Head; MergeBase }`.
    ///   • `DiffRaw Ok` is parsed as NUL-delimited `--name-status` records into `ChangedPath`s
    ///     (rename old/new pairs honored; unknown status letter ⇒ `UnparsableGitOutput`); each
    ///     path normalized to a repo-relative `GovernedPath` via the Config normalizer (D7).
    ///   • `StatusRaw Ok` is parsed as NUL-delimited porcelain into `Dirty` (tracked-modified/
    ///     staged) and `Untracked` (`??`), each normalized; categories are mutually exclusive and
    ///     exclusive of `Changed` (FR-003).
    ///   • `BranchRaw Ok "HEAD"` ⇒ `Branch = None` (detached); any other ⇒ `Some (BranchName ...)`.
    ///   • A genuinely empty diff ⇒ empty `Changed` with empty `Diagnostics` and `Some Range` —
    ///     DISTINCT from any failure (FR-011).
    ///   • Every collection is sorted deterministically: `Changed` by `Path`, `Dirty`/`Untracked`
    ///     by value, `Digests` by command token, `Diagnostics` by `(id, operation)` (FR-009).
    val assemble: raw: RawSensing -> RepoSnapshot
