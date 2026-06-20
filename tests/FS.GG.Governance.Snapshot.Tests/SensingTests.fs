module FS.GG.Governance.Snapshot.Tests.SensingTests

open Expecto
open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Snapshot.Tests.Support

// US1/US2/US3/US4/US5 (real evidence, Principle V): the edge `senseSnapshot` runs the REAL `git`
// against a disposable temp repo. No fake git, no network. Path facts come from real diff/status.

let private opts since baseRef headRef : SnapshotOptions =
    { Since = since; Base = baseRef; Head = headRef }

let private defaultOpts = opts None None None

/// Real git ports for a repo dir, with an injected CiPort (so no network is ever reached, SC-007).
let private portsWithCi dir ci : Ports =
    { Git = (Interpreter.realPorts dir).Git; Ci = fun () -> ci }

let private changedPaths (snap: RepoSnapshot) = snap.Changed |> List.map (fun c -> c.Path)

[<Tests>]
let tests =
    testList
        "Sensing"
        [ // ── US1: committed changed-path set over real git ──

          test "senses the committed changed-path set between base and head (US1 AS1/AS3)" {
              withTempRepo (fun dir ->
                  writeFile dir "src/Eval.fs" "v1\n"
                  writeFile dir "docs/intro.md" "v1\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "base" ] |> ignore
                  writeFile dir "src/Eval.fs" "v2\n"
                  writeFile dir "docs/intro.md" "v2\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "head" ] |> ignore

                  let ports = Interpreter.realPorts dir
                  let snap = Interpreter.senseSnapshot ports (opts None (Some(GitRef "HEAD~1")) (Some(GitRef "HEAD")))

                  Expect.equal (changedPaths snap) [ gp "docs/intro.md"; gp "src/Eval.fs" ] "two normalized paths, sorted"
                  Expect.isEmpty snap.Diagnostics "successful sense, no diagnostics"

                  // SC-002: the same state sensed twice is byte-identical.
                  let again = Interpreter.senseSnapshot ports (opts None (Some(GitRef "HEAD~1")) (Some(GitRef "HEAD")))
                  Expect.equal snap again "identical repository state ⇒ identical snapshot")
          }

          // ── US2: working-tree dirty/untracked over real git ──

          test "senses dirty + untracked working-tree state, distinct from committed Changed (US2)" {
              withTempRepo (fun dir ->
                  writeFile dir "tracked.fs" "v1\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "base" ] |> ignore
                  // Uncommitted: modify the tracked file, add an untracked one.
                  writeFile dir "tracked.fs" "v2\n"
                  writeFile dir "fresh.fs" "new\n"

                  let snap = Interpreter.senseSnapshot (Interpreter.realPorts dir) defaultOpts

                  Expect.equal snap.WorkingTree.Dirty [ gp "tracked.fs" ] "modified tracked file is dirty"
                  Expect.equal snap.WorkingTree.Untracked [ gp "fresh.fs" ] "new file is untracked"
                  Expect.isEmpty (changedPaths snap) "Default range ⇒ empty committed Changed (uncommitted work only)")
          }

          test "a clean working tree senses empty dirty/untracked and stays successful (US2 AS3)" {
              withTempRepo (fun dir ->
                  writeFile dir "a.fs" "v1\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "base" ] |> ignore

                  let snap = Interpreter.senseSnapshot (Interpreter.realPorts dir) defaultOpts
                  Expect.isEmpty snap.WorkingTree.Dirty "clean ⇒ no dirty"
                  Expect.isEmpty snap.WorkingTree.Untracked "clean ⇒ no untracked"
                  Expect.isEmpty snap.Diagnostics "clean ⇒ successful, no diagnostics"
                  Expect.isSome snap.Range "range resolved")
          }

          // ── US3: local/CI parity over the same commits (SC-004) ──

          test "the same options resolve identically under local-shaped and CI-shaped contexts (SC-004)" {
              withTempRepo (fun dir ->
                  writeFile dir "x.fs" "v1\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "base" ] |> ignore
                  writeFile dir "x.fs" "v2\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "head" ] |> ignore

                  let o = opts None (Some(GitRef "HEAD~1")) (Some(GitRef "HEAD"))
                  let ciCtx = { Environment = CiEnvironment.Ci; PrLabels = [ "p" ]; RequiredStatusChecks = [ "c" ] }
                  let local = Interpreter.senseSnapshot (portsWithCi dir None) o
                  let ci = Interpreter.senseSnapshot (portsWithCi dir (Some ciCtx)) o

                  Expect.equal local.Range ci.Range "same resolved range in both contexts"
                  Expect.equal (changedPaths local) (changedPaths ci) "same changed paths in both contexts"
                  Expect.equal ci.Ci (Some ciCtx) "the CI-shaped context carried its optional fields"
                  Expect.equal local.Ci None "the local-shaped context has no CI fields")
          }

          // ── US4: branch + optional CI context (real git for the branch) ──

          test "captures the branch from git; detached HEAD ⇒ Branch=None (US4)" {
              withTempRepo (fun dir ->
                  writeFile dir "a.fs" "v1\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "base" ] |> ignore

                  let onBranch = Interpreter.senseSnapshot (Interpreter.realPorts dir) defaultOpts
                  Expect.equal onBranch.Branch (Some(BranchName "main")) "branch captured from git"

                  git dir [ "checkout"; "--detach"; "-q" ] |> ignore
                  let detached = Interpreter.senseSnapshot (Interpreter.realPorts dir) defaultOpts
                  Expect.equal detached.Branch None "detached HEAD ⇒ no fabricated branch")
          }

          // ── US5: fail safe + read-only over real git ──

          test "a non-repository directory ⇒ NotARepository, no throw, not an empty success (US5 AS1)" {
              withTempDir (fun dir ->
                  let snap = Interpreter.senseSnapshot (Interpreter.realPorts dir) defaultOpts
                  Expect.equal (snap.Diagnostics |> List.map (fun d -> d.Id)) [ NotARepository ] "NotARepository diagnostic"
                  Expect.equal snap.Range None "no range — distinct from an empty-but-successful snapshot")
          }

          test "an unknown base ref ⇒ UnknownRef, distinct from an empty diff (US5 AS2)" {
              withTempRepo (fun dir ->
                  writeFile dir "a.fs" "v1\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "base" ] |> ignore

                  let snap =
                      Interpreter.senseSnapshot (Interpreter.realPorts dir) (opts None (Some(GitRef "no-such-ref")) (Some(GitRef "HEAD")))

                  Expect.contains (snap.Diagnostics |> List.map (fun d -> d.Id)) UnknownRef "UnknownRef diagnostic raised"
                  Expect.equal snap.Range None "no range on an unresolved ref")
          }

          test "sensing is byte-for-byte read-only (US5 AS3, SC-005)" {
              withTempRepo (fun dir ->
                  writeFile dir "a.fs" "v1\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "base" ] |> ignore
                  // Add committed history + dirty + untracked so sensing touches diff and status.
                  writeFile dir "a.fs" "v2\n"
                  git dir [ "add"; "-A" ] |> ignore
                  git dir [ "commit"; "-qm"; "second" ] |> ignore
                  writeFile dir "a.fs" "v3-dirty\n"
                  writeFile dir "untracked.fs" "u\n"

                  let before = repoFingerprint dir
                  Interpreter.senseSnapshot (Interpreter.realPorts dir) (opts None (Some(GitRef "HEAD~1")) (Some(GitRef "HEAD")))
                  |> ignore
                  let after = repoFingerprint dir
                  Expect.equal after before "the repository is byte-identical before and after sensing")
          } ]
