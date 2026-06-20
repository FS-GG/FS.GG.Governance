module FS.GG.Governance.GatesJson.Tests.CarryTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GatesJson
open FS.GG.Governance.GatesJson.Tests.Support

// US3 — freshness keys carried forward, enforcement excluded: each gate carries its declared
// freshness-key INPUTS (`check`/`domain`/`cost`/`environment`/`command`, the `None` command as
// explicit JSON null) and its `productCheck` flag verbatim — but no cache-eligibility verdict,
// severity, profile, mode, or enforcement field anywhere. Over REAL fixtures, inspecting the EMITTED
// BYTES.

/// A fixture mixing: a gate with a Some freshness command (build:tests), a gate with a None command
/// (build:format), and a release gate (productCheck = true, since Environment = Release).
let private carryRegistry : GateRegistry =
    registryFor
        [ check "build" "tests" (Some "dotnet-test") Medium Local BlockOnShip
          check "build" "format" None Cheap LocalOrCi Observe
          check "release" "publish" None High Release BlockOnRelease ]
        [ command "dotnet-test" 600 ]

[<Tests>]
let tests =
    testList
        "Carry (US3)"
        [ test "each gate's freshnessKey carries the five declared inputs (AS1, SC-004, FR-014)" {
              use doc = parse (GatesJson.ofGateRegistry carryRegistry)

              for gate in carryRegistry.Gates do
                  let g = gateById doc (gateIdValue gate.Id)
                  let fk = g.GetProperty "freshnessKey"
                  Expect.equal (fieldOrder fk) [ "check"; "domain"; "cost"; "environment"; "command" ] "freshnessKey field order"
                  let key = gate.FreshnessKey
                  let (CheckId c) = key.Check
                  let (DomainId d) = key.Domain
                  Expect.equal (strField fk "check") c "check input verbatim"
                  Expect.equal (strField fk "domain") d "domain input verbatim"
                  Expect.equal (strField fk "cost") (match key.Cost with Cheap -> "cheap" | Medium -> "medium" | High -> "high" | Exhaustive -> "exhaustive") "cost input token"
          }

          test "a Some freshness command renders the command string; a None command renders explicit JSON null (AS2, SC-004, FR-014)" {
              use doc = parse (GatesJson.ofGateRegistry carryRegistry)

              for gate in carryRegistry.Gates do
                  let g = gateById doc (gateIdValue gate.Id)
                  let fk = g.GetProperty "freshnessKey"
                  match gate.FreshnessKey.Command with
                  | Some(CommandId cmd) -> Expect.equal (strField fk "command") cmd "command input string"
                  | None -> Expect.equal (fk.GetProperty "command").ValueKind JsonValueKind.Null "command input is explicit JSON null"
          }

          test "productCheck is carried verbatim from the registry, not re-derived (AS3, FR-002)" {
              use doc = parse (GatesJson.ofGateRegistry carryRegistry)
              for gate in carryRegistry.Gates do
                  let g = gateById doc (gateIdValue gate.Id)
                  Expect.equal (g.GetProperty("productCheck").GetBoolean()) gate.ProductCheck "productCheck verbatim"
              // the release gate carries productCheck = true (Environment = Release)
              Expect.isTrue ((gateById doc "release:publish").GetProperty("productCheck").GetBoolean()) "release gate is a product check"
          }

          test "no cache/severity/profile/mode/enforcement field appears anywhere (AS4, FR-011)" {
              use doc = parse (GatesJson.ofGateRegistry carryRegistry)
              let forbidden = [ "cacheEligibility"; "cacheEligible"; "severity"; "profile"; "mode"; "enforcement" ]

              for name in forbidden do
                  Expect.isFalse (hasField doc.RootElement name) (sprintf "top-level has no %s" name)

              for g in gates doc do
                  for name in forbidden do
                      Expect.isFalse (hasField g name) (sprintf "gate has no %s" name)
                  let fk = g.GetProperty "freshnessKey"
                  for name in forbidden do
                      Expect.isFalse (hasField fk name) (sprintf "freshnessKey has no %s" name)
          }

          test "cost/maturity render verbatim with no enforcement translation, no weighted cost scalar (SC-005, FR-005)" {
              use doc = parse (GatesJson.ofGateRegistry carryRegistry)
              // cost is a declared token, never a number; maturity is the declared vocabulary, not enforcement.
              let g = gateById doc "build:tests"
              Expect.equal (g.GetProperty "cost").ValueKind JsonValueKind.String "cost is a token string, not a scalar"
              Expect.equal (strField g "cost") "medium" "declared cost tier verbatim"
              Expect.equal (strField g "maturity") "blockOnShip" "declared maturity verbatim (not enforcement)"
              Expect.equal (strField (gateById doc "release:publish") "maturity") "blockOnRelease" "release maturity verbatim"
          } ]
