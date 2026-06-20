module FS.GG.Governance.Routing.Tests.RoutingTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Routing.Tests.Support

// US1: route a non-overlapping path map; assert routed / in-root-unmatched / out-of-scope and
// deterministic ordering (FR-004/007/008, SC-001/SC-005).

// Governed root is "src"; a non-overlapping path map. "docs/..." is out of scope.
let private sampleFacts =
    facts "src" [ "src/Kernel/**", "kernel"; "src/Adapters/**", "adapters"; "src/Cli/**", "cli" ]

let private resultFor (report: RouteReport) (path: string) =
    report.Routings
    |> List.tryPick (fun r -> if r.Path = gp path then Some r.Result else None)

[<Tests>]
let tests =
    testList
        "Routing"
        [ test "a matched path routes to its single domain with OnlyMatch" {
              let report = Routing.route sampleFacts [ gp "src/Kernel/Eval.fs" ]

              Expect.equal
                  (resultFor report "src/Kernel/Eval.fs")
                  (Some(Routed(dom "kernel", gp "src/Kernel/**", OnlyMatch)))
                  "single non-overlapping match → OnlyMatch"
          }

          test "an in-root path matching nothing is UnmatchedInRoot" {
              let report = Routing.route sampleFacts [ gp "src/README.md" ]
              Expect.equal (resultFor report "src/README.md") (Some UnmatchedInRoot) "in-root, no glob → UnmatchedInRoot"
              Expect.isEmpty report.Diagnostics "clean fixture emits no diagnostics"
          }

          test "a path outside the governed root is OutOfScope" {
              let report = Routing.route sampleFacts [ gp "docs/guide.md" ]
              Expect.equal (resultFor report "docs/guide.md") (Some OutOfScope) "outside root → OutOfScope"
          }

          test "Routings are sorted by path (ordinal), independent of input order" {
              let report =
                  Routing.route sampleFacts [ gp "src/Cli/Host.fs"; gp "src/Adapters/A.fs"; gp "src/Kernel/K.fs" ]

              let paths = report.Routings |> List.map (fun r -> let (GovernedPath p) = r.Path in p)
              Expect.equal paths (List.sort paths) "routings sorted by ordinal path"
          }

          test "each Routed names its glob + reason and uses only declared domains (SC-005)" {
              let report =
                  Routing.route sampleFacts [ gp "src/Kernel/Eval.fs"; gp "src/Adapters/A.fs" ]

              let declared = sampleFacts.Capabilities.Domains

              for r in report.Routings do
                  match r.Result with
                  | Routed(d, GovernedPath g, _) ->
                      Expect.contains declared d "routed domain is a declared domain (no leaked vocabulary)"
                      Expect.isNotEmpty g "matched glob is recorded for explainability"
                  | _ -> ()
          } ]
