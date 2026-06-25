module FS.GG.Governance.DesignChecks.Tests.EvaluateTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.DesignChecks
open FS.GG.Governance.DesignChecks.Model
open FS.GG.Governance.DesignChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private req = requestFor "design" "design/surface.txt" (Some "design-evidence")

let private empty =
    { Tokens = []
      Captures = []
      Controls = []
      Contrasts = []
      CatalogUnavailable = [] }

[<Tests>]
let tests =
    testList
        "DesignChecks.evaluate"
        [ test "all resolve + contrast meets ⇒ zero findings" {
              let facts =
                  { empty with
                      Tokens = [ { Token = "color.primary"; Outcome = Resolves } ]
                      Contrasts = [ { Pair = "fg-bg"; Ratio = 4.6m; Threshold = 4.5m; Meets = true } ] }

              Expect.isEmpty (DesignChecks.evaluate req facts) "all resolve ⇒ clean"
          }

          test "absent token/capture/control ⇒ Blocking design.<kind> naming the entry" {
              let facts =
                  { empty with
                      Tokens = [ { Token = "color.missing"; Outcome = Absent "color.missing" } ]
                      Captures = [ { Capture = "gone.png"; Outcome = Absent "gone.png" } ]
                      Controls = [ { Control = "Ghost"; Outcome = Absent "Ghost" } ] }

              let findings = DesignChecks.evaluate req facts
              let codes = findings |> List.map (fun f -> f.Code) |> List.sort
              Expect.equal codes [ "design.capture"; "design.control"; "design.token" ] "one per kind"
              Expect.all findings (fun f -> f.BaseSeverity = Blocking) "all Blocking"
              Expect.all findings (fun f -> f.EvidenceTag = Some(EvidenceTag "design-evidence")) "carry the tag"
          }

          test "sub-threshold contrast ⇒ Blocking design.contrast reporting ratio vs threshold" {
              let facts =
                  { empty with
                      Contrasts = [ { Pair = "fg-bg"; Ratio = 2.0m; Threshold = 4.5m; Meets = false } ] }

              let f = List.head (DesignChecks.evaluate req facts)
              Expect.equal f.Code "design.contrast" "code"
              Expect.stringContains f.Message "2.0" "reports the ratio"
              Expect.stringContains f.Message "4.5" "reports the threshold"
          }

          test "catalog unavailable ⇒ IsInputState design.catalog-unavailable naming the catalog" {
              let facts = { empty with CatalogUnavailable = [ "token catalog: not found" ] }
              let f = List.head (DesignChecks.evaluate req facts)
              Expect.equal f.Code "design.catalog-unavailable" "code"
              Expect.isTrue f.IsInputState "input state"
          } ]
