module FS.GG.Governance.RouteJson.Tests.DeterminismTests

open System
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteJson
open FS.GG.Governance.RouteJson.Tests.Support

// US2 — a stable, versioned schema for CI and agents: identical inputs → byte-identical document;
// value-equal inputs from differently-ordered upstream inputs → identical document; a declared
// schemaVersion + a fixed top-level field order; and none of the excluded enforcement/verdict/
// raw-YAML/host-path/timestamp/environment tokens. Properties run over REAL upstream-assembled inputs.

/// A real multi-domain fixture; the property generators draw candidate paths from this world.
let private fixtureFacts =
    facts
        "src"
        [ "src/build/**", "build"
          "src/docs/**", "docs"
          "src/api/**", "api" ]
        [ surface GovernedRoot "root" [ "src" ] ]
        [ check "build" "tests" (Some "dotnet-test") Medium Local BlockOnShip
          check "build" "format" None Cheap Local Observe
          check "docs" "lint" None Cheap LocalOrCi Warn
          check "api" "surface" None High Ci BlockOnPr
          check "release" "audit" None Exhaustive Release BlockOnRelease ]
        [ command "dotnet-test" 600 ]

/// The candidate-path pool: routed paths across three domains, an in-root unmatched path (→ a
/// finding), and an out-of-scope path. Generated changes are sublists of this pool.
let private pool =
    [ "src/build/A.fs"
      "src/build/B.fs"
      "src/docs/G.md"
      "src/api/Surface.fs"
      "src/api/Other.fs"
      "src/loose/x.fs"
      "../outside/y.fs" ]

/// A generator of candidate-path changes: any sublist of the pool, in any order.
let private genPaths : Gen<string list> =
    gen {
        let! picks = Gen.listOf (Gen.elements pool)
        return picks
    }

let private config = { FsCheckConfig.defaultConfig with maxTest = 200; arbitrary = [] }

/// A real, findings-bearing, multi-gate route for the sweeps over populated sections.
let private populated = resultOf fixtureFacts pool

[<Tests>]
let tests =
    testList
        "Determinism (US2)"
        [ testPropertyWithConfig config "twice-identical: ofRouteResult is byte-identical for identical input (AS1, SC-002)"
          <| Prop.forAll (Arb.fromGen genPaths) (fun paths ->
              let r = resultOf fixtureFacts paths
              RouteJson.ofRouteResult r = RouteJson.ofRouteResult r)

          test "fixed-fixture twice-identical equality (SC-002)" {
              Expect.equal (RouteJson.ofRouteResult populated) (RouteJson.ofRouteResult populated) "same result → same bytes"
          }

          testPropertyWithConfig config "permutation-invariant in the candidate paths (AS2, SC-003)"
          <| Prop.forAll (Arb.fromGen genPaths) (fun paths ->
              // sortDescending is a permutation of the same multiset of candidate paths.
              let a = RouteJson.ofRouteResult (resultOf fixtureFacts paths)
              let b = RouteJson.ofRouteResult (resultOf fixtureFacts (List.sortDescending paths))
              a = b)

          testPropertyWithConfig config "permutation-invariant in the registry's gate order (AS2, SC-003)"
          <| Prop.forAll (Arb.fromGen genPaths) (fun paths ->
              // vary only the registry's gate-list order (still a real registry value), mirroring the
              // F019 permutation test, then project both results.
              let report = reportOf fixtureFacts paths
              let findings = findingsOf fixtureFacts report
              let real = registryOf fixtureFacts
              let reversed = { Gates = List.rev real.Gates }
              let a = RouteJson.ofRouteResult (FS.GG.Governance.Route.Route.select real report findings)
              let b = RouteJson.ofRouteResult (FS.GG.Governance.Route.Route.select reversed report findings)
              a = b)

          test "the document carries the declared schemaVersion and the fixed top-level field order (AS3, FR-013)" {
              use doc = parse (RouteJson.ofRouteResult populated)
              Expect.equal (strField doc.RootElement "schemaVersion") RouteJson.schemaVersion "schemaVersion field equals the constant"
              Expect.equal (topLevelFieldOrder doc) [ "schemaVersion"; "selectedGates"; "findings"; "cost" ] "fixed top-level field order"
          }

          test "exclusion sweep: the emitted text contains no enforcement/verdict/raw-YAML/clock token (AS4, SC-007, FR-011/FR-012)" {
              let json = RouteJson.ofRouteResult populated
              let lower = json.ToLowerInvariant()

              let denied =
                  [ "severity"; "profile"; "\"mode\""; "enforcement"; "cacheeligib"; "verdict"
                    "blockers"; "warnings"; "exitcode"; "expectedartifacts"; "timestamp" ]

              for token in denied do
                  Expect.isFalse (lower.Contains token) (sprintf "excluded token %A must not appear" token)

              // no ISO-8601-ish wall-clock value (a 'T' between digits, e.g. 2026-06-20T..)
              Expect.isFalse (System.Text.RegularExpressions.Regex.IsMatch(json, @"\d{4}-\d{2}-\d{2}T\d{2}:")) "no wall-clock timestamp"
          }

          test "positive path-allowlist: every emitted path/matchedGlob is a declared GovernedPath (FR-012)" {
              use doc = parse (RouteJson.ofRouteResult populated)

              // the universe of declared path strings the input route can legitimately carry
              let declared =
                  let globs = fixtureFacts.Capabilities.PathMap |> List.map (fun e -> let (GovernedPath g) = e.Glob in g)
                  let changed = pool |> List.map (fun p -> let (GovernedPath n) = normalizePath p in n)
                  Set.ofList (globs @ changed)

              for p in allEmittedPaths doc do
                  Expect.isTrue (declared.Contains p) (sprintf "emitted path %A is a declared GovernedPath (no host/absolute path)" p)

              // and none is an absolute/host path by shape
              for p in allEmittedPaths doc do
                  Expect.isFalse (p.StartsWith "/") (sprintf "%A is not absolute" p)
                  Expect.isFalse (System.Text.RegularExpressions.Regex.IsMatch(p, @"^[A-Za-z]:")) (sprintf "%A is not a drive path" p)
          } ]
