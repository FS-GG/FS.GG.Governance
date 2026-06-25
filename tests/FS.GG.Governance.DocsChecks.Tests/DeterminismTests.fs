module FS.GG.Governance.DocsChecks.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.DocsChecks
open FS.GG.Governance.DocsChecks.Model
open FS.GG.Governance.DocsChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private req = requestFor "docs" "docs/guide.md" None
let private src = normalizePath "docs/guide.md"

let private link t = { Source = src; LinkText = t; Target = t; Outcome = LinkDangling t }
let private ref r = { Source = src; Reference = r; Outcome = ReferenceStale r }

[<Tests>]
let tests =
    testList
        "DocsChecks.determinism"
        [ test "repeated evaluate over identical facts ⇒ byte-identical findings" {
              let facts =
                  { Sources = [ src ]
                    Links = [ link "b"; link "a" ]
                    References = [ ref "z" ]
                    Examples = []
                    Unreadable = [] }

              Expect.equal (DocsChecks.evaluate req facts) (DocsChecks.evaluate req facts) "deterministic"
          }

          test "reordering Links/References leaves the sorted findings unchanged" {
              let mk links refs =
                  DocsChecks.evaluate
                      req
                      { Sources = [ src ]
                        Links = links
                        References = refs
                        Examples = []
                        Unreadable = [] }

              Expect.equal (mk [ link "a"; link "b" ] [ ref "z" ]) (mk [ link "b"; link "a" ] [ ref "z" ]) "order-independent"
          } ]
