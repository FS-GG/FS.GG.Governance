module FS.GG.Governance.RouteJson.Tests.CarryTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteJson
open FS.GG.Governance.RouteJson.Tests.Support

// US3 — findings and freshness carried forward, enforcement excluded: the F017 findings carried
// unchanged in F017 order (empty report → present-and-empty array), and each gate carries its
// declared freshness-key INPUTS — but no cache-eligibility verdict, severity, profile, mode, or
// enforcement field anywhere. Over REAL fixtures, inspecting the EMITTED BYTES.

/// A fixture producing BOTH finding zones: an in-root unclassified path (governedRootUnknown) and an
/// unclassified path on a declared protected surface boundary (protectedBoundary).
let private carryFacts =
    facts
        "src"
        [ "src/build/**", "build" ]
        [ surface GovernedRoot "root" [ "src" ]
          surface ProtectedSurface "api-surface" [ "src/api" ] ]
        [ check "build" "tests" (Some "dotnet-test") Medium Local BlockOnShip
          check "build" "format" None Cheap LocalOrCi Observe ]
        [ command "dotnet-test" 600 ]

let private carryPaths = [ "src/build/a.fs"; "src/api/secret.fs"; "src/loose/x.fs" ]
let private carryResult = resultOf carryFacts carryPaths

/// Assert one emitted finding element matches one in-memory finding verbatim, incl. its zone shape.
let private assertFinding (el: JsonElement) (f: UnknownGovernedPathFinding) =
    Expect.equal (strField el "id") (findingIdToken f.Id) "finding id token"
    let (GovernedPath p) = f.Path
    Expect.equal (strField el "path") p "finding path verbatim"
    Expect.equal (strField el "message") f.Message "finding message verbatim"
    let zone = el.GetProperty "zone"

    match f.Zone with
    | GovernedRootUnknown ->
        Expect.equal zone.ValueKind JsonValueKind.String "governed-root zone is a string"
        Expect.equal (strField el "zone") "governedRootUnknown" "governed-root zone token"
    | ProtectedBoundaryUnknown(SurfaceId sid) ->
        Expect.equal zone.ValueKind JsonValueKind.Object "protected-boundary zone is an object"
        Expect.equal (strField zone "protectedBoundary") sid "protected boundary carries the surface id"

[<Tests>]
let tests =
    testList
        "Carry (US3)"
        [ test "a non-empty F017 report is carried one-to-one, unchanged, in F017 order (AS1, SC-004)" {
              // sanity: the fixture really produced findings (both zones)
              Expect.isNonEmpty carryResult.Findings.Findings "fixture has findings"
              use doc = parse (RouteJson.ofRouteResult carryResult)
              let emitted = findings doc
              Expect.equal (List.length emitted) (List.length carryResult.Findings.Findings) "one emitted finding per F017 finding"

              List.iter2 assertFinding emitted carryResult.Findings.Findings

              // explicit order check by id token
              Expect.equal
                  (findingIds doc)
                  (carryResult.Findings.Findings |> List.map (fun f -> findingIdToken f.Id))
                  "findings in F017 order, unchanged"
          }

          test "both finding zones round-trip: governedRootUnknown (string) and protectedBoundary (object)" {
              use doc = parse (RouteJson.ofRouteResult carryResult)
              let zones = findings doc |> List.map (fun f -> (f.GetProperty "zone").ValueKind)
              Expect.contains zones JsonValueKind.String "a string zone is present (governed-root)"
              Expect.contains zones JsonValueKind.Object "an object zone is present (protected boundary)"
          }

          test "an empty F017 report renders as a present-and-empty findings array (AS2)" {
              // a change touching only a routed path → selected gates but no findings
              let r = resultOf carryFacts [ "src/build/a.fs" ]
              Expect.isEmpty r.Findings.Findings "fixture has no findings"
              use doc = parse (RouteJson.ofRouteResult r)
              Expect.isTrue (hasField doc.RootElement "findings") "findings field present"
              Expect.isEmpty (findings doc) "findings array present and empty"
          }

          test "each gate's freshnessKey carries the five declared inputs, command as string or null (AS3, FR-014)" {
              use doc = parse (RouteJson.ofRouteResult carryResult)

              for sg in carryResult.SelectedGates do
                  let g = selectedGates doc |> List.find (fun e -> strField e "id" = gateIdValue sg.Gate.Id)
                  let fk = g.GetProperty "freshnessKey"
                  Expect.equal (fieldOrder fk) [ "check"; "domain"; "cost"; "environment"; "command" ] "freshnessKey field order"
                  let key = sg.Gate.FreshnessKey
                  let (CheckId c) = key.Check
                  let (DomainId d) = key.Domain
                  Expect.equal (strField fk "check") c "check input"
                  Expect.equal (strField fk "domain") d "domain input"
                  // command: string when Some, JSON null when None
                  match key.Command with
                  | Some(CommandId cmd) ->
                      Expect.equal (strField fk "command") cmd "command input string"
                  | None ->
                      Expect.equal (fk.GetProperty "command").ValueKind JsonValueKind.Null "command input is JSON null"
          }

          test "no cache/severity/profile/mode/enforcement field appears anywhere (AS4, FR-011)" {
              use doc = parse (RouteJson.ofRouteResult carryResult)
              let forbidden = [ "cacheEligibility"; "cacheEligible"; "severity"; "profile"; "mode"; "enforcement" ]

              // top level
              for name in forbidden do
                  Expect.isFalse (hasField doc.RootElement name) (sprintf "top-level has no %s" name)

              // each gate and its freshnessKey
              for g in selectedGates doc do
                  for name in forbidden do
                      Expect.isFalse (hasField g name) (sprintf "gate has no %s" name)
                  let fk = g.GetProperty "freshnessKey"
                  for name in forbidden do
                      Expect.isFalse (hasField fk name) (sprintf "freshnessKey has no %s" name)

              // each finding
              for f in findings doc do
                  for name in forbidden do
                      Expect.isFalse (hasField f name) (sprintf "finding has no %s" name)
          } ]
