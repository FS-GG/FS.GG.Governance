module FS.GG.Governance.Routing.Tests.AmbiguityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Routing.Tests.Support

// US3: ambiguity, catalog-shape diagnostics, and explicit scoping.

let private resultFor (report: RouteReport) (path: string) =
    report.Routings
    |> List.tryPick (fun r -> if r.Path = gp path then Some r.Result else None)

let private hasDiag (report: RouteReport) id =
    report.Diagnostics |> List.exists (fun d -> d.Id = id)

[<Tests>]
let tests =
    testList
        "Ambiguity"
        [ test "co-specific competitors → AmbiguousRoute + deterministic LexicographicTiebreak (FR-006, SC-004)" {
              // src/*/Eval.fs and src/Kernel/*.fs are co-specific; both match src/Kernel/Eval.fs.
              let report =
                  Routing.route (facts "." [ "src/*/Eval.fs", "a"; "src/Kernel/*.fs", "b" ]) [ gp "src/Kernel/Eval.fs" ]

              // ordinal-first glob ('*' < 'K') wins → domain a, reason LexicographicTiebreak.
              Expect.equal
                  (resultFor report "src/Kernel/Eval.fs")
                  (Some(Routed(dom "a", gp "src/*/Eval.fs", LexicographicTiebreak)))
                  "resolves to ordinal-first glob, total"

              let amb = report.Diagnostics |> List.filter (fun d -> d.Id = AmbiguousRoute)
              Expect.hasLength amb 1 "exactly one AmbiguousRoute"
              let d = List.head amb
              Expect.equal d.Path (Some(gp "src/Kernel/Eval.fs")) "names the candidate path"
              Expect.equal d.Globs [ gp "src/*/Eval.fs"; gp "src/Kernel/*.fs" ] "names both competing globs"
          }

          test "same glob string, different domains → ConflictingGlobBinding (excluded, FR-009)" {
              let report =
                  Routing.route (facts "." [ "src/**", "a"; "src/**", "b" ]) [ gp "src/x.fs" ]

              Expect.isTrue (hasDiag report ConflictingGlobBinding) "conflict diagnosed"
              // Excluded from routing entirely → the path matches nothing.
              Expect.equal (resultFor report "src/x.fs") (Some UnmatchedInRoot) "conflicting glob excluded from matching"
          }

          test "reserved-char glob → UnsupportedGlobSyntax (excluded, not silent never-match, FR-010)" {
              let report =
                  Routing.route (facts "." [ "src/[ab].fs", "a"; "src/**", "core" ]) [ gp "src/c.fs" ]

              Expect.isTrue (hasDiag report UnsupportedGlobSyntax) "unsupported syntax diagnosed"
              // The valid src/** still routes; the bad glob is simply excluded.
              Expect.equal (resultFor report "src/c.fs") (Some(Routed(dom "core", gp "src/**", OnlyMatch))) "valid glob still routes"
          }

          test "out-of-scope path never yields an AmbiguousRoute (US3 AS2)" {
              let report =
                  Routing.route (facts "src" [ "src/*/Eval.fs", "a"; "src/Kernel/*.fs", "b" ]) [ gp "docs/Eval.fs" ]

              Expect.equal (resultFor report "docs/Eval.fs") (Some OutOfScope) "outside root → OutOfScope"
              Expect.isFalse (hasDiag report AmbiguousRoute) "no ambiguity for out-of-scope paths"
          }

          test "in-root unmatched carries no severity (US3 AS3, FR-016)" {
              let report = Routing.route (facts "src" [ "src/Kernel/**", "kernel" ]) [ gp "src/README.md" ]
              Expect.equal (resultFor report "src/README.md") (Some UnmatchedInRoot) "in-root miss is plain UnmatchedInRoot"
              Expect.isEmpty report.Diagnostics "no finding/severity emitted for an in-root miss"
          } ]
