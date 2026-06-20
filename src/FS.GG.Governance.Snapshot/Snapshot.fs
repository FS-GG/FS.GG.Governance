// The PURE core of repository-snapshot sensing (F016). Visibility lives in Snapshot.fsi
// (Principle II). `planResolution` and `assemble` are TOTAL and perform NO I/O: they never
// touch git, the clock, the environment, or the filesystem and NEVER throw — identical input
// yields a byte-identical `RepoSnapshot` (FR-009/FR-011, SC-002/SC-003). The actual git/CI
// sensing that fills `RawSensing` is the edge `Interpreter`'s job. Path normalization is
// single-sourced from `Config.Model.normalizePath` (research D7), so the emitted `GovernedPath`s
// are byte-identical to what `Routing.route` consumes (SC-001).

namespace FS.GG.Governance.Snapshot

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Snapshot =

    // ── Pure range planning (the pure half of D8) ──

    type RangeForm =
        | Since of GitRef
        | BaseHead of baseRef: GitRef * headRef: GitRef
        | Default

    type ResolutionPlan =
        { Form: RangeForm
          BaseRef: GitRef option
          HeadRef: GitRef option
          UseMergeBase: bool }

    // The documented default base/head (git-sensing.md §4): `HEAD`. The default range compares the
    // working position against `HEAD`, so the committed diff is empty and only uncommitted work is
    // surfaced via the working-tree sets. `route`/`ship` MAY override this default (their row).
    let private headRef = GitRef "HEAD"

    let planResolution (options: SnapshotOptions) : ResolutionPlan =
        // Precedence (git-sensing.md §4): `Since` wins; else explicit `Base`/`Head`; else default.
        // The committed diff is always against the merge base (three-dot), so `UseMergeBase=true`
        // for every form. A `None` `HeadRef` means "the current working position" (`HEAD`).
        match options.Since with
        | Some r ->
            { Form = Since r
              BaseRef = Some r
              HeadRef = None
              UseMergeBase = true }
        | None ->
            match options.Base, options.Head with
            | Some b, Some h ->
                { Form = BaseHead(b, h)
                  BaseRef = Some b
                  HeadRef = Some h
                  UseMergeBase = true }
            | Some b, None ->
                { Form = BaseHead(b, headRef)
                  BaseRef = Some b
                  HeadRef = Some headRef
                  UseMergeBase = true }
            | None, Some h ->
                { Form = BaseHead(headRef, h)
                  BaseRef = Some headRef
                  HeadRef = Some h
                  UseMergeBase = true }
            | None, None ->
                { Form = Default
                  BaseRef = Some headRef
                  HeadRef = None
                  UseMergeBase = true }

    // ── Raw sensing intermediate (filled by the edge, consumed here) ──

    type RawSensing =
        { RepoOk: bool
          BaseResolved: Result<CommitId, string>
          HeadResolved: Result<CommitId, string>
          MergeBaseResolved: Result<CommitId, string>
          DiffRaw: Result<string, string>
          StatusRaw: Result<string, string>
          BranchRaw: Result<string, string>
          RawCi: CiContext option
          Digests: CommandRunDigest list
          Plan: ResolutionPlan }

    // ── Stable operation tokens (mirror GitCommand.Token; used in digests + diagnostics) ──
    // Kept identical to the `GitCommand.Token` strings the edge emits so a diagnostic's
    // `Operation` matches the command's `CommandRunDigest.Command` (FR-008/FR-010).

    let private repoCheckOp = "repo-check"
    let private revParseOp = "rev-parse"
    let private mergeBaseOp = "merge-base"
    let private diffOp = "diff-name-status"
    let private statusOp = "status-porcelain"
    let private currentBranchOp = "current-branch"

    // ── Deterministic ordering helpers (FR-009, SC-002/SC-003) ──

    // Ordinal string comparison so ordering is culture-independent and byte-stable.
    let private byStringOrdinal (key: 'a -> string) =
        List.sortWith (fun a b -> System.String.CompareOrdinal(key a, key b))

    let private pathStr (GovernedPath p) = p

    let private sortPaths (ps: GovernedPath list) : GovernedPath list = ps |> byStringOrdinal pathStr

    let private sortChanged (cs: ChangedPath list) : ChangedPath list =
        cs |> byStringOrdinal (fun c -> pathStr c.Path)

    let private sortDigests (ds: CommandRunDigest list) : CommandRunDigest list =
        ds |> byStringOrdinal (fun d -> d.Command)

    let private sortDiagnostics (ds: SensingDiagnostic list) : SensingDiagnostic list =
        ds
        |> List.sortWith (fun a b ->
            let ka = sensingDiagnosticIdToken a.Id, a.Operation
            let kb = sensingDiagnosticIdToken b.Id, b.Operation
            let c = System.String.CompareOrdinal(fst ka, fst kb)
            if c <> 0 then c else System.String.CompareOrdinal(snd ka, snd kb))

    // ── Porcelain parsers (pure; literal-`-z`-fixture tested) ──

    let private nulFields (raw: string) : string list =
        // `-z` forms are NUL-delimited and UNQUOTED (git-sensing.md §1), so a plain split on NUL
        // recovers every field verbatim. Empty fields (e.g. from a trailing NUL) carry no record
        // and are dropped — neither a status letter nor a path is ever empty.
        raw.Split('\000') |> Array.filter (fun s -> s <> "") |> List.ofArray

    /// Parse `git diff --name-status -z -M` (git-sensing.md §2). Returns the changed paths plus any
    /// parse diagnostics (an unrecognized status letter ⇒ a single `UnparsableGitOutput`, never a
    /// silent drop). A rename/copy record spans three NUL fields (`<X>`, `<old>`, `<new>`).
    let private parseDiff (raw: string) : ChangedPath list * SensingDiagnostic list =
        let unparsable =
            { Id = UnparsableGitOutput
              Operation = "diff-name-status"
              Message = "git emitted a --name-status record this version cannot decode; re-run with a supported git" }

        // mutable: a tail-recursive fold would be equivalent; the explicit loop keeps the
        // three-field rename branch readable (Principle III disclosure — no shared mutable state).
        let rec loop (tokens: string list) (acc: ChangedPath list) =
            match tokens with
            | [] -> List.rev acc, []
            | status :: rest ->
                let letter = status.[0]

                let single kind =
                    match rest with
                    | path :: more ->
                        loop more ({ Path = normalizePath path; Kind = kind; OldPath = None } :: acc)
                    | [] -> List.rev acc, [ unparsable ]

                let pair kind =
                    match rest with
                    | oldPath :: newPath :: more ->
                        loop
                            more
                            ({ Path = normalizePath newPath
                               Kind = kind
                               OldPath = Some(normalizePath oldPath) }
                             :: acc)
                    | _ -> List.rev acc, [ unparsable ]

                match letter with
                | 'A' -> single Added
                | 'M' -> single Modified
                | 'D' -> single Deleted
                | 'T' -> single TypeChanged
                | 'R' -> pair Renamed
                | 'C' -> pair Copied
                | _ -> List.rev acc, [ unparsable ]

        loop (nulFields raw) []

    /// Parse `git status --porcelain=v1 -z` (git-sensing.md §3) into mutually-exclusive working-tree
    /// dirty/untracked sets. `??` ⇒ untracked; any other non-space status ⇒ dirty (the tracked-
    /// modified/staged path). An index rename/copy adds a trailing NUL field (the original path),
    /// which is consumed but not reported — the current (new) path is the dirty one.
    let private parseStatus (raw: string) : GovernedPath list * GovernedPath list =
        let rec loop (tokens: string list) (dirty: GovernedPath list) (untracked: GovernedPath list) =
            match tokens with
            | [] -> List.rev dirty, List.rev untracked
            | entry :: rest when entry.Length >= 3 ->
                let x = entry.[0]
                let code = entry.Substring(0, 2)
                let path = entry.Substring 3 // skip "XY "
                let normalized = normalizePath path
                // A rename/copy in the index carries a second NUL field (the original path).
                let rest' =
                    if x = 'R' || x = 'C' then
                        match rest with
                        | _orig :: more -> more
                        | [] -> []
                    else
                        rest

                if code = "??" then
                    loop rest' dirty (normalized :: untracked)
                else
                    loop rest' (normalized :: dirty) untracked
            | _ :: rest ->
                // A token too short to carry "XY <path>" is not a record we emit; skip it.
                loop rest dirty untracked

        loop (nulFields raw) [] []

    // ── Pure assembly (the heart of the feature) ──

    let private diag id operation message : SensingDiagnostic =
        { Id = id; Operation = operation; Message = message }

    let private emptyWorkingTree: WorkingTreeState = { Dirty = []; Untracked = [] }

    let assemble (raw: RawSensing) : RepoSnapshot =
        let digests = sortDigests raw.Digests

        if not raw.RepoOk then
            // Not a work tree (FR-008): a single stable diagnostic, Range = None, empty path sets —
            // structurally DISTINCT from an empty-but-successful snapshot (FR-011).
            { Range = None
              Changed = []
              WorkingTree = emptyWorkingTree
              Branch = None
              Ci = raw.RawCi
              Digests = digests
              Diagnostics =
                [ diag
                      NotARepository
                      repoCheckOp
                      "the target directory is not a git repository (or has no commits yet); run sensing inside a git work tree" ] }
        else

            // Range resolution: each ref/merge-base Error becomes a diagnostic and forces Range=None
            // (FR-008); all three Ok ⇒ Some range.
            let baseOk = match raw.BaseResolved with Ok _ -> true | Error _ -> false
            let headOk = match raw.HeadResolved with Ok _ -> true | Error _ -> false

            let rangeDiags =
                [ match raw.BaseResolved with
                  | Error msg -> diag UnknownRef revParseOp (sprintf "could not resolve the base ref: %s" msg)
                  | Ok _ -> ()
                  match raw.HeadResolved with
                  | Error msg -> diag UnknownRef revParseOp (sprintf "could not resolve the head ref: %s" msg)
                  | Ok _ -> ()
                  // A merge-base failure is only meaningful (and only reported) when both endpoints
                  // resolved — otherwise the base/head diagnostics above already explain the gap.
                  match raw.MergeBaseResolved with
                  | Error msg when baseOk && headOk ->
                      diag GitCommandFailed mergeBaseOp (sprintf "could not compute the merge base: %s" msg)
                  | _ -> () ]

            let range =
                match raw.BaseResolved, raw.HeadResolved, raw.MergeBaseResolved with
                | Ok b, Ok h, Ok mb -> Some { Base = b; Head = h; MergeBase = mb }
                | _ -> None

            // Committed diff: parsed only when the range resolved (the range diagnostics already
            // explain a failed range; a genuine diff-command failure under a good range is its own
            // GitCommandFailed). An empty diff under a good range is the "nothing changed" outcome.
            let changed, diffDiags =
                match range, raw.DiffRaw with
                | Some _, Ok text -> parseDiff text
                | Some _, Error msg ->
                    [], [ diag GitCommandFailed diffOp (sprintf "the committed diff could not be read: %s" msg) ]
                | None, _ -> [], []

            // Working tree: read independently of the range (FR-003). A read failure is
            // UnreadableWorkingTree.
            let workingTree, statusDiags =
                match raw.StatusRaw with
                | Ok text ->
                    let dirty, untracked = parseStatus text
                    { Dirty = sortPaths dirty; Untracked = sortPaths untracked }, []
                | Error msg ->
                    emptyWorkingTree,
                    [ diag UnreadableWorkingTree statusOp (sprintf "the working tree could not be read: %s" msg) ]

            // Branch: `"HEAD"` ⇒ detached (None, never fabricated, FR-005); any other ⇒ Some.
            let branch, branchDiags =
                match raw.BranchRaw with
                | Ok name ->
                    let trimmed = name.Trim()
                    (if trimmed = "HEAD" || trimmed = "" then None else Some(BranchName trimmed)), []
                | Error msg ->
                    None, [ diag GitCommandFailed currentBranchOp (sprintf "the current branch could not be read: %s" msg) ]

            { Range = range
              Changed = sortChanged changed
              WorkingTree = workingTree
              Branch = branch
              Ci = raw.RawCi
              Digests = digests
              Diagnostics = sortDiagnostics (rangeDiags @ diffDiags @ statusDiags @ branchDiags) }
