module FS.GG.Governance.DesignChecks.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.DesignChecks
open FS.GG.Governance.DesignChecks.Model
open FS.GG.Governance.DesignChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private req = requestFor "design" "design/surface.txt" None

let private mk tokens =
    { Tokens = tokens
      Captures = []
      Controls = []
      Contrasts = []
      CatalogUnavailable = [] }

[<Tests>]
let tests =
    testList
        "DesignChecks.determinism"
        [ test "repeated evaluate over identical facts ⇒ byte-identical" {
              let f = mk [ { Token = "b"; Outcome = Absent "b" }; { Token = "a"; Outcome = Absent "a" } ]
              Expect.equal (DesignChecks.evaluate req f) (DesignChecks.evaluate req f) "deterministic"
          }

          test "reordering tokens leaves the sorted findings unchanged" {
              let a = { Token = "a"; Outcome = Absent "a" }
              let b = { Token = "b"; Outcome = Absent "b" }
              Expect.equal (DesignChecks.evaluate req (mk [ a; b ])) (DesignChecks.evaluate req (mk [ b; a ])) "order-independent"
          } ]
