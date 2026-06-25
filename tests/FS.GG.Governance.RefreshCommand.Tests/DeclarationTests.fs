module FS.GG.Governance.RefreshCommand.Tests.DeclarationTests

open Expecto
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// `Declaration.parse` over real `.fsgg/refresh.yml` text (manifest.md field rules) — pure, total,
// product-neutral.

[<Tests>]
let tests =
    testList
        "Declaration"
        [ test "a well-formed manifest parses to entries in declared order, fields populated" {
              match Declaration.parse (ymlLines refreshYmlTwoViews) with
              | Ok m ->
                  Expect.equal (m.Entries |> List.map (fun e -> e.ViewId)) [ "doc"; "cat" ] "declared order preserved"
                  let doc = m.Entries.Head
                  Expect.equal doc.Kind Baseline "kind"
                  Expect.equal doc.OutputPath "a.out" "output"
                  Expect.equal doc.Sources [ "a.txt" ] "sources"
                  Expect.equal doc.Generator [ "cp"; "a.txt"; "a.out" ] "generator argv"
                  Expect.equal doc.GeneratorBasis "g1" "generator basis"
              | Error e -> failtestf "expected Ok, got Error: %s" e.Reason
          }

          test "kind tokens are kebab/camel/underscore tolerant; unknown ⇒ Other verbatim" {
              let yml =
                  "views:\n"
                  + "  - id: a\n    kind: gateMetadata\n    output: a\n    generator: [\"x\"]\n    generatorBasis: g\n"
                  + "  - id: b\n    kind: api_surface_doc\n    output: b\n    generator: [\"x\"]\n    generatorBasis: g\n"
                  + "  - id: c\n    kind: totally-made-up\n    output: c\n    generator: [\"x\"]\n    generatorBasis: g\n"

              match Declaration.parse (ymlLines yml) with
              | Ok m ->
                  Expect.equal (m.Entries |> List.map (fun e -> e.Kind)) [ GateMetadata; ApiSurfaceDoc; Other "totally-made-up" ] "tolerant + Other"
              | Error e -> failtestf "expected Ok, got Error: %s" e.Reason
          }

          test "an empty views list is valid (nothing to refresh, FR-012)" {
              match Declaration.parse (ymlLines refreshYmlEmpty) with
              | Ok m -> Expect.isEmpty m.Entries "empty manifest"
              | Error e -> failtestf "expected Ok for empty manifest, got Error: %s" e.Reason
          }

          test "a duplicate id is rejected" {
              let yml =
                  "views:\n"
                  + "  - id: dup\n    kind: baseline\n    output: a\n    generator: [\"x\"]\n    generatorBasis: g\n"
                  + "  - id: dup\n    kind: baseline\n    output: b\n    generator: [\"x\"]\n    generatorBasis: g\n"

              match Declaration.parse (ymlLines yml) with
              | Error e -> Expect.stringContains e.Reason "dup" "names the duplicated id"
              | Ok _ -> failtest "expected a duplicate-id rejection"
          }

          test "a missing required field is rejected (total, no exception)" {
              let yml = "views:\n  - id: a\n    kind: baseline\n    output: a\n    generatorBasis: g\n" // no generator
              match Declaration.parse (ymlLines yml) with
              | Error e -> Expect.stringContains e.Reason "generator" "names the missing field"
              | Ok _ -> failtest "expected a missing-field rejection"
          }

          test "malformed YAML is an Error, never an exception (fail-safe)" {
              match Declaration.parse [ "views: ["; "  - oops" ] with
              | Error _ -> ()
              | Ok _ -> failtest "expected an Error for malformed YAML"
          } ]
