module FS.GG.Governance.Route.Tests.TraceTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Route.Tests.Support

// US2 (P1): every selected gate explains itself — the selecting path(s), the affected `Domain`, the
// winning glob each path won on (the "rule"), and the declared `Cost` — deduped to one entry per
// gate with all selecting paths in normalized-path ordinal order; fields are declared ids only
// (FR-004/FR-007/FR-012, SC-002/SC-007). Inputs are REAL upstream-assembled values.

let private fixtureFacts =
    facts
        "src"
        [ "src/api/**", "api"
          "src/docs/**", "docs" ]
        []
        [ check "api" "surface" None High
          check "docs" "lint" None Cheap ]
        []

[<Tests>]
let tests =
    testList
        "Trace"
        [ test "a selected gate carries its selecting path, matched glob, domain, and declared cost (AS1, SC-002)" {
              let r = selectOf fixtureFacts [ "src/api/Surface.fs" ]
              let api = r.SelectedGates |> List.exactlyOne

              Expect.equal (gateIdValue api.Gate.Id) "api:surface" "the api gate"
              Expect.equal api.Gate.Domain (DomainId "api") "carries the affected domain"
              Expect.equal api.Gate.Cost High "carries the declared cost verbatim (via the embedded Gate)"

              let sp = api.SelectingPaths |> List.exactlyOne
              Expect.equal sp.Path (gp "src/api/Surface.fs") "the selecting path"
              Expect.equal sp.MatchedGlob (gp "src/api/**") "the F015 winning glob (the rule)"
          }

          test "a gate reached by several paths appears ONCE with ALL selecting paths, path-ordered (AS2, FR-007)" {
              // Two distinct paths both route to `api` → the single `api:surface` gate carries both.
              let r = selectOf fixtureFacts [ "src/api/Zeta.fs"; "src/api/Alpha.fs" ]
              let api = r.SelectedGates |> List.exactlyOne

              let paths = api.SelectingPaths |> List.map (fun p -> p.Path)
              Expect.equal
                  paths
                  [ gp "src/api/Alpha.fs"; gp "src/api/Zeta.fs" ]
                  "both selecting paths recorded once, sorted by normalized path ordinal"
              Expect.equal
                  (api.SelectingPaths |> List.map (fun p -> p.MatchedGlob))
                  [ gp "src/api/**"; gp "src/api/**" ]
                  "each selecting path carries the glob it won on"
          }

          test "every selected gate carries only declared ids and the declared cost (AS3, SC-007)" {
              let r = selectOf fixtureFacts [ "src/api/Surface.fs"; "src/docs/Guide.md" ]

              for sg in r.SelectedGates do
                  // GateId / DomainId render to plain declared strings; no host path, no raw YAML.
                  let (GateId gid) = sg.Gate.Id
                  Expect.isFalse (gid.Contains "/") "GateId is a declared id, not a host path"
                  let (DomainId dom) = sg.Gate.Domain
                  Expect.isNotEmpty dom "domain is a declared id"

                  for p in sg.SelectingPaths do
                      let (GovernedPath pv) = p.Path
                      let (GovernedPath gv) = p.MatchedGlob
                      Expect.isFalse (pv.StartsWith "/") "selecting path is a normalized GovernedPath, not absolute"
                      Expect.isFalse (gv.StartsWith "/") "matched glob is a normalized GovernedPath, not absolute"
          } ]
