module FS.GG.Governance.Routing.Tests.DeterminismTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Routing
open FS.GG.Governance.Routing.Tests.Support

// FR-012 / SC-002 / SC-003 — the route report is byte-stable for identical input, and
// re-ordering the authored path map does not change it.

let private basePairs =
    [ "src/**", "core"
      "src/Adapters/**", "adapters"
      "src/Kernel/Eval.fs", "kernel-eval"
      "docs/**", "docs" ]

let private candidates =
    [ gp "src/Kernel/Eval.fs"; gp "src/Adapters/X.fs"; gp "src/Cli/Host.fs"; gp "docs/g.md"; gp "README.md" ]

let private baseline = Routing.route (facts "." basePairs) candidates

// All permutations of the authored path map (the order-independence generator, SC-003).
let rec private permutations =
    function
    | [] -> [ [] ]
    | xs -> xs |> List.collect (fun x -> permutations (List.filter ((<>) x) xs) |> List.map (fun p -> x :: p))

type Perm = Perm of (string * string) list

type PermArb =
    static member Perm() =
        Arb.fromGen (Gen.elements (permutations basePairs) |> Gen.map Perm)

let private cfg =
    { FsCheckConfig.defaultConfig with
        arbitrary = [ typeof<PermArb> ]
        maxTest = 200 }

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "route twice on the same input → structurally identical RouteReport (SC-002)" {
              Expect.equal (Routing.route (facts "." basePairs) candidates) baseline "two routes identical"
          }

          testPropertyWithConfig cfg "permuting the authored PathMap yields an identical RouteReport (FR-012, SC-003)" (fun (Perm pairs) ->
              Routing.route (facts "." pairs) candidates = baseline) ]
