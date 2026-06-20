module FS.GG.Governance.Gates.Tests.GateBuildTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Tests.Support

// US1 (SC-001): each declared check becomes exactly one gate carrying a stable `GateId`
// (`domain:checkId`) and the declared domain/cost/owner/maturity verbatim, a non-empty
// description, a present timeout, and a `RequiresCommand` prerequisite iff the check declares a
// command. Plus determinism-of-ids (AS2) and the empty case (AS3). All over real `factsOf`.

let private byId (id: string) (reg: GateRegistry) =
    reg.Gates |> List.find (fun g -> gateIdValue g.Id = id)

[<Tests>]
let tests =
    testList
        "GateBuild"
        [ test "N declared checks → exactly N gates, one per check (SC-001)" {
              let checks =
                  [ check "build" "tests" None "team-a" Medium Ci Warn
                    check "security" "scan" None "team-b" High Ci BlockOnPr
                    check "docs" "lint" None "team-c" Cheap Local Observe ]

              let reg = Gates.buildRegistry (factsOf checks [])

              Expect.equal reg.Gates.Length 3 "one gate per declared check"

              let ids = reg.Gates |> List.map (fun g -> gateIdValue g.Id) |> List.sort
              Expect.equal ids [ "build:tests"; "docs:lint"; "security:scan" ] "stable domain:checkId ids"
          }

          test "each gate carries the declared Check fields verbatim + non-empty description + timeout" {
              let c = check "build" "tests" None "team-a" High Release BlockOnShip
              let reg = Gates.buildRegistry (factsOf [ c ] [])
              let g = byId "build:tests" reg

              Expect.equal g.Id (GateId "build:tests") "Id = domain:checkId"
              Expect.equal g.Domain (DomainId "build") "Domain verbatim"
              Expect.equal g.Cost High "Cost verbatim"
              Expect.equal g.Owner (Owner "team-a") "Owner verbatim"
              Expect.equal g.Maturity BlockOnShip "Maturity verbatim (not translated to enforcement)"
              Expect.isFalse (System.String.IsNullOrWhiteSpace g.Description) "Description non-empty"
              Expect.stringContains g.Description "tests" "Description names the check id"
              Expect.stringContains g.Description "build" "Description names the domain"
              // A timeout is always present (a value, never null) — the bound check lives in MetadataTests.
              Expect.equal g.Timeout Gates.defaultTimeout "command-less check → defaultTimeout"
          }

          test "Prerequisites = [RequiresCommand c] iff Check.Command = Some c" {
              let withCmd = check "build" "tests" (Some "dotnet-test") "team-a" Medium Ci Warn
              let without = check "docs" "lint" None "team-c" Cheap Local Observe
              let reg = Gates.buildRegistry (factsOf [ withCmd; without ] [ command "dotnet-test" 600 ])

              Expect.equal
                  (byId "build:tests" reg).Prerequisites
                  [ RequiresCommand(CommandId "dotnet-test") ]
                  "command-referencing check → one RequiresCommand prerequisite"

              Expect.equal (byId "docs:lint" reg).Prerequisites [] "command-less check → no prerequisite"
          }

          test "assembling the same facts twice yields byte-identical GateIds (AS2)" {
              let checks =
                  [ check "build" "tests" None "team-a" Medium Ci Warn
                    check "security" "scan" (Some "trivy") "team-b" High Ci BlockOnPr ]

              let facts = factsOf checks [ command "trivy" 120 ]
              let a = Gates.buildRegistry facts
              let b = Gates.buildRegistry facts
              Expect.equal a b "identical facts → identical registry (ids included)"
          }

          test "no declared checks → empty registry, a successful result not an error (AS3, FR-014)" {
              let reg = Gates.buildRegistry (factsOf [] [])
              Expect.equal reg { Gates = [] } "empty checks → empty gates"
          } ]
