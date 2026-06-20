module FS.GG.Governance.Snapshot.Tests.AssembleTests

open Expecto
open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Snapshot.Tests.Support

// US1/US2/US4/US5 (pure): `assemble` parses, normalizes, categorizes, orders, and diagnoses a
// hand-built RawSensing into a RepoSnapshot. PURE and TOTAL — never touches git, never throws.

[<Tests>]
let tests =
    testList
        "Assemble"
        [ // ── US1: committed changed-path set ──

          test "a two-file diff yields both as normalized GovernedPaths sorted by Path, with Some Range" {
              let snap =
                  Snapshot.assemble
                      { baseRaw with DiffRaw = Ok(znul [ "M"; "./src/Kernel/Eval.fs" ] + znul [ "A"; "docs/intro.md" ]) }

              Expect.equal
                  (snap.Changed |> List.map (fun c -> c.Path))
                  [ gp "docs/intro.md"; gp "src/Kernel/Eval.fs" ]
                  "normalized (leading ./ stripped) and sorted by Path"

              Expect.isSome snap.Range "range resolved"
          }

          test "an empty diff under a resolved range is the genuine 'nothing changed' success (FR-011)" {
              let snap = Snapshot.assemble { baseRaw with DiffRaw = Ok "" }
              Expect.isEmpty snap.Changed "no changed paths"
              Expect.isEmpty snap.Diagnostics "no diagnostics — distinct from a failure"
              Expect.isSome snap.Range "range still resolved"
          }

          // ── US2: working-tree planes ──

          test "dirty + untracked are normalized, sorted, and mutually exclusive" {
              let snap =
                  Snapshot.assemble
                      { baseRaw with
                          StatusRaw = Ok(znul [ "M  ./src/b.fs" ] + znul [ "?? src/a.fs" ] + znul [ " M src/c.fs" ]) }

              Expect.equal snap.WorkingTree.Dirty [ gp "src/b.fs"; gp "src/c.fs" ] "dirty normalized + sorted"
              Expect.equal snap.WorkingTree.Untracked [ gp "src/a.fs" ] "untracked"

              let overlap =
                  Set.intersect (Set.ofList snap.WorkingTree.Dirty) (Set.ofList snap.WorkingTree.Untracked)

              Expect.isEmpty overlap "mutually exclusive within the working-tree plane"
          }

          test "a path committed-changed AND dirty appears in BOTH planes (distinct, not cross-exclusive)" {
              let snap =
                  Snapshot.assemble
                      { baseRaw with
                          DiffRaw = Ok(znul [ "M"; "src/shared.fs" ])
                          StatusRaw = Ok(znul [ "M  src/shared.fs" ]) }

              Expect.equal (snap.Changed |> List.map (fun c -> c.Path)) [ gp "src/shared.fs" ] "in the committed plane"
              Expect.equal snap.WorkingTree.Dirty [ gp "src/shared.fs" ] "AND in the working-tree dirty plane"
          }

          // ── US4: branch + optional CI context ──

          test "BranchRaw 'HEAD' ⇒ detached (Branch=None, never fabricated)" {
              let snap = Snapshot.assemble { baseRaw with BranchRaw = Ok "HEAD" }
              Expect.equal snap.Branch None "detached HEAD ⇒ no branch"
          }

          test "BranchRaw of a name ⇒ Some BranchName" {
              let snap = Snapshot.assemble { baseRaw with BranchRaw = Ok "feature/x\n" }
              Expect.equal snap.Branch (Some(BranchName "feature/x")) "branch name captured (trimmed)"
          }

          test "a supplied CiContext is captured; absence is explicit None" {
              let ci =
                  { Environment = CiEnvironment.Ci
                    PrLabels = [ "needs-review"; "size/s" ]
                    RequiredStatusChecks = [ "build"; "test" ] }

              let withCi = Snapshot.assemble { baseRaw with RawCi = Some ci }
              Expect.equal withCi.Ci (Some ci) "CI context passed through faithfully"

              let withoutCi = Snapshot.assemble { baseRaw with RawCi = None }
              Expect.equal withoutCi.Ci None "absent ⇒ None, not empty-as-present"
          }

          // ── US5: every diagnostic id reachable, with operation token + fix hint ──

          test "each SensingDiagnosticId is reachable from its matching RawSensing error shape" {
              let only id (snap: RepoSnapshot) =
                  Expect.equal (snap.Diagnostics |> List.map (fun d -> d.Id)) [ id ] (sprintf "exactly %A" id)
                  let d = snap.Diagnostics.[0]
                  Expect.isNotEmpty d.Operation "carries an operation token"
                  Expect.isNotEmpty d.Message "carries a fix-hint message"

              only NotARepository (Snapshot.assemble { baseRaw with RepoOk = false })
              only UnknownRef (Snapshot.assemble { baseRaw with BaseResolved = Error "no such ref" })
              only GitCommandFailed (Snapshot.assemble { baseRaw with MergeBaseResolved = Error "no merge base" })
              only UnreadableWorkingTree (Snapshot.assemble { baseRaw with StatusRaw = Error "permission denied" })
              only UnparsableGitOutput (Snapshot.assemble { baseRaw with DiffRaw = Ok(znul [ "Q"; "x.fs" ]) })
          }

          test "diagnostics are sorted by (id token, operation) and non-empty exactly on failure" {
              let snap =
                  Snapshot.assemble
                      { baseRaw with
                          BaseResolved = Error "bad base"
                          StatusRaw = Error "bad tree" }

              let sorted =
                  snap.Diagnostics
                  |> List.sortWith (fun a b ->
                      let c = System.String.CompareOrdinal(sensingDiagnosticIdToken a.Id, sensingDiagnosticIdToken b.Id)
                      if c <> 0 then c else System.String.CompareOrdinal(a.Operation, b.Operation))

              Expect.equal snap.Diagnostics sorted "diagnostics already in (id, operation) order"
              Expect.isNonEmpty snap.Diagnostics "failure ⇒ non-empty diagnostics"
              Expect.equal snap.Range None "a range-resolution failure forces Range = None"
          }

          test "empty-success and failure are structurally distinct (FR-011)" {
              let success = Snapshot.assemble { baseRaw with DiffRaw = Ok "" }
              let failure = Snapshot.assemble { baseRaw with RepoOk = false }
              Expect.isTrue (success.Diagnostics.IsEmpty && success.Range.IsSome) "success: no diagnostics, Some range"
              Expect.isTrue (not failure.Diagnostics.IsEmpty && failure.Range.IsNone) "failure: diagnostics, no range"
          } ]
