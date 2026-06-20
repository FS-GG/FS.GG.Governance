module FS.GG.Governance.Route.Tests.SelectionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Route.Tests.Support

// US1 (P1, MVP): for each `Routed (d, _, _)` path select EXACTLY the registry gates with
// `Gate.Domain = d`; the change's selected set is the union across `Routed` paths deduped by
// `GateId`; unreached-domain gates are absent; `UnmatchedInRoot`/`OutOfScope` select nothing; the
// join is on declared-id equality, never a re-parsed `GateId` string (FR-002/FR-003/FR-010, SC-001).
// Every input is REAL: a `Gates.buildRegistry` registry + a `Routing.route` report over real facts.

/// A fixture with three domains — `build`, `docs`, `release` — and the path-map globs that route to
/// the first two. `release` exists in the registry but no fixture path reaches it.
let private fixtureFacts =
    facts
        "src"
        [ "src/build/**", "build"
          "src/docs/**", "docs" ]
        []
        [ check "build" "tests" (Some "dotnet-test") Medium
          check "build" "format" None Cheap
          check "docs" "lint" None Cheap
          check "release" "audit" None High ]
        [ command "dotnet-test" 600 ]

let private gateIds (r: RouteResult) =
    r.SelectedGates |> List.map (fun sg -> gateIdValue sg.Gate.Id)

[<Tests>]
let tests =
    testList
        "Selection"
        [ test "a Routed path selects EVERY gate of its domain, annotated with that path (AS1, SC-001)" {
              let r = selectOf fixtureFacts [ "src/build/Core.fs" ]
              Expect.equal (gateIds r) [ "build:format"; "build:tests" ] "both build gates selected, GateId-ordered"

              for sg in r.SelectedGates do
                  let ps = sg.SelectingPaths |> List.map (fun p -> p.Path)
                  Expect.equal ps [ gp "src/build/Core.fs" ] "each gate annotated with the selecting path"
          }

          test "a gate whose domain is reached by no Routed path is ABSENT (AS2)" {
              let r = selectOf fixtureFacts [ "src/build/Core.fs" ]
              Expect.isFalse
                  (gateIds r |> List.contains "release:audit")
                  "release gate not selected — its domain was never routed to"
              Expect.isFalse (gateIds r |> List.contains "docs:lint") "docs gate not selected either"
          }

          test "two Routed paths to different domains select the UNION, none omitted/duplicated (AS3)" {
              let r = selectOf fixtureFacts [ "src/build/Core.fs"; "src/docs/Guide.md" ]
              Expect.equal
                  (gateIds r)
                  [ "build:format"; "build:tests"; "docs:lint" ]
                  "union of build + docs gates, GateId-ordered, no duplicates"
          }

          test "UnmatchedInRoot and OutOfScope paths select NO gate — no fallback (FR-003)" {
              // `src/loose/x.fs` is in-root but matches no glob; `../outside/y.fs` is out of scope.
              let report = reportOf fixtureFacts [ "src/loose/x.fs"; "../outside/y.fs" ]

              // sanity: the report really classifies them as UnmatchedInRoot / OutOfScope
              let results = report.Routings |> List.map (fun pr -> pr.Result)
              Expect.contains results UnmatchedInRoot "in-root unmatched path present"
              Expect.contains results OutOfScope "out-of-scope path present"

              let r = FS.GG.Governance.Route.Route.select (registryOf fixtureFacts) report (findingsOf fixtureFacts report)
              Expect.isEmpty r.SelectedGates "no gate selected — there is no select-everything fallback"
          }

          test "selection joins on declared Gate.Domain by id equality, not a re-parsed GateId (FR-010)" {
              // A domain whose NAME contains a colon: the GateId string `"build:extra:weird"` would
              // mis-parse to domain `"build"` if anyone split on ':'. The declared `Gate.Domain` is
              // the whole `"build:extra"`, and only a path routed to `"build:extra"` selects it.
              let f =
                  facts
                      "src"
                      [ "src/weird/**", "build:extra" ]
                      []
                      [ check "build:extra" "weird" None Cheap
                        check "build" "tests" None Cheap ]
                      []

              let r = selectOf f [ "src/weird/thing.fs" ]
              Expect.equal (gateIds r) [ "build:extra:weird" ] "selected by declared Gate.Domain id-equality"
              Expect.isFalse
                  (gateIds r |> List.contains "build:tests")
                  "the plain `build` gate is NOT selected — domains are distinct ids, not a colon split"
          }

          test "an AmbiguousRoute diagnostic on a Routed path still selects its resolved domain (D7)" {
              // Two equally-specific globs for the same path → routing resolves to one domain and
              // reports an AmbiguousRoute diagnostic. `select` reads only the resolved `Routed`
              // outcome and never consumes `report.Diagnostics`.
              let f =
                  facts
                      "src"
                      [ "src/app/*.fs", "build"
                        "src/*/Main.fs", "docs" ]
                      []
                      [ check "build" "tests" None Cheap
                        check "docs" "lint" None Cheap ]
                      []

              let report = reportOf f [ "src/app/Main.fs" ]
              // The fixture is only meaningful if routing actually flagged the ambiguity.
              let resolvedDomain =
                  report.Routings
                  |> List.tryPick (fun pr ->
                      match pr.Result with
                      | Routed(d, _, _) -> Some d
                      | _ -> None)

              Expect.isSome resolvedDomain "the ambiguous path still resolves to one domain"
              let r = FS.GG.Governance.Route.Route.select (registryOf f) report (findingsOf f report)
              // Whichever domain won, exactly that domain's single gate is selected — never both,
              // never a re-resolution.
              Expect.equal r.SelectedGates.Length 1 "exactly the resolved domain's gate is selected"
          }

          test "an empty registry yields an empty, successful route (FR-009)" {
              let f = facts "src" [ "src/build/**", "build" ] [] [] []
              let r = selectOf f [ "src/build/Core.fs" ]
              Expect.isEmpty r.SelectedGates "no gates exist → empty selection, a valid success"
          } ]
