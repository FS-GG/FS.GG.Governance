module FS.GG.Governance.Snapshot.Tests.ResolutionTests

open Expecto
open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Model

// US3 (FR-004): planResolution is PURE and TOTAL — every option form maps to the documented
// ResolutionPlan (git-sensing.md §4) with no git involved, so the option-form contract is
// unit-testable without a repository. Identical options ⇒ identical plan (local/CI parity).

let private opts since baseRef headRef : SnapshotOptions =
    { Since = since; Base = baseRef; Head = headRef }

let private head = GitRef "HEAD"

[<Tests>]
let tests =
    testList
        "Resolution"
        [ test "Since wins over base/head and heads at the working position" {
              let plan = Snapshot.planResolution (opts (Some(GitRef "v1")) (Some(GitRef "b")) (Some(GitRef "h")))
              Expect.equal plan.Form (Snapshot.Since(GitRef "v1")) "Since form"
              Expect.equal plan.BaseRef (Some(GitRef "v1")) "base = since rev"
              Expect.equal plan.HeadRef None "head = current working position"
              Expect.isTrue plan.UseMergeBase "three-dot merge base"
          }

          test "explicit base + head" {
              let plan = Snapshot.planResolution (opts None (Some(GitRef "main")) (Some(GitRef "feature")))
              Expect.equal plan.Form (Snapshot.BaseHead(GitRef "main", GitRef "feature")) "BaseHead form"
              Expect.equal plan.BaseRef (Some(GitRef "main")) "base"
              Expect.equal plan.HeadRef (Some(GitRef "feature")) "head"
              Expect.isTrue plan.UseMergeBase "three-dot merge base"
          }

          test "base only ⇒ head defaults to HEAD" {
              let plan = Snapshot.planResolution (opts None (Some(GitRef "main")) None)
              Expect.equal plan.Form (Snapshot.BaseHead(GitRef "main", head)) "BaseHead with HEAD head"
              Expect.equal plan.BaseRef (Some(GitRef "main")) "base"
              Expect.equal plan.HeadRef (Some head) "head = HEAD"
          }

          test "head only ⇒ base defaults to HEAD" {
              let plan = Snapshot.planResolution (opts None None (Some(GitRef "feature")))
              Expect.equal plan.Form (Snapshot.BaseHead(head, GitRef "feature")) "BaseHead with HEAD base"
              Expect.equal plan.BaseRef (Some head) "base = default HEAD"
              Expect.equal plan.HeadRef (Some(GitRef "feature")) "head"
          }

          test "no options ⇒ Default (HEAD vs working position)" {
              let plan = Snapshot.planResolution (opts None None None)
              Expect.equal plan.Form Snapshot.Default "Default form"
              Expect.equal plan.BaseRef (Some head) "base = documented default HEAD"
              Expect.equal plan.HeadRef None "head = current working position"
              Expect.isTrue plan.UseMergeBase "three-dot merge base"
          }

          test "identical options resolve to an identical plan (local/CI parity, SC-004)" {
              let o = opts None (Some(GitRef "main")) (Some(GitRef "feature"))
              Expect.equal (Snapshot.planResolution o) (Snapshot.planResolution o) "pure ⇒ identical"
          } ]
