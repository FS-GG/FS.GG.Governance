module FS.GG.Governance.Gates.Tests.MetadataTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Tests.Support

// US4 (SC-004/SC-005): product-check flag (true iff Release), a carried deterministic freshness
// key naming declared inputs (no clock, no verdict), and a bounded command-or-default timeout —
// including the Tooling = None fallback (C1).

let private byId (id: string) (reg: GateRegistry) =
    reg.Gates |> List.find (fun g -> gateIdValue g.Id = id)

[<Tests>]
let tests =
    testList
        "Metadata"
        [ test "ProductCheck = true iff Check.Environment = Release (SC-004, AS1)" {
              let checks =
                  [ check "rel" "publish" None "team-a" High Release BlockOnRelease
                    check "ci" "build" None "team-b" Medium Ci Warn
                    check "loc" "fmt" None "team-c" Cheap Local Observe
                    check "both" "lint" None "team-d" Cheap LocalOrCi Observe ]

              let reg = Gates.buildRegistry (factsOf checks [])
              Expect.isTrue (byId "rel:publish" reg).ProductCheck "Release → product-check"
              Expect.isFalse (byId "ci:build" reg).ProductCheck "Ci → not product-check"
              Expect.isFalse (byId "loc:fmt" reg).ProductCheck "Local → not product-check"
              Expect.isFalse (byId "both:lint" reg).ProductCheck "LocalOrCi → not product-check"
          }

          test "every gate carries a freshness key equal to the declared inputs, byte-identical twice (AS2/AS3)" {
              let c = check "build" "tests" (Some "dotnet-test") "team-a" Medium Release Warn
              let facts = factsOf [ c ] [ command "dotnet-test" 600 ]
              let g = (Gates.buildRegistry facts) |> byId "build:tests"

              Expect.equal
                  g.FreshnessKey
                  { Check = CheckId "tests"
                    Domain = DomainId "build"
                    Cost = Medium
                    Environment = Release
                    Command = Some(CommandId "dotnet-test") }
                  "freshness key carries the declared identity inputs verbatim"

              // Byte-identical across two assemblies — no clock, no verdict computed.
              Expect.equal (Gates.buildRegistry facts) (Gates.buildRegistry facts) "freshness key is deterministic"
          }

          test "command-referencing gate carries the command's declared timeout; command-less → defaultTimeout (SC-005)" {
              let withCmd = check "build" "tests" (Some "dotnet-test") "team-a" Medium Ci Warn
              let without = check "docs" "lint" None "team-c" Cheap Local Observe
              let reg = Gates.buildRegistry (factsOf [ withCmd; without ] [ command "dotnet-test" 600 ])

              Expect.equal (byId "build:tests" reg).Timeout (TimeoutLimit 600) "uses the command's declared timeout"
              Expect.equal (byId "docs:lint" reg).Timeout Gates.defaultTimeout "command-less → defaultTimeout"
              Expect.equal Gates.defaultTimeout (TimeoutLimit 300) "defaultTimeout is five minutes, bounded"
          }

          test "Tooling = None: a command-referencing check still falls back to defaultTimeout (C1, FR-010)" {
              // The absent-tooling.yml case: the command index is empty, so even a check that names a
              // command resolves to defaultTimeout rather than throwing or producing an unbounded timeout.
              let c = check "build" "tests" (Some "dotnet-test") "team-a" Medium Ci Warn
              let reg = Gates.buildRegistry (factsNoTooling [ c ])
              let g = byId "build:tests" reg
              Expect.equal g.Timeout Gates.defaultTimeout "absent tooling.yml → defaultTimeout fallback"
              // The declared prerequisite is still carried (the command reference is a fact, not a timeout).
              Expect.equal g.Prerequisites [ RequiresCommand(CommandId "dotnet-test") ] "prerequisite still carried"
          } ]
