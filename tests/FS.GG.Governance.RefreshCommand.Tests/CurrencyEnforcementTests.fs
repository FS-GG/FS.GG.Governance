module FS.GG.Governance.RefreshCommand.Tests.CurrencyEnforcementTests

// T013 — the F070 manifest-level `currency-enforcement:` dial parses into
// GenerationManifest.CurrencyEnforcement (reusing the F014 Maturity vocabulary): each canonical value maps
// to the right Maturity; absent ⇒ None; an unknown value is rejected (not silently dropped).
// T014 — refresh.json byte-identity (SC-002): the dial is NOT projected into refresh.json, so the real
// pipeline renders byte-identical refresh.json with and without it.

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

let private viewYml =
    "views:\n  - id: a\n    kind: baseline\n    output: a\n    generator: [\"x\"]\n    generatorBasis: g\n"

[<Tests>]
let tests =
    testList
        "Declaration.currencyEnforcement"
        [ test "absent key ⇒ None (opt-in / byte-identity)" {
              match Declaration.parse (ymlLines viewYml) with
              | Ok m -> Expect.equal m.CurrencyEnforcement None "absent ⇒ None"
              | Error e -> failtestf "expected Ok, got %s" e.Reason
          }

          test "each canonical value maps to the right Maturity" {
              let cases =
                  [ "observe", Observe
                    "warn", Warn
                    "block-on-pr", BlockOnPr
                    "block-on-ship", BlockOnShip
                    "block-on-release", BlockOnRelease ]

              for token, expected in cases do
                  let yml = sprintf "currency-enforcement: %s\n%s" token viewYml

                  match Declaration.parse (ymlLines yml) with
                  | Ok m -> Expect.equal m.CurrencyEnforcement (Some expected) (sprintf "%s ⇒ %A" token expected)
                  | Error e -> failtestf "expected Ok for %s, got %s" token e.Reason
          }

          test "an unknown value is rejected, not silently dropped" {
              let yml = "currency-enforcement: block-on-merge\n" + viewYml

              match Declaration.parse (ymlLines yml) with
              | Error e -> Expect.stringContains e.Reason "block-on-merge" "names the offending value"
              | Ok _ -> failtest "expected rejection of an unknown currency-enforcement value"
          }

          test "refresh.json is byte-identical with and without the dial (T014/SC-002)" {
              let renderRepo yml =
                  withTempRepo yml (fun d -> writeFile d "src.txt" "hello\n") (fun repo ->
                      let m = runReal repo (requestFor repo)
                      Loop.render m Loop.Json)

              let withDial = "currency-enforcement: block-on-ship\n" + refreshYmlOneView
              Expect.equal (renderRepo withDial) (renderRepo refreshYmlOneView) "the dial never reaches refresh.json"
          } ]
