module FS.GG.Governance.CurrencySensing.Tests.ParseTests

// parseManifest over real refresh.yml text: the currency-enforcement dial + the per-view fields the currency
// decision needs (id/kind/sources/generatorBasis). Pure, total — malformed ⇒ None.

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.CurrencySensing.CurrencySensing

let private lines (s: string) = s.Split('\n') |> List.ofArray

let private yml =
    "currency-enforcement: block-on-ship\n"
    + "views:\n"
    + "  - id: route-projection\n"
    + "    kind: route-projection\n"
    + "    output: docs/route.generated.json\n"
    + "    sources:\n      - .fsgg/route.yml\n      - src/Thing.fs\n"
    + "    generator: [\"fsgg\", \"route\", \"--json\"]\n"
    + "    generatorBasis: tool-version\n"

[<Tests>]
let tests =
    testList
        "CurrencySensing.parseManifest"
        [ test "parses the dial + the per-view currency fields" {
              match parseManifest (lines yml) with
              | Some(dial, entries) ->
                  Expect.equal dial (Some BlockOnShip) "currency-enforcement dial"
                  Expect.equal (entries |> List.map (fun e -> e.ViewId)) [ "route-projection" ] "view id"
                  let e = List.head entries
                  Expect.equal e.Kind RouteProjection "kind via viewKindOfToken"
                  Expect.equal e.Sources [ ".fsgg/route.yml"; "src/Thing.fs" ] "declared sources in order"
                  Expect.equal e.GeneratorBasis "tool-version" "generator basis"
              | None -> failtest "expected a parsed manifest"
          }

          test "absent dial ⇒ None (opt-in); views still parse" {
              let noDial = "views:\n  - id: v\n    kind: baseline\n    generatorBasis: g\n"

              match parseManifest (lines noDial) with
              | Some(dial, entries) ->
                  Expect.equal dial None "no dial ⇒ None"
                  Expect.equal (List.length entries) 1 "view parsed"
              | None -> failtest "expected a parsed manifest"
          }

          test "an unknown dial value degrades to None (the refresh host rejects it loudly; sensing stays advisory)" {
              let bad = "currency-enforcement: block-on-merge\nviews: []\n"

              match parseManifest (lines bad) with
              | Some(dial, _) -> Expect.equal dial None "unknown dial ⇒ None ⇒ no blocking finding"
              | None -> failtest "expected a parsed manifest"
          } ]
