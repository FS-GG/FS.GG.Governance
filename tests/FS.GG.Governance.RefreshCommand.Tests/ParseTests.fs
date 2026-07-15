module FS.GG.Governance.RefreshCommand.Tests.ParseTests

open Expecto
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.RefreshCommand

// `Loop.parse` (cli.md invocation table) — pure, total, no I/O on rejection (a usage error is decided
// before any port is built).

[<Tests>]
let tests =
    testList
        "Parse"
        [ test "bare argv → Ok with documented defaults" {
              match Loop.parse [] with
              | Ok r ->
                  Expect.equal r.Repo "." "default repo is the cwd"
                  Expect.isFalse r.DryRun "default is write mode"
                  Expect.equal r.Scope Loop.AllViews "default scope is all views"
                  Expect.equal r.Format Loop.Text "default format is text"
                  Expect.equal r.RefreshOut None "no refresh.json by default"
              | Error e -> failtestf "expected Ok, got Error: %s" e.Message
          }

          test "a leading bare `refresh` token is tolerated (command precedent)" {
              match Loop.parse [ "refresh"; "--dry-run" ] with
              | Ok r -> Expect.isTrue r.DryRun "the flag after the tolerated token is honored"
              | Error e -> failtestf "expected Ok, got Error: %s" e.Message
          }

          test "flags parse into the request" {
              match Loop.parse [ "--repo"; "/r"; "--dry-run"; "--json"; "--refresh-out"; "o.json" ] with
              | Ok r ->
                  Expect.equal r.Repo "/r" "repo"
                  Expect.isTrue r.DryRun "dry-run"
                  Expect.equal r.Format Loop.Json "json format"
                  Expect.equal r.RefreshOut (Some "o.json") "refresh-out path"
              | Error e -> failtestf "expected Ok, got Error: %s" e.Message
          }

          test "--view-kind / --view set the scope" {
              match Loop.parse [ "--view-kind"; "baseline" ], Loop.parse [ "--view"; "doc" ] with
              | Ok byKind, Ok byView ->
                  Expect.equal byKind.Scope (Loop.ByKind Baseline) "kind selector"
                  Expect.equal byView.Scope (Loop.ByView "doc") "view selector"
              | _ -> failtest "expected both selectors to parse"
          }

          test "an unrecognized --view-kind token is carried as Other (product-neutral)" {
              match Loop.parse [ "--view-kind"; "weird-kind" ] with
              | Ok r -> Expect.equal r.Scope (Loop.ByKind(Other "weird-kind")) "unknown kind → Other verbatim"
              | Error e -> failtestf "expected Ok, got Error: %s" e.Message
          }

          test "unknown flag → Error (exit 2)" {
              match Loop.parse [ "--nope" ] with
              | Error e -> Expect.stringContains e.Message "unknown flag" "names the offending flag"
              | Ok _ -> failtest "expected an error for an unknown flag"
          }

          test "CLI-5: a stray non-`--` positional → 'unexpected argument' (distinct from an unknown flag)" {
              match Loop.parse [ "junk" ] with
              | Error e -> Expect.stringContains e.Message "unexpected argument: junk" "names the unexpected positional"
              | Ok _ -> failtest "expected an error for a stray positional"
          }

          test "missing flag value → Error" {
              match Loop.parse [ "--repo" ] with
              | Error e -> Expect.stringContains e.Message "--repo" "names the flag missing a value"
              | Ok _ -> failtest "expected an error for a missing value"
          } ]
