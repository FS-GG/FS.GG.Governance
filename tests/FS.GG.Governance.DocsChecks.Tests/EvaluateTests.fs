module FS.GG.Governance.DocsChecks.Tests.EvaluateTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.DocsChecks
open FS.GG.Governance.DocsChecks.Model
open FS.GG.Governance.DocsChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private req = requestFor "docs" "docs/guide.md" (Some "docs-evidence")
let private src = normalizePath "docs/guide.md"

let private empty =
    { Sources = [ src ]
      Links = []
      References = []
      Examples = []
      Unreadable = [] }

[<Tests>]
let tests =
    testList
        "DocsChecks.evaluate"
        [ test "all resolve ⇒ zero findings (zero false positives)" {
              let facts =
                  { empty with
                      Links = [ { Source = src; LinkText = "Guide"; Target = "docs/other.md"; Outcome = LinkResolves } ]
                      References = [ { Source = src; Reference = "Sym"; Outcome = ReferenceResolves } ] }

              Expect.isEmpty (DocsChecks.evaluate req facts) "clean docs yield no findings"
          }

          test "dangling link ⇒ Blocking docs.link-currency naming file + link + target" {
              let facts =
                  { empty with
                      Links =
                          [ { Source = src
                              LinkText = "Broken"
                              Target = "docs/missing.md"
                              Outcome = LinkDangling "docs/missing.md" } ] }

              let f = List.head (DocsChecks.evaluate req facts)
              Expect.equal f.Code "docs.link-currency" "code"
              Expect.equal f.BaseSeverity Blocking "Blocking"
              Expect.equal f.Location.Detail "Broken" "detail is the link text"
              Expect.stringContains f.Message "docs/missing.md" "names the target"
              Expect.equal f.EvidenceTag (Some(EvidenceTag "docs-evidence")) "carries the tag"
          }

          test "stale reference ⇒ Blocking docs.reference-currency naming the symbol" {
              let facts =
                  { empty with
                      References = [ { Source = src; Reference = "OldSym"; Outcome = ReferenceStale "OldSym" } ] }

              let f = List.head (DocsChecks.evaluate req facts)
              Expect.equal f.Code "docs.reference-currency" "code"
              Expect.stringContains f.Message "OldSym" "names the stale symbol"
          }

          test "unreadable source ⇒ IsInputState docs.source-unreadable" {
              let facts = { empty with Unreadable = [ "docs/guide.md: denied" ] }
              let f = List.head (DocsChecks.evaluate req facts)
              Expect.equal f.Code "docs.source-unreadable" "code"
              Expect.isTrue f.IsInputState "input state"
          }

          test "no declared tag ⇒ EvidenceTag is None" {
              let req2 = requestFor "docs" "docs/guide.md" None

              let facts =
                  { empty with
                      Links = [ { Source = src; LinkText = "x"; Target = "y"; Outcome = LinkDangling "y" } ] }

              let f = List.head (DocsChecks.evaluate req2 facts)
              Expect.equal f.EvidenceTag None "None when no tag declared"
          } ]
