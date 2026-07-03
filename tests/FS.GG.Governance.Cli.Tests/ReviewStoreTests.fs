module FS.GG.Governance.Cli.Tests.ReviewStoreTests

open System.IO
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Cli

let private requestWithStore dir =
    match Cli.parse [ "route" ] with
    | Ok req -> { req with ReviewStore = Some dir }
    | Error e -> failtestf "base parse failed: %A" e

let private tempStore () =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-reviewstore-tests", Path.GetRandomFileName())
    Directory.CreateDirectory dir |> ignore
    dir

[<Tests>]
let tests =
    testList
        "ReviewStore"
        [ test "round-trips a verdict for its own key (#55 M-CLI-2)" {
              let req = requestWithStore (tempStore ())
              let review: RecordedReview = { Rule = RuleId "rule.one"; Key = "rule:a/b"; Verdict = Pass }
              Expect.equal (ReviewStore.saveReview req review) (Ok()) "save"
              Expect.equal (ReviewStore.loadReview req "rule:a/b") (Ok(Some review)) "load own key"
          }

          test "distinct keys that sanitize identically do NOT collide (#55 M-CLI-2)" {
              // "rule:a/b" and "rule:a b" both sanitize to "rule_a_b"; before the hash suffix they
              // shared one file, so the second save silently overwrote the first's verdict.
              let req = requestWithStore (tempStore ())
              let k1 = "rule:a/b"
              let k2 = "rule:a b"
              let r1: RecordedReview = { Rule = RuleId "rule.one"; Key = k1; Verdict = Fail "blocked-by-k1" }
              let r2: RecordedReview = { Rule = RuleId "rule.one"; Key = k2; Verdict = Pass }
              ReviewStore.saveReview req r1 |> ignore
              ReviewStore.saveReview req r2 |> ignore
              Expect.equal (ReviewStore.loadReview req k1) (Ok(Some r1)) "k1 keeps its own verdict"
              Expect.equal (ReviewStore.loadReview req k2) (Ok(Some r2)) "k2 keeps its own verdict"
          }

          test "a store path containing the retired fixture token is not a backdoor failure (#55 M-CLI-2)" {
              // The old backdoor hard-failed any repo whose path contained this token. It is gone;
              // a store rooted under such a path now behaves like any other.
              let dir = Path.Combine(tempStore (), "review-store-unavailable")
              Directory.CreateDirectory dir |> ignore
              let req = requestWithStore dir
              Expect.equal (ReviewStore.loadReview req "any-key") (Ok None) "no fixture failure on load (miss)"
              let review: RecordedReview = { Rule = RuleId "r"; Key = "any-key"; Verdict = Pass }
              Expect.equal (ReviewStore.saveReview req review) (Ok()) "no fixture failure on save"
          } ]
