module FS.GG.Governance.GatesJson.Tests.ProjectionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GatesJson
open FS.GG.Governance.GatesJson.Tests.Support

// US1 — render a real gate registry to a deterministic gates.json: each declared gate by its declared
// id with carried F018 metadata; no invented gate; present-and-empty prerequisite arrays; an empty
// registry → valid empty-`gates` document. Every test projects a REAL `GateRegistry` from the genuine
// F018 `Gates.buildRegistry` and inspects the EMITTED BYTES via JsonDocument (research D7).

// ── shared real fixture: two build gates (one with a command prerequisite, one without), one docs
//    gate, one release gate — all declared, all rendered (whole-catalog, no per-change selection) ──

let private fixtureRegistry : GateRegistry =
    registryFor
        [ check "build" "format" None Cheap Local Observe
          check "build" "tests" (Some "dotnet-test") Medium Local BlockOnShip
          check "docs" "lint" None Cheap Local Warn
          check "release" "publish" None High Release BlockOnRelease ]
        [ command "dotnet-test" 600 ]

[<Tests>]
let tests =
    testList
        "Projection (US1)"
        [ test "every declared gate is present exactly once by declared id with carried metadata verbatim (SC-001)" {
              use doc = parse (GatesJson.ofGateRegistry fixtureRegistry)

              // one emitted gate per real Gate, by declared id, in the same (GateId ordinal) order
              let emittedIds = gateIds doc
              let expectedIds = fixtureRegistry.Gates |> List.map (fun g -> gateIdValue g.Id)
              Expect.equal emittedIds expectedIds "emitted gate ids match the registry's gates, in order"
              Expect.equal (List.length emittedIds) (List.length (List.distinct emittedIds)) "each gate appears exactly once"

              // each carried metadata field matches the F018 Gate verbatim
              for gate in fixtureRegistry.Gates do
                  let g = gateById doc (gateIdValue gate.Id)
                  let (DomainId domain) = gate.Domain
                  let (Owner owner) = gate.Owner
                  let (TimeoutLimit secs) = gate.Timeout
                  Expect.equal (strField g "domain") domain "domain verbatim"
                  Expect.equal (strField g "description") gate.Description "description verbatim"
                  Expect.equal (g.GetProperty("timeout").GetInt32()) secs "timeout (int seconds) verbatim"
                  Expect.equal (strField g "owner") owner "owner verbatim"
                  Expect.equal (g.GetProperty("productCheck").GetBoolean()) gate.ProductCheck "productCheck verbatim"
          }

          test "build:tests carries its declared cost/maturity tokens and its command prerequisite; a command-less gate is present-and-empty (FR-004, FR-005)" {
              use doc = parse (GatesJson.ofGateRegistry fixtureRegistry)
              let g = gateById doc "build:tests"
              Expect.equal (strField g "cost") "medium" "declared cost token (not a weighted scalar)"
              Expect.equal (strField g "maturity") "blockOnShip" "declared maturity carried verbatim (not enforcement)"
              Expect.equal (g.GetProperty("timeout").GetInt32()) 600 "timeout resolved from the referenced command, verbatim"
              Expect.equal (prerequisites g) [ "dotnet-test" ] "the command prerequisite is rendered, in carried order"

              // a command-less gate renders a present-and-empty prerequisites array, never an omitted field
              let fmt = gateById doc "build:format"
              Expect.isTrue (hasField fmt "prerequisites") "prerequisites field present"
              Expect.isEmpty (prerequisites fmt) "no command → present-and-empty prerequisites"
          }

          test "no gate the registry did not contain appears; nothing is invented (FR-003)" {
              use doc = parse (GatesJson.ofGateRegistry fixtureRegistry)
              let emitted = gateIds doc |> Set.ofList
              let expected = fixtureRegistry.Gates |> List.map (fun g -> gateIdValue g.Id) |> Set.ofList
              Expect.equal emitted expected "exactly the registry's gates, no extras"
          }

          test "an empty registry projects to a present-and-empty gates array, never an error or placeholder (FR-009)" {
              let empty = registryFor [] []
              Expect.isEmpty empty.Gates "fixture registry is empty"
              use doc = parse (GatesJson.ofGateRegistry empty)
              Expect.isTrue (hasField doc.RootElement "gates") "gates field present"
              Expect.isEmpty (gates doc) "gates array present and empty"
          }

          test "a domain containing the gate-id separator renders id and domain verbatim, no re-parse (FR-008/FR-010)" {
              // domain "a:b" + check "tests" → GateId "a:b:tests" (a two-colon id). DomainId carrying a
              // colon is a legitimately-typed value fed through the real buildRegistry — real evidence.
              let r = registryFor [ check "a:b" "tests" None Medium Local Observe ] []
              use doc = parse (GatesJson.ofGateRegistry r)
              let gate = r.Gates |> List.exactlyOne
              let g = gateById doc (gateIdValue gate.Id)
              let (DomainId domain) = gate.Domain
              Expect.equal (strField g "id") (gateIdValue gate.Id) "id equals gateIdValue verbatim (id is a:b:tests)"
              Expect.equal (strField g "domain") domain "domain equals declared DomainId, no separator re-derivation"
              Expect.equal domain "a:b" "the declared domain carried the separator verbatim"
          }

          test "a default-timeout gate and a command-derived-timeout gate both render their TimeoutLimit verbatim (FR-006)" {
              // build:tests references dotnet-test (600s); build:format references no command → defaultTimeout.
              use doc = parse (GatesJson.ofGateRegistry fixtureRegistry)
              let derived = fixtureRegistry.Gates |> List.find (fun g -> gateIdValue g.Id = "build:tests")
              let defaulted = fixtureRegistry.Gates |> List.find (fun g -> gateIdValue g.Id = "build:format")
              let (TimeoutLimit dSecs) = derived.Timeout
              let (TimeoutLimit fSecs) = defaulted.Timeout
              Expect.equal ((gateById doc "build:tests").GetProperty("timeout").GetInt32()) dSecs "command-derived timeout verbatim"
              Expect.equal ((gateById doc "build:format").GetProperty("timeout").GetInt32()) fSecs "default timeout carried verbatim"
          }

          test "JSON-special characters in free text round-trip via the writer, never manual escaping (FR-002/FR-012)" {
              // a checkId carrying a quote, a backslash, and a newline flows into the composed gate
              // description; the value read back from the parsed document must equal it exactly.
              let weird = "te\"st\\x\ny"
              let r = registryFor [ check "q" weird None Medium Local Observe ] []
              use doc = parse (GatesJson.ofGateRegistry r)
              let gate = r.Gates |> List.exactlyOne
              let g = gateById doc (gateIdValue gate.Id)
              Expect.equal (strField g "id") (gateIdValue gate.Id) "id with special chars round-trips exactly"
              Expect.equal (strField g "description") gate.Description "description with special chars round-trips exactly"
          } ]
