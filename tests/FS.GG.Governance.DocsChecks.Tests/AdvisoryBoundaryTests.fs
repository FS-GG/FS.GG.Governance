module FS.GG.Governance.DocsChecks.Tests.AdvisoryBoundaryTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.DocsChecks
open FS.GG.Governance.DocsChecks.Model
open FS.GG.Governance.DocsChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

// T050 (US5): the docs example-freshness advisory produced in DocsChecks.evaluate is labelled
// BaseSeverity = Advisory and is distinguishable from a deterministic (Blocking) finding.
let private req = requestFor "docs" "docs/guide.md" None
let private src = normalizePath "docs/guide.md"

[<Tests>]
let tests =
    testList
        "DocsChecks.advisoryBoundary"
        [ test "judgement-heavy example staleness ⇒ Advisory docs.example-freshness (never Blocking)" {
              let facts =
                  { Sources = [ src ]
                    Links = []
                    References = []
                    Examples = [ { Source = src; Example = "snippet-1"; Outcome = ExampleStale "signature may differ" } ]
                    Unreadable = [] }

              let f = List.head (DocsChecks.evaluate req facts)
              Expect.equal f.Code "docs.example-freshness" "code"
              Expect.equal f.BaseSeverity Advisory "judgement-heavy ⇒ Advisory"
          }

          test "current example ⇒ no finding; advisory is distinguishable from a Blocking link finding" {
              let facts =
                  { Sources = [ src ]
                    Links = [ { Source = src; LinkText = "x"; Target = "y"; Outcome = LinkDangling "y" } ]
                    References = []
                    Examples = [ { Source = src; Example = "ok"; Outcome = ExampleCurrent } ]
                    Unreadable = [] }

              let findings = DocsChecks.evaluate req facts
              Expect.hasLength findings 1 "only the link finding"
              Expect.equal (List.head findings).BaseSeverity Blocking "the deterministic link finding is Blocking"
          } ]
