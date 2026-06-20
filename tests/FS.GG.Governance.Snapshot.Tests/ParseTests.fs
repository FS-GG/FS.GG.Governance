module FS.GG.Governance.Snapshot.Tests.ParseTests

open Expecto
open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Snapshot.Tests.Support

// US1/US2: porcelain parsing is exercised through the PUBLIC `assemble` (the parsers themselves are
// private — Principle II). Fixtures are the ACTUAL NUL-delimited `-z` wire bytes git emits
// (verified against real git in Support), ugly on purpose.

let private changedOf (diffRaw: string) =
    (Snapshot.assemble { baseRaw with DiffRaw = Ok diffRaw }).Changed

let private wtOf (statusRaw: string) =
    (Snapshot.assemble { baseRaw with StatusRaw = Ok statusRaw }).WorkingTree

let private findPath p (cs: ChangedPath list) = cs |> List.find (fun c -> c.Path = gp p)

[<Tests>]
let tests =
    testList
        "Parse"
        [ // ── diff --name-status -z -M ──

          test "single A/M/D/T records map to the right kind with no OldPath" {
              let cs =
                  changedOf (znul [ "A"; "a.fs" ] + znul [ "M"; "m.fs" ] + znul [ "D"; "d.fs" ] + znul [ "T"; "t.fs" ])

              Expect.equal (findPath "a.fs" cs).Kind Added "A → Added"
              Expect.equal (findPath "m.fs" cs).Kind Modified "M → Modified"
              Expect.equal (findPath "d.fs" cs).Kind Deleted "D → Deleted (the deleted path)"
              Expect.equal (findPath "t.fs" cs).Kind TypeChanged "T → TypeChanged"
              Expect.isTrue (cs |> List.forall (fun c -> c.OldPath = None)) "no OldPath on non-rename records"
          }

          test "rename/copy three-NUL records carry Path=new, OldPath=Some old" {
              let cs = changedOf (znul [ "R096"; "old.fs"; "new.fs" ] + znul [ "C100"; "src.fs"; "copy.fs" ])
              let r = findPath "new.fs" cs
              Expect.equal r.Kind Renamed "R → Renamed"
              Expect.equal r.OldPath (Some(gp "old.fs")) "rename destination is Path, source is OldPath"
              let c = findPath "copy.fs" cs
              Expect.equal c.Kind Copied "C → Copied"
              Expect.equal c.OldPath (Some(gp "src.fs")) "copy source is OldPath"
          }

          test "an unknown status letter yields a single UnparsableGitOutput (not a silent drop)" {
              let snap = Snapshot.assemble { baseRaw with DiffRaw = Ok(znul [ "Z"; "weird.fs" ]) }
              let ids = snap.Diagnostics |> List.map (fun d -> d.Id)
              Expect.equal ids [ UnparsableGitOutput ] "exactly one UnparsableGitOutput diagnostic"
              Expect.equal snap.Diagnostics.[0].Operation "diff-name-status" "operation token names the failed read"
          }

          test "a non-ASCII / spaced path survives -z verbatim (FR-012)" {
              let cs = changedOf (znul [ "A"; "docs/föö bar.md" ])
              Expect.equal (cs |> List.map (fun c -> c.Path)) [ gp "docs/föö bar.md" ] "path preserved, not git-quoted"
          }

          // ── status --porcelain=v1 -z ──

          test "?? is untracked; every other non-space status is dirty" {
              let wt =
                  wtOf (
                      znul [ "M  staged.fs" ]
                      + znul [ " M worktree.fs" ]
                      + znul [ "MM both.fs" ]
                      + znul [ "A  added.fs" ]
                      + znul [ " D deleted.fs" ]
                      + znul [ "?? untracked.fs" ]
                  )

              Expect.equal
                  wt.Dirty
                  [ gp "added.fs"; gp "both.fs"; gp "deleted.fs"; gp "staged.fs"; gp "worktree.fs" ]
                  "all non-?? statuses → dirty, sorted"

              Expect.equal wt.Untracked [ gp "untracked.fs" ] "?? → untracked"
          }

          test "an index rename's current (new) path is the dirty path; the old path is consumed" {
              // Real git -z emits: 'R  <new>' NUL '<old>' NUL (verified in Support).
              let wt = wtOf (znul [ "R  renamed-new.fs"; "renamed-old.fs" ])
              Expect.equal wt.Dirty [ gp "renamed-new.fs" ] "current path is dirty"
              Expect.equal wt.Untracked [] "the old-path field is consumed, not emitted"
          }

          test "Dirty and Untracked are mutually exclusive within the working-tree plane (FR-003)" {
              let wt = wtOf (znul [ "M  a.fs" ] + znul [ "?? b.fs" ] + znul [ " M c.fs" ])
              let overlap = Set.intersect (Set.ofList wt.Dirty) (Set.ofList wt.Untracked)
              Expect.isEmpty overlap "no path is both dirty and untracked"
          } ]
