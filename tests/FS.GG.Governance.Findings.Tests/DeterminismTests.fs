module FS.GG.Governance.Findings.Tests.DeterminismTests

open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Findings.Tests.Support

// US4: the finding set is byte-identical for identical inputs and unchanged under re-ordering of
// the candidate paths OR the authored surfaces (SC-004); every message names the path + ≥1
// remediation with no leaked vocabulary (SC-006); and `findUnknownGovernedPaths` is total — it
// never throws (the no-throw clause of SC-005 / FR-011/FR-012).

let private baseSurfaces =
    [ surface Routine "legacy" [ "src/Legacy" ]
      surface ProtectedSurface "core" [ "src/Core" ]
      surface ProtectedSurface "core2" [ "src/Core" ] ]

let private basePaths =
    [ "src/New.fs"
      "src/Core/Secret.fs"
      "src/Legacy/Old.fs"
      "src/Kernel/k.fs"
      "docs/x.md"
      "src/Another.fs" ]

let private run paths surfaces =
    let f = facts "src" [ "src/Kernel/**", "kernel" ] surfaces
    Findings.findUnknownGovernedPaths f (routeOf f paths)

let private baseline = run basePaths baseSurfaces

let rec private permutations =
    function
    | [] -> [ [] ]
    | xs -> xs |> List.collect (fun x -> permutations (List.filter ((<>) x) xs) |> List.map (fun p -> x :: p))

type PathPerm = PathPerm of string list
type SurfPerm = SurfPerm of Surface list

type Arbs =
    static member PathPerm() =
        Arb.fromGen (Gen.elements (permutations basePaths) |> Gen.map PathPerm)

    static member SurfPerm() =
        Arb.fromGen (Gen.elements (permutations baseSurfaces) |> Gen.map SurfPerm)

let private cfg =
    { FsCheckConfig.defaultConfig with
        arbitrary = [ typeof<Arbs> ]
        maxTest = 200 }

// ── Totality generators (arbitrary routings + arbitrary surfaces, the no-throw domain) ──

let private resultGen =
    Gen.elements
        [ Routed(DomainId "d", GovernedPath "g/**", OnlyMatch)
          UnmatchedInRoot
          OutOfScope ]

let private rawPathGen =
    Gen.elements [ "src/a.fs"; "src/b/c.fs"; "docs/x"; "src/Kernel/k.fs"; "lib/y.fs"; "src" ]

let private routingGen =
    Gen.zip rawPathGen resultGen |> Gen.map (fun (p, r) -> routing p r)

let private classGen =
    Gen.elements [ Routine; GovernedRoot; ProtectedSurface; GeneratedView; ReleaseSurface ]

let private surfaceGen =
    gen {
        let! cls = classGen
        let! id = Gen.elements [ "s1"; "s2"; "s3" ]
        let! paths = Gen.listOf (Gen.elements [ "src"; "src/b"; "docs"; "src/Kernel" ])
        return surface cls id paths
    }

type TotalityArbs =
    static member Routings() = Arb.fromGen (Gen.listOf routingGen)
    static member Surfaces() = Arb.fromGen (Gen.listOf surfaceGen)

let private totalityCfg =
    { FsCheckConfig.defaultConfig with
        arbitrary = [ typeof<TotalityArbs> ]
        maxTest = 500 }

let private pathStr (GovernedPath s) = s

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "compute twice over identical inputs → structurally identical FindingReport (US4 AS1, SC-004)" {
              Expect.equal (run basePaths baseSurfaces) baseline "two computations identical, including order"
          }

          testPropertyWithConfig cfg "permuting the candidate paths yields an identical FindingReport (US4 AS2, FR-009)" (fun (PathPerm paths) ->
              run paths baseSurfaces = baseline)

          testPropertyWithConfig cfg "permuting the authored surfaces yields an identical FindingReport (US4 AS2, FR-009)" (fun (SurfPerm surfaces) ->
              run basePaths surfaces = baseline)

          testPropertyWithConfig totalityCfg "findUnknownGovernedPaths is total — never throws and returns a sorted report (SC-005, FR-011/FR-012)" (fun (routings: PathRouting list) (surfaces: Surface list) ->
              let f = facts "src" [] surfaces
              let report = Findings.findUnknownGovernedPaths f (routingsWith routings)
              // Findings sorted by ordinal path, and at most one finding per distinct path (dedup).
              let paths = report.Findings |> List.map (fun x -> pathStr x.Path)
              paths = List.sortWith (fun a b -> System.String.CompareOrdinal(a, b)) paths
              && List.length paths = List.length (List.distinct paths))

          test "every message names the path + a concrete remediation, with no leaked vocabulary (US4 AS3, SC-006)" {
              for fnd in baseline.Findings do
                  let m = fnd.Message
                  Expect.stringContains m (pathStr fnd.Path) "message names the offending path"

                  let hasRemediation =
                      m.Contains "path-map glob" || m.Contains "mark the region routine" || m.Contains "classify the surface"

                  Expect.isTrue hasRemediation (sprintf "message offers a concrete remediation: %s" m)
                  Expect.isFalse (m.Contains "\\") "no host-path separators"
                  Expect.isFalse (m.Contains ".yml") "no raw YAML"
                  Expect.isFalse (m.Contains ".yaml") "no raw YAML"
          }

          test "a protected-boundary message also names the escalating SurfaceId (cross-checks T023)" {
              let f = facts "src" [] [ surface ProtectedSurface "kernel-core" [ "src/Core" ] ]
              let report = Findings.findUnknownGovernedPaths f (routeOf f [ "src/Core/x.fs" ])
              let m = (List.head report.Findings).Message
              Expect.stringContains m "kernel-core" "protected message names the escalating SurfaceId"
          } ]
