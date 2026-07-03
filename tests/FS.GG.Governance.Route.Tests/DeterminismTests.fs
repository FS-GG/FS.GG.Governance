module FS.GG.Governance.Route.Tests.DeterminismTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Tests.Support

// US5 (P2): the same inputs always produce the same selected gates, selecting paths, findings, and
// cost in the same order; re-ordering the input candidate paths or the registry's gate list never
// changes the result; `select` is total over any well-typed input (FR-007/FR-012, SC-005/SC-006).
// FsCheck drives the properties over REAL upstream-assembled inputs.

/// A real multi-domain fixture; the property generators draw candidate paths from this world.
let private fixtureFacts =
    facts
        "src"
        [ "src/build/**", "build"
          "src/docs/**", "docs"
          "src/api/**", "api" ]
        []
        [ check "build" "tests" None Medium
          check "build" "format" None Cheap
          check "docs" "lint" None Cheap
          check "api" "surface" None High
          check "release" "audit" None Exhaustive ]
        []

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

[<Tests>]
let tests =
    testList
        "Determinism"
        [ testPropertyWithConfig config "twice-identical: select is byte-identical for identical inputs (AS1, SC-005)"
          <| Prop.forAll (Arb.fromGen genPaths) (fun paths ->
              selectOf fixtureFacts paths = selectOf fixtureFacts paths)

          testPropertyWithConfig config "permutation-invariant in the candidate paths (AS2, SC-005)"
          <| Prop.forAll (Arb.fromGen genPaths) (fun paths ->
              // sortDescending is a permutation of the same multiset of candidate paths.
              selectOf fixtureFacts paths = selectOf fixtureFacts (List.sortDescending paths))

          testPropertyWithConfig config "permutation-invariant in the registry's gate order (AS3, SC-005)"
          <| Prop.forAll (Arb.fromGen genPaths) (fun paths ->
              // `buildRegistry` already returns GateId-sorted gates, so vary the order ourselves by
              // constructing a GateRegistry from the real registry's gates, reversed. The registry is
              // still a real value — only its list order differs.
              let report = reportOf fixtureFacts paths
              let findings = findingsOf fixtureFacts report
              let real = registryOf fixtureFacts
              let reversed = { Gates = List.rev real.Gates }
              let a = FS.GG.Governance.Route.Route.select real report findings
              let b = FS.GG.Governance.Route.Route.select reversed report findings
              a = b)

          testPropertyWithConfig config "total: select never throws; empty selection is a valid zero-cost route (SC-006, FR-008/FR-009)"
          <| Prop.forAll (Arb.fromGen genPaths) (fun paths ->
              let r = selectOf fixtureFacts paths
              // never throws (reaching here proves it); and an empty selection is a valid success
              // with the all-zero rollup.
              if List.isEmpty r.SelectedGates then
                  r.Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 }
              else
                  let total = r.Cost.Cheap + r.Cost.Medium + r.Cost.High + r.Cost.Exhaustive
                  total = r.SelectedGates.Length)

          testCase "empty routings and empty registry both yield an empty, successful route (SC-006)"
          <| fun () ->
              let emptyReg = facts "src" [ "src/build/**", "build" ] [] [] []
              let r1 = selectOf emptyReg [ "src/build/Core.fs" ]
              Expect.isEmpty r1.SelectedGates "empty registry → empty route"

              let r2 = selectOf fixtureFacts []
              Expect.isEmpty r2.SelectedGates "empty routings → empty route"
              Expect.equal r2.Cost { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } "all-zero cost" ]
